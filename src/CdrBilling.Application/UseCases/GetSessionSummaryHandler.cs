using CdrBilling.Application.Abstractions;
using CdrBilling.Application.DTOs;
using MediatR;

namespace CdrBilling.Application.UseCases;

public sealed record GetSessionSummaryQuery(Guid SessionId) : IRequest<IReadOnlyList<SubscriberSummaryDto>>;

public sealed class GetSessionSummaryHandler(ICallRecordRepository repo)
    : IRequestHandler<GetSessionSummaryQuery, IReadOnlyList<SubscriberSummaryDto>>
{
    public Task<IReadOnlyList<SubscriberSummaryDto>> Handle(
        GetSessionSummaryQuery request,
        CancellationToken cancellationToken)
        => repo.GetSummaryAsync(request.SessionId, cancellationToken);
}
