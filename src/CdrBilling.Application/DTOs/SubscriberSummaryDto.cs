namespace CdrBilling.Application.DTOs;

public sealed record SubscriberSummaryDto(
    string PhoneNumber,
    string ClientName,
    long CallCount,
    long TotalBillableSec,
    decimal TotalCharge);
