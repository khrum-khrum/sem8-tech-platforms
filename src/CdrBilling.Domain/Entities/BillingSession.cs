using CdrBilling.Domain.Enums;

namespace CdrBilling.Domain.Entities;

public sealed class BillingSession
{
    public Guid Id { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public SessionStatus Status { get; private set; }
    public int TotalRecords { get; private set; }
    public int ProcessedRecords { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private BillingSession() { }

    public static BillingSession Create(Guid id) => new()
    {
        Id = id,
        CreatedAt = DateTimeOffset.UtcNow,
        Status = SessionStatus.Pending
    };

    public void SetRunning(int totalRecords)
    {
        Status = SessionStatus.Running;
        TotalRecords = totalRecords;
        ProcessedRecords = 0;
    }

    public void UpdateProgress(int processed)
    {
        ProcessedRecords = processed;
    }

    public void MarkCompleted()
    {
        Status = SessionStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        ProcessedRecords = TotalRecords;
    }

    public void MarkFailed(string error)
    {
        Status = SessionStatus.Failed;
        ErrorMessage = error;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
