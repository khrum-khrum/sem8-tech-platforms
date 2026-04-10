using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CdrBilling.Application.Abstractions;
using CdrBilling.Application.DTOs;
using CdrBilling.Domain.Entities;
using CdrBilling.Domain.Enums;
using CdrBilling.Domain.Services;
using CdrBilling.Infrastructure.Persistence;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace CdrBilling.Infrastructure.Persistence.Repositories;

public sealed class CallRecordRepository(
    AppDbContext db,
    NpgsqlDataSource dataSource,
    ILogger<CallRecordRepository> logger)
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

    public async IAsyncEnumerable<TarificationCall> GetUnratedAsync(
        Guid sessionId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var count = 0;

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, start_time, calling_party, called_party, direction, disposition, billable_sec
            FROM call_records
            WHERE session_id = @sessionId
              AND computed_charge IS NULL
            ORDER BY id
            """;
        cmd.Parameters.AddWithValue("sessionId", NpgsqlDbType.Uuid, sessionId);

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
        while (await reader.ReadAsync(ct))
        {
            count++;

            var id = reader.GetInt64(0);
            var startTime = reader.GetFieldValue<DateTimeOffset>(1);
            var callingParty = reader.GetString(2);
            var calledParty = reader.GetString(3);
            var direction = Enum.Parse<CallDirection>(reader.GetString(4), ignoreCase: false);
            var disposition = Enum.Parse<Disposition>(reader.GetString(5), ignoreCase: false);
            var billableSec = reader.GetInt32(6);

            yield return new TarificationCall(
                id,
                startTime,
                direction,
                disposition,
                billableSec,
                NormalizeDigits(direction == CallDirection.Outgoing
                    ? calledParty
                    : callingParty));
        }

        logger.LogInformation(
            "Streamed {Count} unrated call records for session {SessionId} in {ElapsedMs} ms.",
            count,
            sessionId,
            stopwatch.ElapsedMilliseconds);
    }

    public async Task<ICallRecordChargeBatchUpdater> CreateChargeBatchUpdaterAsync(CancellationToken ct = default)
        => await CallRecordChargeBatchUpdater.CreateAsync(dataSource, logger, ct);

    public Task<int> CountAsync(Guid sessionId, CancellationToken ct = default)
        => db.CallRecords.CountAsync(r => r.SessionId == sessionId, ct);

    public async Task BulkUpdateChargesAsync(
        IEnumerable<(long Id, decimal Charge, long TariffId)> updates,
        CancellationToken ct = default)
    {
        await using var updater = await CreateChargeBatchUpdaterAsync(ct);
        await updater.ApplyAsync(updates, ct);
    }

    public async Task<IReadOnlyList<SubscriberSummaryDto>> GetSummaryAsync(
        Guid sessionId, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        const string sql = """
            WITH outgoing_calls AS (
                SELECT
                    cr.calling_party         AS phone_number,
                    COUNT(cr.id)             AS call_count,
                    COALESCE(SUM(cr.billable_sec), 0)    AS total_billable_sec,
                    COALESCE(SUM(cr.computed_charge), 0) AS total_charge
                FROM call_records cr
                WHERE cr.session_id = @SessionId
                  AND cr.direction = 'Outgoing'
                  AND cr.disposition = 'Answered'
                GROUP BY cr.calling_party
            ),
            incoming_calls AS (
                SELECT
                    cr.called_party          AS phone_number,
                    COUNT(cr.id)             AS call_count,
                    COALESCE(SUM(cr.billable_sec), 0)    AS total_billable_sec,
                    COALESCE(SUM(cr.computed_charge), 0) AS total_charge
                FROM call_records cr
                WHERE cr.session_id = @SessionId
                  AND cr.direction = 'Incoming'
                  AND cr.disposition = 'Answered'
                GROUP BY cr.called_party
            )
            SELECT
                s.phone_number  AS PhoneNumber,
                s.client_name   AS ClientName,
                COALESCE(oc.call_count, 0) + COALESCE(ic.call_count, 0)                  AS CallCount,
                COALESCE(oc.total_billable_sec, 0) + COALESCE(ic.total_billable_sec, 0)  AS TotalBillableSec,
                COALESCE(oc.total_charge, 0)                                              AS TotalCharge
            FROM subscribers s
            LEFT JOIN outgoing_calls oc
                ON oc.phone_number = s.phone_number
            LEFT JOIN incoming_calls ic
                ON ic.phone_number = s.phone_number
            WHERE s.session_id = @SessionId
            ORDER BY TotalCharge DESC
            """;

        var rows = await conn.QueryAsync<SubscriberSummaryDto>(
            new CommandDefinition(sql, new { SessionId = sessionId }, commandTimeout: 180, cancellationToken: ct));
        var items = rows.AsList();

        logger.LogInformation(
            "Loaded summary for session {SessionId}: {Count} rows in {ElapsedMs} ms.",
            sessionId,
            items.Count,
            stopwatch.ElapsedMilliseconds);

        return items;
    }

    public async Task<PagedResult<CallRecordDetailDto>> GetDetailAsync(
        Guid sessionId,
        string? phoneNumber,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
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

        logger.LogInformation(
            "Loaded call detail page for session {SessionId}: page {Page}, size {PageSize}, total {TotalCount} in {ElapsedMs} ms.",
            sessionId,
            page,
            pageSize,
            totalCount,
            stopwatch.ElapsedMilliseconds);

        return new PagedResult<CallRecordDetailDto>(items, page, pageSize, totalCount);
    }

    private static string NormalizeDigits(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        Span<char> buffer = stackalloc char[input.Length];
        var length = 0;
        foreach (var ch in input)
        {
            if (char.IsAsciiDigit(ch))
                buffer[length++] = ch;
        }

        return length == 0 ? string.Empty : new string(buffer[..length]);
    }

    private sealed class CallRecordChargeBatchUpdater(
        NpgsqlConnection connection,
        ILogger logger)
        : ICallRecordChargeBatchUpdater
    {
        private const string TempTableName = "tmp_charges";

        public static async Task<CallRecordChargeBatchUpdater> CreateAsync(
            NpgsqlDataSource dataSource,
            ILogger logger,
            CancellationToken ct)
        {
            var connection = await dataSource.OpenConnectionAsync(ct);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"""
                CREATE TEMP TABLE IF NOT EXISTS {TempTableName} (
                    id BIGINT,
                    charge NUMERIC(12,4),
                    tariff_id BIGINT
                ) ON COMMIT PRESERVE ROWS
                """;
            await cmd.ExecuteNonQueryAsync(ct);
            return new CallRecordChargeBatchUpdater(connection, logger);
        }

        public async Task ApplyAsync(
            IEnumerable<(long Id, decimal Charge, long TariffId)> updates,
            CancellationToken ct = default)
        {
            var rows = updates as IReadOnlyCollection<(long Id, decimal Charge, long TariffId)> ?? updates.ToList();
            if (rows.Count == 0)
                return;

            var stopwatch = Stopwatch.StartNew();
            await using var tx = await connection.BeginTransactionAsync(ct);

            await using (var truncateCmd = connection.CreateCommand())
            {
                truncateCmd.Transaction = tx;
                truncateCmd.CommandText = $"TRUNCATE TABLE {TempTableName}";
                await truncateCmd.ExecuteNonQueryAsync(ct);
            }

            await using (var writer = await connection.BeginBinaryImportAsync(
                             $"COPY {TempTableName} (id, charge, tariff_id) FROM STDIN (FORMAT BINARY)", ct))
            {
                foreach (var (id, charge, tariffId) in rows)
                {
                    await writer.StartRowAsync(ct);
                    await writer.WriteAsync(id, NpgsqlDbType.Bigint, ct);
                    await writer.WriteAsync(charge, NpgsqlDbType.Numeric, ct);
                    await writer.WriteAsync(tariffId, NpgsqlDbType.Bigint, ct);
                }

                await writer.CompleteAsync(ct);
            }

            await using (var updateCmd = connection.CreateCommand())
            {
                updateCmd.Transaction = tx;
                updateCmd.CommandText = $"""
                    UPDATE call_records cr
                    SET computed_charge = tc.charge,
                        applied_tariff_id = tc.tariff_id
                    FROM {TempTableName} tc
                    WHERE cr.id = tc.id
                    """;
                await updateCmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);

            logger.LogDebug(
                "Applied {Count} charge updates in {ElapsedMs} ms.",
                rows.Count,
                stopwatch.ElapsedMilliseconds);
        }

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
        }
    }

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
