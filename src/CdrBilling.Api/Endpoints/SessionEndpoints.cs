using CdrBilling.Application.UseCases;
using MediatR;

namespace CdrBilling.Api.Endpoints;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions").WithTags("Sessions");

        // POST /api/sessions — create a new billing session
        group.MapPost("/", async (ISender sender, CancellationToken ct) =>
        {
            var sessionId = Guid.NewGuid();
            await sender.Send(new CreateSessionCommand(sessionId), ct);
            return Results.Created($"/api/sessions/{sessionId}/status", new { sessionId });
        })
        .WithName("CreateSession")
        .WithSummary("Create a new billing session");

        // GET /api/sessions/{sessionId}/status
        group.MapGet("/{sessionId:guid}/status", async (Guid sessionId, ISender sender, CancellationToken ct) =>
        {
            var status = await sender.Send(new GetSessionStatusQuery(sessionId), ct);
            return status is null ? Results.NotFound() : Results.Ok(status);
        })
        .WithName("GetSessionStatus")
        .WithSummary("Get session status and progress");

        return app;
    }
}
