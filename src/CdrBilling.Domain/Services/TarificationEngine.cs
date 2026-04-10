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
    private readonly List<List<TariffEntry>> _candidateLists = new(8);

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
    public TariffMatch? FindBestTariff(TarificationCall call)
    {
        // Only answered outgoing calls are billed.
        if (call.Disposition != Disposition.Answered || call.Direction != CallDirection.Outgoing)
            return null;

        if (string.IsNullOrEmpty(call.DigitsToRate))
            return null;

        // Collect all tariff lists whose prefix matches a prefix of the number
        _trie.CollectPrefixMatches(call.DigitsToRate, _candidateLists);
        if (_candidateLists.Count == 0)
            return null;

        var callDate = DateOnly.FromDateTime(call.StartTime.UtcDateTime);
        var callTime = TimeOnly.FromDateTime(call.StartTime.UtcDateTime);
        var callDow  = ToDayOfWeekMask(call.StartTime.DayOfWeek);

        TariffEntry? best = null;

        foreach (var list in _candidateLists)
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

        var roundedBillableMinutes = (int)Math.Ceiling(Math.Max(call.BillableSec, 0) / 60m);
        var charge = best.ConnectionFee + roundedBillableMinutes * best.RatePerMin;
        charge = Math.Round(charge, 2, MidpointRounding.AwayFromZero);

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
}
