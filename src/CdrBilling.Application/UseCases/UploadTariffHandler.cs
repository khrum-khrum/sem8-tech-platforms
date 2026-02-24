using CdrBilling.Application.Abstractions;
using CdrBilling.Application.DTOs;
using CdrBilling.Domain.Entities;
using MediatR;

namespace CdrBilling.Application.UseCases;

public sealed record UploadTariffCommand(Stream FileStream, Guid SessionId) : IRequest<UploadResult>;

public interface ITariffFileParser
{
    IEnumerable<TariffEntry> Parse(Stream stream, Guid sessionId);
}

public sealed class UploadTariffHandler(
    ITariffFileParser parser,
    ITariffRepository repo)
    : IRequestHandler<UploadTariffCommand, UploadResult>
{
    public async Task<UploadResult> Handle(UploadTariffCommand request, CancellationToken cancellationToken)
    {
        var entries = parser.Parse(request.FileStream, request.SessionId).ToList();
        await repo.BulkInsertAsync(entries, cancellationToken);
        return new UploadResult(entries.Count, $"Imported {entries.Count} tariff entries.");
    }
}
