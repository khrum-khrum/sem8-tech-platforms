using CdrBilling.Application.UseCases;
using MediatR;

namespace CdrBilling.Api.Endpoints;

public static class UploadEndpoints
{
    public static IEndpointRouteBuilder MapUploadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions/{sessionId:guid}").WithTags("Upload");

        // POST /api/sessions/{sessionId}/upload/cdr
        group.MapPost("/upload/cdr", async (
            Guid sessionId,
            IFormFile file,
            ISender sender,
            CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var result = await sender.Send(new UploadCdrCommand(stream, sessionId), ct);
            return Results.Ok(result);
        })
        .DisableAntiforgery()
        .WithName("UploadCdr")
        .WithSummary("Upload CDR file (pipe-delimited)");

        // POST /api/sessions/{sessionId}/upload/tariff
        group.MapPost("/upload/tariff", async (
            Guid sessionId,
            IFormFile file,
            ISender sender,
            CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var result = await sender.Send(new UploadTariffCommand(stream, sessionId), ct);
            return Results.Ok(result);
        })
        .DisableAntiforgery()
        .WithName("UploadTariff")
        .WithSummary("Upload tariff file (semicolon CSV)");

        // POST /api/sessions/{sessionId}/upload/subscribers
        group.MapPost("/upload/subscribers", async (
            Guid sessionId,
            IFormFile file,
            ISender sender,
            CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var result = await sender.Send(new UploadSubscriberCommand(stream, sessionId), ct);
            return Results.Ok(result);
        })
        .DisableAntiforgery()
        .WithName("UploadSubscribers")
        .WithSummary("Upload subscriber base file (semicolon CSV)");

        return app;
    }
}
