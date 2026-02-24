using CdrBilling.Application.Abstractions;
using CdrBilling.Application.DTOs;
using CdrBilling.Domain.Entities;
using MediatR;

namespace CdrBilling.Application.UseCases;

public sealed record UploadCdrCommand(Stream FileStream, Guid SessionId) : IRequest<UploadResult>;

public interface ICdrFileParser
{
    IAsyncEnumerable<CallRecord> ParseAsync(Stream stream, Guid sessionId, CancellationToken ct = default);
}

public sealed class UploadCdrHandler(
    ICdrFileParser parser,
    ICallRecordRepository repo)
    : IRequestHandler<UploadCdrCommand, UploadResult>
{
    public async Task<UploadResult> Handle(UploadCdrCommand request, CancellationToken cancellationToken)
    {
        var records = parser.ParseAsync(request.FileStream, request.SessionId, cancellationToken);
        var counter = new CountingEnumerable(records);
        await repo.BulkInsertAsync(counter, cancellationToken);
        return new UploadResult(counter.Count, $"Imported {counter.Count} CDR records.");
    }

    // Wraps the IAsyncEnumerable to count consumed items without buffering
    private sealed class CountingEnumerable(IAsyncEnumerable<CallRecord> source)
        : IAsyncEnumerable<CallRecord>
    {
        public int Count { get; private set; }

        public async IAsyncEnumerator<CallRecord> GetAsyncEnumerator(CancellationToken ct = default)
        {
            await foreach (var item in source.WithCancellation(ct))
            {
                Count++;
                yield return item;
            }
        }
    }
}
