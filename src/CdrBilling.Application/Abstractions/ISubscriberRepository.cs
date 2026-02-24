using CdrBilling.Domain.Entities;

namespace CdrBilling.Application.Abstractions;

public interface ISubscriberRepository
{
    Task BulkInsertAsync(IEnumerable<Subscriber> subscribers, CancellationToken ct = default);
}
