using CdrBilling.Domain.Enums;

namespace CdrBilling.Domain.Entities;

public sealed class CallRecord
{
    public long Id { get; private set; }
    public Guid SessionId { get; private set; }
    public DateTimeOffset StartTime { get; private set; }
    public DateTimeOffset EndTime { get; private set; }
    public string CallingParty { get; private set; } = default!;
    public string CalledParty { get; private set; } = default!;
    public CallDirection Direction { get; private set; }
    public Disposition Disposition { get; private set; }
    public int DurationSec { get; private set; }
    public int BillableSec { get; private set; }
    public decimal? OriginalCharge { get; private set; }
    public string? AccountCode { get; private set; }
    public string CallId { get; private set; } = default!;
    public string? TrunkName { get; private set; }

    // Set after tariffication
    public decimal? ComputedCharge { get; private set; }
    public long? AppliedTariffId { get; private set; }

    // Navigation
    public TariffEntry? AppliedTariff { get; private set; }

    private CallRecord() { }

    public static CallRecord Create(
        Guid sessionId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string callingParty,
        string calledParty,
        CallDirection direction,
        Disposition disposition,
        int durationSec,
        int billableSec,
        decimal? originalCharge,
        string? accountCode,
        string callId,
        string? trunkName) => new()
    {
        SessionId = sessionId,
        StartTime = startTime,
        EndTime = endTime,
        CallingParty = callingParty,
        CalledParty = calledParty,
        Direction = direction,
        Disposition = disposition,
        DurationSec = durationSec,
        BillableSec = billableSec,
        OriginalCharge = originalCharge,
        AccountCode = accountCode,
        CallId = callId,
        TrunkName = trunkName
    };

    public void ApplyTariff(decimal charge, long tariffId)
    {
        ComputedCharge = charge;
        AppliedTariffId = tariffId;
    }

    public void SetNoTariff()
    {
        ComputedCharge = 0m;
    }
}
