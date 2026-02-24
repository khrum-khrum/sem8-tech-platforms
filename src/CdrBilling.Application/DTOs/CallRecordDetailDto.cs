namespace CdrBilling.Application.DTOs;

public sealed record CallRecordDetailDto(
    long Id,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string CallingParty,
    string CalledParty,
    string Direction,
    string Disposition,
    int DurationSec,
    int BillableSec,
    decimal? OriginalCharge,
    decimal? ComputedCharge,
    string? AccountCode,
    string CallId,
    string? TrunkName,
    AppliedTariffDto? AppliedTariff);

public sealed record AppliedTariffDto(
    long Id,
    string Prefix,
    string Destination,
    decimal RatePerMin,
    decimal ConnectionFee);
