using System.Text;
using CdrBilling.Application.UseCases;
using CdrBilling.Domain.Entities;

namespace CdrBilling.Infrastructure.Parsing;

/// <summary>
/// Parses subscriber CSV files (semicolon-delimited).
/// Format: phone_number;client_name
/// </summary>
public sealed class SubscriberFileParser : ISubscriberFileParser
{
    public IEnumerable<Subscriber> Parse(Stream stream, Guid sessionId)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        bool first = true;

        while (reader.ReadLine() is { } line)
        {
            if (first) { first = false; continue; }
            line = line.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(';', 2);
            if (parts.Length < 2) continue;

            var phone = parts[0].Trim();
            var name  = parts[1].Trim();
            if (string.IsNullOrEmpty(phone)) continue;

            yield return Subscriber.Create(sessionId, phone, name);
        }
    }
}
