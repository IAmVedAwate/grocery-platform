using Infrastructure.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Exceptions;
using Xunit;

namespace Unit.Ai;

/// <summary>
/// The fallback chain is the one part of the AI layer worth testing
/// without a model: which exceptions justify moving to the next model,
/// which must propagate untouched, and what the caller sees when every
/// model is down. All of that is pure decision logic — see AiTools.cs for
/// why the LLM itself is deliberately not faked.
///
/// Written after a real incident: Gemini returned 503 "this model is
/// currently experiencing high demand" and the assistant surfaced a bare
/// 500 reading "An unexpected error occurred".
/// </summary>
public class FallbackChatClientTests
{
    private static FallbackChatClient Chain(params IChatClient[] clients) =>
        new(clients.Select((c, i) => ($"model-{i}", c)).ToList(), NullLogger<FallbackChatClient>.Instance);

    private static readonly ChatMessage[] Question = [new(ChatRole.User, "what's low on stock?")];

    [Fact]
    public async Task PrimarySucceeds_NeverTouchesTheFallback()
    {
        var primary = new StubChatClient(reply: "from primary");
        var fallback = new StubChatClient(reply: "from fallback");

        var response = await Chain(primary, fallback).GetResponseAsync(Question);

        Assert.Equal("from primary", response.Text);
        Assert.Equal(0, fallback.Calls);
    }

    [Theory]
    [MemberData(nameof(TransientFailures))]
    public async Task TransientPrimaryFailure_FallsThroughToTheNextModel(Exception transient)
    {
        var primary = new StubChatClient(throws: transient);
        var fallback = new StubChatClient(reply: "from fallback");

        var response = await Chain(primary, fallback).GetResponseAsync(Question);

        Assert.Equal("from fallback", response.Text);
        Assert.Equal(1, primary.Calls);
    }

    public static TheoryData<Exception> TransientFailures() =>
    [
        new TaskCanceledException("HttpClient.Timeout elapsed"),
        new TimeoutException(),
        new HttpRequestException("socket closed"),
    ];

    [Fact]
    public async Task EveryModelDown_ReportsServiceUnavailable_NotAGenericFailure()
    {
        var chain = Chain(
            new StubChatClient(throws: new HttpRequestException()),
            new StubChatClient(throws: new TimeoutException()));

        var ex = await Assert.ThrowsAsync<ServiceUnavailableAppException>(() => chain.GetResponseAsync(Question));

        // Maps to 503 and tells the user the one useful thing: retry.
        Assert.Contains("try again", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NonTransientFailure_PropagatesAndDoesNotBurnTheFallback()
    {
        // A bug in our own tool code must surface as itself, not get
        // retried against every model and then reported as "busy".
        var primary = new StubChatClient(throws: new InvalidOperationException("tool registry misconfigured"));
        var fallback = new StubChatClient(reply: "should not be reached");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Chain(primary, fallback).GetResponseAsync(Question));

        Assert.Equal(0, fallback.Calls);
    }

    [Fact]
    public async Task CallerCancellation_IsNotRetriedAcrossModels()
    {
        // The user navigating away shouldn't cost three more model calls.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var primary = new StubChatClient(throws: new TaskCanceledException());
        var fallback = new StubChatClient(reply: "should not be reached");

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => Chain(primary, fallback).GetResponseAsync(Question, cancellationToken: cts.Token));

        Assert.Equal(0, fallback.Calls);
    }

    private sealed class StubChatClient(string? reply = null, Exception? throws = null) : IChatClient
    {
        public int Calls { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (throws is not null) throw throws;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply!)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
