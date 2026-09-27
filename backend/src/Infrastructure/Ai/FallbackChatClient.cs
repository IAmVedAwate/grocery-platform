using Google.GenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Infrastructure.Ai;

/// <summary>
/// Tries each configured Gemini model in order, moving to the next only
/// when one fails in a way that retrying the SAME model won't fix.
///
/// This is the second of two layers. The first is inside the SDK itself:
/// each client is built with HttpRetryOptions (exponential backoff with
/// jitter over 429/5xx — see Program.cs), which handles the common case
/// where a model is briefly busy. This layer exists because that isn't
/// always enough: a specific model can stay overloaded for minutes at a
/// time, and Gemini answers that with an immediate 503 ("this model is
/// currently experiencing high demand"), so no amount of retrying the
/// same endpoint helps. Observed in practice, which is why this exists at
/// all rather than on principle.
///
/// Deliberately NOT Polly. The SDK already does backoff properly; a
/// second generic retry layer stacked on top would multiply attempts
/// (3 SDK retries inside 3 Polly retries is 9 calls, not 3) and obscure
/// which layer actually gave up — the same reasoning that chose EF Core's
/// own execution strategy over a hand-rolled policy for SQL.
/// </summary>
public sealed class FallbackChatClient(
    IReadOnlyList<(string Model, IChatClient Client)> chain,
    ILogger<FallbackChatClient> logger) : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var buffered = messages as IList<ChatMessage> ?? messages.ToList();

        for (var i = 0; i < chain.Count; i++)
        {
            var (model, client) = chain[i];
            try
            {
                return await client.GetResponseAsync(buffered, options, cancellationToken);
            }
            catch (Exception ex) when (IsTransient(ex, cancellationToken) && i < chain.Count - 1)
            {
                logger.LogWarning(ex,
                    "Gemini model {Model} unavailable after its own retries; falling back to {Fallback}",
                    model, chain[i + 1].Model);
            }
            catch (Exception ex) when (IsTransient(ex, cancellationToken))
            {
                logger.LogError(ex, "All {Count} Gemini models exhausted; last was {Model}", chain.Count, model);
                throw new ServiceUnavailableAppException(
                    "The assistant is busy right now. Please try again in a moment.");
            }
        }

        // Only reachable with an empty chain, which Program.cs prevents.
        throw new ServiceUnavailableAppException("No AI model is configured.");
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        // Nothing in this app streams yet; when something does, it needs
        // its own fallback handling (you can't retry a response you've
        // already begun writing to the client), so this deliberately
        // passes through to the primary rather than pretending to be safe.
        => chain[0].Client.GetStreamingResponseAsync(messages, options, cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null)
        => serviceType == typeof(FallbackChatClient) ? this : chain[0].Client.GetService(serviceType, serviceKey);

    public void Dispose()
    {
        foreach (var (_, client) in chain) client.Dispose();
    }

    /// <summary>
    /// Transient = the request never produced an answer for a reason that
    /// a different model might not have. A caller-cancelled request is
    /// explicitly excluded: the user navigating away must not be retried
    /// against three models in turn.
    /// </summary>
    private static bool IsTransient(Exception ex, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return false;

        return ex switch
        {
            ServerError => true,                       // 5xx, incl. "high demand"
            ClientError ce => ce.StatusCode == 429,    // rate limited
            TaskCanceledException => true,             // HttpClient timeout (ct already ruled out)
            TimeoutException => true,
            HttpRequestException => true,
            _ => false,
        };
    }
}
