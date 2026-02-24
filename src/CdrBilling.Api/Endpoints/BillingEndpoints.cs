using CdrBilling.Application.UseCases;
using MediatR;

namespace CdrBilling.Api.Endpoints;

public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions/{sessionId:guid}").WithTags("Billing");

        // POST /api/sessions/{sessionId}/run — start tariffication
        group.MapPost("/run", async (
            Guid sessionId,
            ISender sender,
            IServiceScopeFactory scopeFactory,
            HttpContext ctx) =>
        {
            // Fire-and-forget in a dedicated DI scope so DbContext lifetime is correct
            _ = Task.Run(async () =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var inner = scope.ServiceProvider.GetRequiredService<ISender>();
                await inner.Send(new RunTarificationCommand(sessionId));
            });

            return Results.Accepted($"/api/sessions/{sessionId}/status",
                new { message = "Tariffication started.", sessionId });
        })
        .WithName("RunTarification")
        .WithSummary("Start the tariffication procedure (returns 202 immediately)");

        // GET /api/sessions/{sessionId}/results/summary
        group.MapGet("/results/summary", async (
            Guid sessionId,
            ISender sender,
            CancellationToken ct) =>
        {
            var summary = await sender.Send(new GetSessionSummaryQuery(sessionId), ct);
            return Results.Ok(summary);
        })
        .WithName("GetResultsSummary")
        .WithSummary("Total charged amount per subscriber");

        // GET /api/sessions/{sessionId}/results/calls?phone=...&page=1&pageSize=50
        group.MapGet("/results/calls", async (
            Guid sessionId,
            string? phone,
            int page,
            int pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);
            var result = await sender.Send(new GetCallDetailsQuery(sessionId, phone, page, pageSize), ct);
            return Results.Ok(result);
        })
        .WithName("GetCallDetails")
        .WithSummary("Paged call records with computed charges and applied tariff reference");

        return app;
    }
}
