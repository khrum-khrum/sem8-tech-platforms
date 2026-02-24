using CdrBilling.Application.Abstractions;
using CdrBilling.Application.DTOs;
using CdrBilling.Domain.Entities;
using CdrBilling.Domain.Enums;
using CdrBilling.Infrastructure.Persistence;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace CdrBilling.Infrastructure.Persistence.Repositories;

public sealed class CallRecordRepository(AppDbContext db, NpgsqlDataSource dataSource)
    : ICallRecordRepository
{
    public async Task BulkInsertAsync(IAsyncEnumerable<CallRecord> records, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var writer = await conn.BeginBinaryImportAsync(
            """
            COPY call_records (session_id, start_time, end_time, calling_party, called_party,
                direction, disposition, duration_sec, billable_sec, original_charge,
                account_code, call_id, trunk_name)
            FROM STDIN (FORMAT BINARY)
            """, ct);

        await foreach (var r in records.WithCancellation(ct))
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(r.SessionId, NpgsqlDbType.Uuid, ct);
            await writer.WriteAsync(r.StartTime, NpgsqlDbType.TimestampTz, ct);
            await writer.WriteAsync(r.EndTime, NpgsqlDbType.TimestampTz, ct);
            await writer.WriteAsync(r.CallingParty, NpgsqlDbType.Varchar, ct);
            await writer.WriteAsync(r.CalledParty, NpgsqlDbType.Varchar, ct);
            await writer.WriteAsync(r.Direction.ToString(), NpgsqlDbType.Varchar, ct);
            await writer.WriteAsync(r.Disposition.ToString(), NpgsqlDbType.Varchar, ct);
            await writer.WriteAsync(r.DurationSec, NpgsqlDbType.Integer, ct);
            await writer.WriteAsync(r.BillableSec, NpgsqlDbType.Integer, ct);
            if (r.OriginalCharge.HasValue)
                await writer.WriteAsync(r.OriginalCharge.Value, NpgsqlDbType.Numeric, ct);
            else
                await writer.WriteNullAsync(ct);
            if (r.AccountCode is not null)
                await writer.WriteAsync(r.AccountCode, NpgsqlDbType.Varchar, ct);
            else
                await writer.WriteNullAsync(ct);
            await writer.WriteAsync(r.CallId, NpgsqlDbType.Varchar, ct);
            if (r.TrunkName is not null)
                await writer.WriteAsync(r.TrunkName, NpgsqlDbType.Varchar, ct);
            else
                await writer.WriteNullAsync(ct);
        }

        await writer.CompleteAsync(ct);
    }

    public IAsyncEnumerable<CallRecord> GetUnratedAsync(Guid sessionId, CancellationToken ct = default)
        => db.CallRecords
            .AsNoTracking()
            .Where(r => r.SessionId == sessionId && r.ComputedCharge == null)
            .AsAsyncEnumerable();

    public Task<int> CountAsync(Guid sessionId, CancellationToken ct = default)
        => db.CallRecords.CountAsync(r => r.SessionId == sessionId, ct);

    public async Task BulkUpdateChargesAsync(
        IEnumerable<(long Id, decimal Charge, long TariffId)> updates,
        CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        // Use a temp table + UPDATE FROM for batch efficiency
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TEMP TABLE tmp_charges (id BIGINT, charge NUMERIC(12,4), tariff_id BIGINT) ON COMMIT DROP";
        await cmd.ExecuteNonQueryAsync(ct);

        await using var writer = await conn.BeginBinaryImportAsync(
            "COPY tmp_charges (id, charge, tariff_id) FROM STDIN (FORMAT BINARY)", ct);

        foreach (var (id, charge, tariffId) in updates)
        {
            await writer.StartRowAsync(ct);
            await writer.WriteAsync(id, NpgsqlDbType.Bigint, ct);
            await writer.WriteAsync(charge, NpgsqlDbType.Numeric, ct);
            await writer.WriteAsync(tariffId, NpgsqlDbType.Bigint, ct);
        }
        await writer.CompleteAsync(ct);

        await using var updateCmd = conn.CreateCommand();
        updateCmd.CommandText = """
            UPDATE call_records cr
            SET computed_charge = tc.charge,
                applied_tariff_id = tc.tariff_id
            FROM tmp_charges tc
            WHERE cr.id = tc.id
            """;
        await updateCmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<SubscriberSummaryDto>> GetSummaryAsync(
        Guid sessionId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        const string sql = """
            SELECT
                s.phone_number  AS PhoneNumber,
                s.client_name   AS ClientName,
                COUNT(cr.id)    AS CallCount,
                COALESCE(SUM(cr.billable_sec), 0)     AS TotalBillableSec,
                COALESCE(SUM(cr.computed_charge), 0)  AS TotalCharge
            FROM subscribers s
            LEFT JOIN call_records cr
                ON (cr.calling_party = s.phone_number OR cr.called_party = s.phone_number)
               AND cr.session_id = @SessionId
               AND cr.disposition = 'Answered'
            WHERE s.session_id = @SessionId
            GROUP BY s.phone_number, s.client_name
            ORDER BY TotalCharge DESC
            """;

        var rows = await conn.QueryAsync<SubscriberSummaryDto>(
            new CommandDefinition(sql, new { SessionId = sessionId }, cancellationToken: ct));

        return rows.AsList();
    }

    public async Task<PagedResult<CallRecordDetailDto>> GetDetailAsync(
        Guid sessionId,
        string? phoneNumber,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        var phoneFilter = string.IsNullOrWhiteSpace(phoneNumber)
            ? string.Empty
            : "AND (cr.calling_party = @Phone OR cr.called_party = @Phone)";

        var countSql = $"""
            SELECT COUNT(*) FROM call_records cr
            WHERE cr.session_id = @SessionId {phoneFilter}
            """;

        var totalCount = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, new { SessionId = sessionId, Phone = phoneNumber }, cancellationToken: ct));

        var dataSql = $"""
            SELECT
                cr.id           AS Id,
                cr.start_time   AS StartTime,
                cr.end_time     AS EndTime,
                cr.calling_party AS CallingParty,
                cr.called_party  AS CalledParty,
                cr.direction    AS Direction,
                cr.disposition  AS Disposition,
                cr.duration_sec AS DurationSec,
                cr.billable_sec AS BillableSec,
                cr.original_charge AS OriginalCharge,
                cr.computed_charge AS ComputedCharge,
                cr.account_code AS AccountCode,
                cr.call_id      AS CallId,
                cr.trunk_name   AS TrunkName,
                te.id           AS TariffId,
                te.prefix       AS TariffPrefix,
                te.destination  AS TariffDestination,
                te.rate_per_min AS TariffRatePerMin,
                te.connection_fee AS TariffConnectionFee
            FROM call_records cr
            LEFT JOIN tariff_entries te ON te.id = cr.applied_tariff_id
            WHERE cr.session_id = @SessionId {phoneFilter}
            ORDER BY cr.start_time
            LIMIT @PageSize OFFSET @Offset
            """;

        var rows = await conn.QueryAsync<CallRecordRow>(
            new CommandDefinition(dataSql,
                new { SessionId = sessionId, Phone = phoneNumber, PageSize = pageSize, Offset = (page - 1) * pageSize },
                cancellationToken: ct));

        var items = rows.Select(r => new CallRecordDetailDto(
            r.Id, r.StartTime, r.EndTime, r.CallingParty, r.CalledParty,
            r.Direction, r.Disposition, r.DurationSec, r.BillableSec,
            r.OriginalCharge, r.ComputedCharge, r.AccountCode, r.CallId, r.TrunkName,
            r.TariffId.HasValue
                ? new AppliedTariffDto(r.TariffId.Value, r.TariffPrefix!, r.TariffDestination!,
                                       r.TariffRatePerMin!.Value, r.TariffConnectionFee!.Value)
                : null
        )).ToList();

        return new PagedResult<CallRecordDetailDto>(items, page, pageSize, totalCount);
    }

    // Flat projection for Dapper multi-column query
    private sealed class CallRecordRow
    {
        public long Id { get; init; }
        public DateTimeOffset StartTime { get; init; }
        public DateTimeOffset EndTime { get; init; }
        public string CallingParty { get; init; } = default!;
        public string CalledParty { get; init; } = default!;
        public string Direction { get; init; } = default!;
        public string Disposition { get; init; } = default!;
        public int DurationSec { get; init; }
        public int BillableSec { get; init; }
        public decimal? OriginalCharge { get; init; }
        public decimal? ComputedCharge { get; init; }
        public string? AccountCode { get; init; }
        public string CallId { get; init; } = default!;
        public string? TrunkName { get; init; }
        public long? TariffId { get; init; }
        public string? TariffPrefix { get; init; }
        public string? TariffDestination { get; init; }
        public decimal? TariffRatePerMin { get; init; }
        public decimal? TariffConnectionFee { get; init; }
    }
}
