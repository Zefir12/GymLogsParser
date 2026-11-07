using System.Net;
using System.Text;
using Core.Configuration;
using Core.Models;
using Infrastructure.Helpers;
using Infrastructure.Interfaces;
using Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Tests;

/// <summary>
/// #45: the parse cache key includes the owner, so one user's cached parse (built with
/// their bodyweight) is never served to another. DeepSeek is a stub that counts requests.
/// </summary>
public sealed class DeepSeekParseCacheTests : IDisposable
{
    private const string Log = "2026-11-02 bench 100x5";

    private readonly CountingHandler _handler = new();
    private readonly RecordingBodyweights _bodyweights = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly DeepSeekService _service;

    public DeepSeekParseCacheTests()
    {
        var client = new HttpClient(_handler) { BaseAddress = new Uri("https://deepseek.invalid/") };
        _service = new DeepSeekService(
            client,
            Options.Create(new DeepSeekOptions { ApiKey = "test" }),
            _cache,
            new StubCatalog(),
            _bodyweights,
            NullLogger<DeepSeekService>.Instance);
    }

    [Fact]
    public async Task SameUserSameLog_IsServedFromTheCache()
    {
        var first = await _service.ParseWorkoutAsync(TestUsers.Alice, Log, false, TestContext.Current.CancellationToken);
        var second = await _service.ParseWorkoutAsync(TestUsers.Alice, Log, false, TestContext.Current.CancellationToken);

        Assert.Equal(1, _handler.Calls);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task OtherUserSameLog_IsParsedAgain()
    {
        var alice = await _service.ParseWorkoutAsync(TestUsers.Alice, Log, false, TestContext.Current.CancellationToken);
        var bob = await _service.ParseWorkoutAsync(TestUsers.Bob, Log, false, TestContext.Current.CancellationToken);

        Assert.Equal(2, _handler.Calls);
        Assert.NotSame(alice, bob);

        // Both entries stay cached, each for its own user.
        Assert.Same(alice, await _service.ParseWorkoutAsync(TestUsers.Alice, Log, false, TestContext.Current.CancellationToken));
        Assert.Same(bob, await _service.ParseWorkoutAsync(TestUsers.Bob, Log, false, TestContext.Current.CancellationToken));
        Assert.Equal(2, _handler.Calls);
    }

    [Fact]
    public async Task BodyweightHint_ComesFromTheCallersHistory()
    {
        await _service.ParseWorkoutAsync(TestUsers.Bob, Log, false, TestContext.Current.CancellationToken);

        Assert.Equal(TestUsers.Bob, Assert.Single(_bodyweights.Owners));
    }

    public void Dispose() => _cache.Dispose();

    private sealed class CountingHandler : HttpMessageHandler
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            const string body = """
                {
                  "choices": [{ "message": { "content": "{\"title\":\"Push\",\"exercises\":[]}" }, "finish_reason": "stop" }],
                  "usage": { "prompt_tokens": 10, "completion_tokens": 5, "total_tokens": 15 }
                }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class RecordingBodyweights : IBodyweightService
    {
        public List<string> Owners { get; } = [];

        public Task<BodyweightHistory> GetHistoryAsync(string ownerId, CancellationToken cancellationToken = default)
        {
            Owners.Add(ownerId);
            return Task.FromResult(BodyweightHistory.Empty);
        }
    }

    private sealed class StubCatalog : IExerciseCatalogService
    {
        private static readonly ExerciseCatalogSnapshot Snapshot = new([], "v1");

        public Task<ExerciseCatalogSnapshot> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Snapshot);

        public Task<ExerciseDefinition> CreateAsync(ExerciseDefinition definition, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ExerciseDefinition?> UpdateAsync(string id, ExerciseDefinition definition, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
