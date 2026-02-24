using System.Globalization;
using System.Text;
using CdrBilling.Application.UseCases;
using CdrBilling.Domain.Entities;
using CdrBilling.Domain.Enums;

namespace CdrBilling.Infrastructure.Parsing;

/// <summary>
/// Parses tariff CSV files (semicolon-delimited).
/// Format: prefix;destination;rate_per_min;connection_fee;timeband;weekday;priority;effective_date;expiry_date
/// </summary>
public sealed class TariffFileParser : ITariffFileParser
{
    public IEnumerable<TariffEntry> Parse(Stream stream, Guid sessionId)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        bool first = true;

        while (reader.ReadLine() is { } line)
        {
            if (first) { first = false; continue; } // skip header
            line = line.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var entry = TryParseLine(line, sessionId);
            if (entry is not null)
                yield return entry;
        }
    }

    private static TariffEntry? TryParseLine(string line, Guid sessionId)
    {
        try
        {
            var parts = line.Split(';');
            if (parts.Length < 9) return null;

            var prefix          = parts[0].Trim();
            var destination     = parts[1].Trim();
            var ratePerMin      = decimal.Parse(parts[2].Trim(), CultureInfo.InvariantCulture);
            var connectionFee   = decimal.Parse(parts[3].Trim(), CultureInfo.InvariantCulture);
            var (tbStart, tbEnd) = ParseTimeband(parts[4].Trim());
            var weekdayMask     = ParseWeekday(parts[5].Trim());
            var priority        = int.Parse(parts[6].Trim());
            var effectiveDate   = DateOnly.Parse(parts[7].Trim());
            DateOnly? expiryDate = string.IsNullOrWhiteSpace(parts[8])
                ? null
                : DateOnly.Parse(parts[8].Trim());

            return TariffEntry.Create(
                sessionId, prefix, destination, ratePerMin, connectionFee,
                tbStart, tbEnd, weekdayMask, priority, effectiveDate, expiryDate);
        }
        catch
        {
            return null;
        }
    }

    private static (TimeOnly Start, TimeOnly End) ParseTimeband(string value)
    {
        // Format: "08:00-20:00"
        var dash = value.IndexOf('-');
        if (dash < 0) return (TimeOnly.MinValue, TimeOnly.MaxValue);

        var start = TimeOnly.Parse(value[..dash].Trim());
        var end   = TimeOnly.Parse(value[(dash + 1)..].Trim());
        return (start, end);
    }

    internal static DayOfWeekMask ParseWeekday(string value)
    {
        value = value.Trim();
        if (string.IsNullOrWhiteSpace(value)) return DayOfWeekMask.All;

        // Range notation: "1-5"
        if (value.Contains('-') && !value.Contains(','))
        {
            var dash = value.IndexOf('-');
            var start = int.Parse(value[..dash].Trim());
            var end   = int.Parse(value[(dash + 1)..].Trim());
            var mask  = DayOfWeekMask.None;
            for (int i = start; i <= end; i++)
                mask |= IsoWeekdayToMask(i);
            return mask;
        }

        // Comma-separated: "1,3,5"
        if (value.Contains(','))
        {
            var mask = DayOfWeekMask.None;
            foreach (var part in value.Split(','))
                if (int.TryParse(part.Trim(), out var d))
                    mask |= IsoWeekdayToMask(d);
            return mask;
        }

        // Single value
        return int.TryParse(value, out var day) ? IsoWeekdayToMask(day) : DayOfWeekMask.All;
    }

    private static DayOfWeekMask IsoWeekdayToMask(int iso) => iso switch
    {
        1 => DayOfWeekMask.Monday,
        2 => DayOfWeekMask.Tuesday,
        3 => DayOfWeekMask.Wednesday,
        4 => DayOfWeekMask.Thursday,
        5 => DayOfWeekMask.Friday,
        6 => DayOfWeekMask.Saturday,
        7 => DayOfWeekMask.Sunday,
        _ => DayOfWeekMask.None
    };
}
