namespace CdrBilling.Application.DTOs;

public sealed record UploadResult(int RecordsImported, string Message);
