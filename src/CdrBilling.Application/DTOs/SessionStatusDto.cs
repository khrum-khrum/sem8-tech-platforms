namespace CdrBilling.Application.DTOs;

public sealed record SessionStatusDto(
    Guid Id,
    string Status,
    int TotalRecords,
    int ProcessedRecords,
    double ProgressPercent,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);
