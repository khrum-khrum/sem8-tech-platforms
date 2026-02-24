using CdrBilling.Application.Abstractions;
using CdrBilling.Domain.Entities;
using Npgsql;
using NpgsqlTypes;

namespace CdrBilling.Infrastructure.Persistence.Repositories;

public sealed class SubscriberRepository(NpgsqlDataSource dataSource) : ISubscriberRepository
{
    public async Task BulkInsertAsync(IEnumerable<Subscriber> subscribers, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var writer = await conn.BeginBinaryImportAsync(
            "COPY subscribers (session_id, phone_number, client_name) FROM STDIN (FORMAT BINARY)", ct);

        foreach (var s in subscribers)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(s.SessionId, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(s.PhoneNumber, NpgsqlDbType.Varchar, ct);
            await writer.WriteAsync(s.ClientName, NpgsqlDbType.Varchar, ct);
        }

        await writer.CompleteAsync(ct);
    }
}
