using CdrBilling.Application.Abstractions;
using CdrBilling.Application.DTOs;
using CdrBilling.Domain.Enums;
using MediatR;

namespace CdrBilling.Application.UseCases;

public sealed record GetSessionStatusQuery(Guid SessionId) : IRequest<SessionStatusDto?>;

public sealed class GetSessionStatusHandler(IBillingSessionRepository repo)
    : IRequestHandler<GetSessionStatusQuery, SessionStatusDto?>
{
    public async Task<SessionStatusDto?> Handle(GetSessionStatusQuery request, CancellationToken cancellationToken)
    {
        var session = await repo.GetAsync(request.SessionId, cancellationToken);
        if (session is null) return null;

        var percent = session.TotalRecords > 0
            ? Math.Round(session.ProcessedRecords * 100.0 / session.TotalRecords, 1)
            : session.Status == SessionStatus.Completed ? 100.0 : 0.0;

        return new SessionStatusDto(
            session.Id,
            session.Status.ToString(),
            session.TotalRecords,
            session.ProcessedRecords,
            percent,
            session.ErrorMessage,
            session.CreatedAt,
            session.CompletedAt);
    }
}
