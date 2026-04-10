namespace CdrBilling.Application.Options;

public sealed class TarificationOptions
{
    public const string SectionName = "Tarification";

    public int BatchSize { get; set; } = 200_000;
}
