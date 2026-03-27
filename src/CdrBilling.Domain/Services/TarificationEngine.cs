using CdrBilling.Domain.Entities;
using CdrBilling.Domain.Enums;

namespace CdrBilling.Domain.Services;

public sealed record TariffMatch(TariffEntry Tariff, decimal Charge);

/// <summary>
/// Pure-domain tariffication logic. No framework dependencies.
/// Builds a prefix trie from tariff entries for O(k) per-call lookup.
/// </summary>
public sealed class TarificationEngine
{
    private readonly PrefixTrie<List<TariffEntry>> _trie = new PrefixTrie<List<TariffEntry>>();

    public TarificationEngine(IReadOnlyList<TariffEntry> tariffs)
    {
        var grouped = tariffs.GroupBy(t => t.Prefix);
        foreach (var g in grouped)
            _trie.Insert(g.Key, [.. g]);
    }

    /// <summary>
    /// Finds the best-matching tariff for a given call record.
    /// Returns null if no applicable tariff exists (call remains unrated).
    /// </summary>
    public TariffMatch? FindBestTariff(CallRecord call)
    {
        // Only answered calls are billed
        if (call.Disposition != Disposition.Answered)
            return null;

        // Internal calls are not billed via tariff table
        if (call.Direction == CallDirection.Internal)
            return null;

        var numberToRate = call.Direction == CallDirection.Outgoing
            ? call.CalledParty
            : call.CallingParty;

        // Strip non-digit characters for prefix matching
        var digits = StripNonDigits(numberToRate);
        if (string.IsNullOrEmpty(digits))
            return null;

        // Collect all tariff lists whose prefix matches a prefix of the number
        var candidateLists = _trie.GetAllPrefixMatches(digits);
        if (candidateLists.Count == 0)
            return null;

        var callDate = DateOnly.FromDateTime(call.StartTime.UtcDateTime);
        var callTime = TimeOnly.FromDateTime(call.StartTime.UtcDateTime);
        var callDow  = ToDayOfWeekMask(call.StartTime.DayOfWeek);

        TariffEntry? best = null;

        foreach (var list in candidateLists)
        {
            foreach (var t in list)
            {
                if (t.EffectiveDate > callDate) continue;
                if (t.ExpiryDate.HasValue && t.ExpiryDate.Value < callDate) continue;
                if ((t.WeekdayMask & callDow) == 0) continue;
                if (!IsInTimeband(callTime, t.TimebandStart, t.TimebandEnd)) continue;

                if (best is null
                    || t.Prefix.Length > best.Prefix.Length
                    || (t.Prefix.Length == best.Prefix.Length && t.Priority > best.Priority))
                {
                    best = t;
                }
            }
        }

        if (best is null)
            return null;

        var charge = best.ConnectionFee + (call.BillableSec / 60m) * best.RatePerMin;
        charge = Math.Round(charge, 4, MidpointRounding.AwayFromZero);

        return new TariffMatch(best, charge);
    }

    private static bool IsInTimeband(TimeOnly t, TimeOnly start, TimeOnly end)
    {
        // Same-day band: 08:00-20:00
        if (start <= end)
            return t >= start && t <= end;

        // Overnight band: e.g. 20:00-08:00
        return t >= start || t <= end;
    }

    private static DayOfWeekMask ToDayOfWeekMask(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday    => DayOfWeekMask.Monday,
        DayOfWeek.Tuesday   => DayOfWeekMask.Tuesday,
        DayOfWeek.Wednesday => DayOfWeekMask.Wednesday,
        DayOfWeek.Thursday  => DayOfWeekMask.Thursday,
        DayOfWeek.Friday    => DayOfWeekMask.Friday,
        DayOfWeek.Saturday  => DayOfWeekMask.Saturday,
        _                   => DayOfWeekMask.Sunday
    };

    private static string StripNonDigits(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // Most phone numbers start with '+' — normalize to digits only
        Span<char> buf = stackalloc char[input.Length];
        var len = 0;
        foreach (var ch in input)
        {
            if (char.IsAsciiDigit(ch))
                buf[len++] = ch;
        }
        return new string(buf[..len]);
    }
}
