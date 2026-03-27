using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using CdrBilling.Application.UseCases;
using CdrBilling.Domain.Entities;
using CdrBilling.Domain.Enums;

namespace CdrBilling.Infrastructure.Parsing;

/// <summary>
/// Streams CDR records from a pipe-delimited text file using System.IO.Pipelines.
/// Zero-copy buffer management — suitable for very large files.
/// Format: StartTime|EndTime|CallingParty|CalledParty|CallDirection|Disposition|Duration|BillableSec|Charge|AccountCode|CallID|TrunkName
/// </summary>
public sealed class CdrFileParser : ICdrFileParser
{
    public async IAsyncEnumerable<CallRecord> ParseAsync(
        Stream stream,
        Guid sessionId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var pipeReader = PipeReader.Create(stream, new StreamPipeReaderOptions(bufferSize: 65536));
        try
        {
            while (true)
            {
                var result = await pipeReader.ReadAsync(ct);
                var buffer = result.Buffer;

                while (TryReadLine(ref buffer, result.IsCompleted, out var line))
                {
                    var record = TryParseLine(line, sessionId);
                    if (record is not null)
                        yield return record;
                }

                pipeReader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted) break;
            }
        }
        finally
        {
            await pipeReader.CompleteAsync();
        }
    }

    private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, bool isCompleted, out ReadOnlySequence<byte> line)
    {
        var reader = new SequenceReader<byte>(buffer);
        if (reader.TryReadTo(out line, (byte)'\n'))
        {
            buffer = buffer.Slice(reader.Position);
            return true;
        }
        // Handle file with no trailing newline: only return remaining bytes as the last line
        // when the pipe is fully completed; otherwise wait for more data to arrive.
        if (isCompleted && !buffer.IsEmpty)
        {
            line = buffer;
            buffer = buffer.Slice(buffer.End);
            return true;
        }
        line = default;
        return false;
    }

    private static CallRecord? TryParseLine(ReadOnlySequence<byte> lineSeq, Guid sessionId)
    {
        try
        {
            // Decode to string — acceptable cost vs. Span-based parsing for correctness
            var text = Encoding.UTF8.GetString(lineSeq).TrimEnd('\r', '\n').Trim();
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (IsHeader(text)) return null;

            var parts = text.Split('|');
            if (parts.Length < 12) return null;

            var startTime  = DateTimeOffset.Parse(parts[0].Trim());
            var endTime    = DateTimeOffset.Parse(parts[1].Trim());
            var calling    = parts[2].Trim();
            var called     = parts[3].Trim();
            var direction  = ParseDirection(parts[4].Trim());
            var disposition = ParseDisposition(parts[5].Trim());
            var duration   = int.Parse(parts[6].Trim());
            var billable   = int.Parse(parts[7].Trim());
            decimal? charge = string.IsNullOrWhiteSpace(parts[8]) ? null : decimal.Parse(parts[8].Trim(), System.Globalization.CultureInfo.InvariantCulture);
            var account    = parts[9].Trim() is { Length: > 0 } a ? a : null;
            var callId     = parts[10].Trim();
            var trunk      = parts[11].Trim() is { Length: > 0 } t ? t : null;

            return CallRecord.Create(sessionId, startTime, endTime, calling, called,
                direction, disposition, duration, billable, charge, account, callId, trunk);
        }
        catch
        {
            // Skip malformed lines
            return null;
        }
    }

    private static CallDirection ParseDirection(string value) => value.ToLowerInvariant() switch
    {
        "outgoing" => CallDirection.Outgoing,
        "internal" => CallDirection.Internal,
        _          => CallDirection.Incoming
    };

    private static Disposition ParseDisposition(string value) => value.ToLowerInvariant() switch
    {
        "answered"  => Disposition.Answered,
        "busy"      => Disposition.Busy,
        "no_answer" => Disposition.NoAnswer,
        _           => Disposition.Failed
    };

    private static bool IsHeader(string line)
        => line.StartsWith("StartTime|", StringComparison.OrdinalIgnoreCase);
}
