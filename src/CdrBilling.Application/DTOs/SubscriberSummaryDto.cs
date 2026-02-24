namespace CdrBilling.Application.DTOs;

public sealed record SubscriberSummaryDto(
    string PhoneNumber,
    string ClientName,
    int CallCount,
    int TotalBillableSec,
    decimal TotalCharge);
