using CdrBilling.Application.Abstractions;
using CdrBilling.Domain.Entities;
using CdrBilling.Domain.Enums;
using CdrBilling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace CdrBilling.Infrastructure.Persistence.Repositories;

public sealed class TariffRepository(AppDbContext db, NpgsqlDataSource dataSource)
    : ITariffRepository
{
    public async Task BulkInsertAsync(IEnumerable<TariffEntry> entries, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var writer = await conn.BeginBinaryImportAsync(
            """
            COPY tariff_entries (session_id, prefix, destination, rate_per_min, connection_fee,
                timeband_start, timeband_end, weekday_mask, priority, effective_date, expiry_date)
            FROM STDIN (FORMAT BINARY)
            """, ct);

        foreach (var t in entries)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(t.SessionId, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(t.Prefix, NpgsqlDbType.Varchar, ct);
            await writer.WriteAsync(t.Destination, NpgsqlDbType.Varchar, ct);
            await writer.WriteAsync(t.RatePerMin, NpgsqlDbType.Numeric, ct);
            await writer.WriteAsync(t.ConnectionFee, NpgsqlDbType.Numeric, ct);
            await writer.WriteAsync(t.TimebandStart, NpgsqlDbType.Time, ct);
            await writer.WriteAsync(t.TimebandEnd, NpgsqlDbType.Time, ct);
            await writer.WriteAsync((short)t.WeekdayMask, NpgsqlDbType.Smallint, ct);
            await writer.WriteAsync(t.Priority, NpgsqlDbType.Integer, ct);
            await writer.WriteAsync(t.EffectiveDate, NpgsqlDbType.Date, ct);
            if (t.ExpiryDate.HasValue)
                await writer.WriteAsync(t.ExpiryDate.Value, NpgsqlDbType.Date, ct);
            else
                await writer.WriteNullAsync(ct);
        }

        await writer.CompleteAsync(ct);
    }

    public async Task<IReadOnlyList<TariffEntry>> GetAllForSessionAsync(
        Guid sessionId, CancellationToken ct = default)
        => await db.TariffEntries
            .AsNoTracking()
            .Where(t => t.SessionId == sessionId)
            .ToListAsync(ct);
}
