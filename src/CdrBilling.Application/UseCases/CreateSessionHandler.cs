using CdrBilling.Application.Abstractions;
using CdrBilling.Domain.Entities;
using MediatR;

namespace CdrBilling.Application.UseCases;

public sealed record CreateSessionCommand(Guid SessionId) : IRequest<Guid>;

public sealed class CreateSessionHandler(IBillingSessionRepository repo)
    : IRequestHandler<CreateSessionCommand, Guid>
{
    public async Task<Guid> Handle(CreateSessionCommand request, CancellationToken cancellationToken)
    {
        var session = BillingSession.Create(request.SessionId);
        await repo.CreateAsync(session, cancellationToken);
        return session.Id;
    }
}
