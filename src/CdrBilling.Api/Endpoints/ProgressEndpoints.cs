using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CdrBilling.Infrastructure.Realtime;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CdrBilling.Api.Endpoints;

public static class ProgressEndpoints
{
    public static IEndpointRouteBuilder MapProgressEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions/{sessionId:guid}").WithTags("Progress");

        // GET /api/sessions/{sessionId}/progress — SSE stream
        group.MapGet("/progress", (
            Guid sessionId,
            SseProgressHub hub,
            CancellationToken ct) =>
        {
            var channel = hub.GetOrCreateChannel(sessionId);

            return TypedResults.ServerSentEvents(
                Stream(channel, hub, sessionId, ct),
                eventType: "progress");
        })
        .WithName("GetProgress")
        .WithSummary("Server-Sent Events stream for real-time tariffication progress");

        return app;
    }

    private static async IAsyncEnumerable<ProgressEvent> Stream(
        Channel<ProgressEvent> channel,
        SseProgressHub hub,
        Guid sessionId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(ct))
            {
                yield return evt;
                if (evt.Status is "completed" or "failed")
                    yield break;
            }
        }
        finally
        {
            hub.RemoveChannel(sessionId);
        }
    }
}
