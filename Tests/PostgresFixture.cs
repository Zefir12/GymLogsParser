using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Tests.PostgresFixture))]

namespace Tests;

/// <summary>
/// One throwaway PostgreSQL server for the whole test run. Started with Testcontainers
/// (needs Docker), unless <c>GYMLOGS_TEST_DATABASE</c> holds the connection string of an
/// existing server (CI uses a service container). That role must be allowed to create
/// databases. Never point it at the production server.
/// <para>
/// The migrations run once, into a template database; every test then gets its own copy
/// (<see cref="CreateDatabaseAsync"/>), so tests can change the seeded legacy rows
/// without seeing each other. Connections are not pooled: a database can only be copied
/// or dropped while nobody is connected to it.
/// </para>
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string ConnectionStringVariable = "GYMLOGS_TEST_DATABASE";

    private static readonly string? ExternalConnectionString =
        Environment.GetEnvironmentVariable(ConnectionStringVariable) is { Length: > 0 } value ? value : null;

    private readonly PostgreSqlContainer? _container =
        ExternalConnectionString is null ? new PostgreSqlBuilder("postgres:17-alpine").Build() : null;

    private readonly string _templateName = $"gymlogs_template_{Guid.NewGuid():N}";
    private readonly SemaphoreSlim _createLock = new(1, 1);

    private string ServerConnectionString => ExternalConnectionString ?? _container!.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        if (_container is not null)
        {
            await _container.StartAsync();
        }

        await ExecuteAsync($"""CREATE DATABASE "{_templateName}" """, CancellationToken.None);
        await using var db = CreateContext(ConnectionStringFor(_templateName));
        await db.Database.MigrateAsync();
    }

    /// <summary>A new database with every migration applied (and their seed data), dropped on dispose.</summary>
    public async Task<TestDatabase> CreateDatabaseAsync(CancellationToken cancellationToken)
    {
        var name = $"gymlogs_test_{Guid.NewGuid():N}";

        // Copies of one template are made one at a time.
        await _createLock.WaitAsync(cancellationToken);
        try
        {
            await ExecuteAsync($"""CREATE DATABASE "{name}" TEMPLATE "{_templateName}" """, cancellationToken);
        }
        finally
        {
            _createLock.Release();
        }

        return new TestDatabase(this, name, ConnectionStringFor(name));
    }

    internal Task DropDatabaseAsync(string name) =>
        ExecuteAsync($"""DROP DATABASE IF EXISTS "{name}" WITH (FORCE)""", CancellationToken.None);

    // Every test has its own connection string; EF would otherwise fail after twenty
    // internal service providers if a provider ever keyed them by connection string.
    internal static AppDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options);

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        // An external server outlives the run.
        await DropDatabaseAsync(_templateName);
    }

    private string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(ServerConnectionString) { Database = database, Pooling = false }.ConnectionString;

    private async Task ExecuteAsync(string sql, CancellationToken cancellationToken)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(ServerConnectionString) { Pooling = false }.ConnectionString;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

/// <summary>One test's own migrated database. Also the context factory for services that need one.</summary>
public sealed class TestDatabase(PostgresFixture fixture, string name, string connectionString)
    : IDbContextFactory<AppDbContext>, IAsyncDisposable
{
    public AppDbContext CreateDbContext() => PostgresFixture.CreateContext(connectionString);

    public ValueTask DisposeAsync() => new(fixture.DropDatabaseAsync(name));
}

/// <summary>Base for tests that need a database: each test gets a fresh one.</summary>
public abstract class DatabaseTest(PostgresFixture postgres) : IAsyncLifetime
{
    protected TestDatabase Database { get; private set; } = null!;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => Database = await postgres.CreateDatabaseAsync(Ct);

    public ValueTask DisposeAsync() => Database.DisposeAsync();
}
