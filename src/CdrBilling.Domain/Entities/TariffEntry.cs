using CdrBilling.Domain.Enums;

namespace CdrBilling.Domain.Entities;

public sealed class TariffEntry
{
    public long Id { get; private set; }
    public Guid SessionId { get; private set; }
    public string Prefix { get; private set; } = default!;
    public string Destination { get; private set; } = default!;
    public decimal RatePerMin { get; private set; }
    public decimal ConnectionFee { get; private set; }
    public TimeOnly TimebandStart { get; private set; }
    public TimeOnly TimebandEnd { get; private set; }
    public DayOfWeekMask WeekdayMask { get; private set; }
    public int Priority { get; private set; }
    public DateOnly EffectiveDate { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }

    private TariffEntry() { }

    public static TariffEntry Create(
        Guid sessionId,
        string prefix,
        string destination,
        decimal ratePerMin,
        decimal connectionFee,
        TimeOnly timebandStart,
        TimeOnly timebandEnd,
        DayOfWeekMask weekdayMask,
        int priority,
        DateOnly effectiveDate,
        DateOnly? expiryDate) => new()
    {
        SessionId = sessionId,
        Prefix = prefix,
        Destination = destination,
        RatePerMin = ratePerMin,
        ConnectionFee = connectionFee,
        TimebandStart = timebandStart,
        TimebandEnd = timebandEnd,
        WeekdayMask = weekdayMask,
        Priority = priority,
        EffectiveDate = effectiveDate,
        ExpiryDate = expiryDate
    };
}
