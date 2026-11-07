using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using GymLogsParser.Configuration;
using GymLogsParser.Controllers;
using Infrastructure;
using Infrastructure.Interfaces;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tests;

/// <summary>
/// #47: the exercise catalog is shared. Every signed-in user reads it; only admins
/// (authestra role <c>admin</c>, the <c>admin</c> policy) add or change exercises.
/// Runs the real controller, attributes and <see cref="AuthorizationRegistration"/>
/// (production setup, with the fallback policy) over HTTP, with a test sign-in scheme
/// in place of the OIDC cookie.
/// </summary>
public sealed class ExerciseCatalogAuthorizationTests(PostgresFixture postgres) : DatabaseTest(postgres)
{
    private const string SeededId = "barbell-bench-press";
    private const string SeededName = "Barbell Bench Press";

    private const string NewExercise =
        """{"id":"","name":"Catalog Test Press","category":"Strength","muscleGroup":"Chest","barbell":false,"aliases":[],"muscles":[{"muscleGroup":"Chest","share":1}]}""";

    private const string RenamedBench =
        """{"name":"Barbell Bench Press Renamed","category":"Strength","muscleGroup":"Chest","barbell":true,"aliases":[],"muscles":[]}""";

    [Fact]
    public async Task NonAdmin_CanRead_ButGets403_OnCreateAndUpdate_AndCatalogIsUnchanged()
    {
        await using var host = await CatalogHost.StartAsync(Database);

        using var list = await host.SendAsync(HttpMethod.Get, "/api/exercises/definitions", TestUsers.Alice);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains(SeededId, await list.Content.ReadAsStringAsync(Ct));

        using var create = await host.SendAsync(HttpMethod.Post, "/api/exercises/definitions", TestUsers.Alice, body: NewExercise);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        using var update = await host.SendAsync(HttpMethod.Put, $"/api/exercises/definitions/{SeededId}", TestUsers.Alice, body: RenamedBench);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);

        await using var db = Database.CreateDbContext();
        Assert.False(await db.ExerciseDefinitions.AnyAsync(e => e.Id == "catalog-test-press", Ct));
        Assert.Equal(SeededName, (await db.ExerciseDefinitions.SingleAsync(e => e.Id == SeededId, Ct)).Name);
    }

    [Fact]
    public async Task Admin_CanCreateAndUpdate()
    {
        await using var host = await CatalogHost.StartAsync(Database);

        using var create = await host.SendAsync(HttpMethod.Post, "/api/exercises/definitions", TestUsers.Bob, admin: true, body: NewExercise);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        using var update = await host.SendAsync(HttpMethod.Put, $"/api/exercises/definitions/{SeededId}", TestUsers.Bob, admin: true, body: RenamedBench);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        await using var db = Database.CreateDbContext();
        Assert.Equal("Catalog Test Press", (await db.ExerciseDefinitions.SingleAsync(e => e.Id == "catalog-test-press", Ct)).Name);
        Assert.Equal("Barbell Bench Press Renamed", (await db.ExerciseDefinitions.SingleAsync(e => e.Id == SeededId, Ct)).Name);
    }

    [Fact]
    public async Task Anonymous_Gets401_OnReadAndWrite()
    {
        await using var host = await CatalogHost.StartAsync(Database);

        using var list = await host.SendAsync(HttpMethod.Get, "/api/exercises/definitions", sub: null);
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);

        using var create = await host.SendAsync(HttpMethod.Post, "/api/exercises/definitions", sub: null, body: NewExercise);
        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
    }

    /// <summary>
    /// A minimal in-process API on a loopback port: the app's controllers, JSON options and
    /// authorization registration, the catalog service on the test's database, and
    /// <see cref="TestAuthHandler"/> for sign-in.
    /// </summary>
    private sealed class CatalogHost(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public static async Task<CatalogHost> StartAsync(TestDatabase database)
        {
            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
            builder.Logging.ClearProviders();

            builder.Services.AddMemoryCache();
            builder.Services.AddScoped<AppDbContext>(_ => database.CreateDbContext());
            builder.Services.AddScoped<IExerciseCatalogService, ExerciseCatalogService>();
            builder.Services
                .AddControllers()
                .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
                .ConfigureApplicationPartManager(parts =>
                {
                    parts.ApplicationParts.Clear();
                    parts.ApplicationParts.Add(new AssemblyPart(typeof(ExerciseDefinitionsController).Assembly));
                });
            builder.Services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            builder.Services.AddAppAuthorization(builder.Environment);

            var app = builder.Build();
            app.Urls.Add("http://127.0.0.1:0");
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync(TestContext.Current.CancellationToken);

            // Kestrel replaces port 0 with the port it bound.
            var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
            return new CatalogHost(app, client);
        }

        /// <summary>Sends a request as <paramref name="sub"/> (anonymous when null), with the admin role when asked.</summary>
        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? sub, bool admin = false, string? body = null)
        {
            var request = new HttpRequestMessage(method, path);
            if (sub is not null) request.Headers.Add(TestAuthHandler.SubHeader, sub);
            if (admin) request.Headers.Add(TestAuthHandler.RolesHeader, "admin");
            if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.StopAsync(CancellationToken.None);
            await app.DisposeAsync();
        }
    }

    /// <summary>
    /// Signs the request in from headers, shaped like the OIDC principal: a <c>sub</c> claim
    /// and roles in <c>roles</c> claims (the role claim type OidcRegistration sets).
    /// No headers: not signed in, so the challenge answers 401 and a forbid 403.
    /// </summary>
    private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "test";
        public const string SubHeader = "X-Test-Sub";
        public const string RolesHeader = "X-Test-Roles";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var sub = Request.Headers[SubHeader].ToString();
            if (sub.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());

            List<Claim> claims = [new("sub", sub)];
            claims.AddRange(Request.Headers[RolesHeader]
                .SelectMany(v => (v ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Select(role => new Claim("roles", role)));

            var identity = new ClaimsIdentity(claims, SchemeName, "name", "roles");
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
