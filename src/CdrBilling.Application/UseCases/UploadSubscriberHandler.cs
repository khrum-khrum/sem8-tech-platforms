using CdrBilling.Application.Abstractions;
using CdrBilling.Application.DTOs;
using CdrBilling.Domain.Entities;
using MediatR;

namespace CdrBilling.Application.UseCases;

public sealed record UploadSubscriberCommand(Stream FileStream, Guid SessionId) : IRequest<UploadResult>;

public interface ISubscriberFileParser
{
    IEnumerable<Subscriber> Parse(Stream stream, Guid sessionId);
}

public sealed class UploadSubscriberHandler(
    ISubscriberFileParser parser,
    ISubscriberRepository repo)
    : IRequestHandler<UploadSubscriberCommand, UploadResult>
{
    public async Task<UploadResult> Handle(UploadSubscriberCommand request, CancellationToken cancellationToken)
    {
        var subscribers = parser.Parse(request.FileStream, request.SessionId).ToList();
        await repo.BulkInsertAsync(subscribers, cancellationToken);
        return new UploadResult(subscribers.Count, $"Imported {subscribers.Count} subscribers.");
    }
}
