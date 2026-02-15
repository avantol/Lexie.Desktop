using System.Text.RegularExpressions;

namespace Lexie.Services;

public partial class CallsignExtractor
{
    [GeneratedRegex(@"\b(\d?[A-Z]{1,2}\d{1,4}[A-Z]{1,4})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CallsignPattern();

    [GeneratedRegex(@"^[A-R]{2}\d{2}$")]
    private static partial Regex GridSquarePattern();

    private static readonly HashSet<string> SkipTokens =
        ["CQ", "DE", "QRZ", "RR73", "RRR", "R73"];

    public List<string> Extract(string? message)
    {
        if (string.IsNullOrEmpty(message)) return [];

        var matches = CallsignPattern().Matches(message);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (Match match in matches)
        {
            var call = match.Groups[1].Value.ToUpperInvariant();

            // Skip grid squares (e.g., FN42, EN50)
            if (GridSquarePattern().IsMatch(call)) continue;
            // Skip common tokens
            if (SkipTokens.Contains(call)) continue;

            if (seen.Add(call))
                result.Add(call);
        }

        // Message format is "<to> <from> <payload>" — return only the "from" (second) callsign
        if (result.Count >= 2) return [result[1]];
        // CQ calls: "CQ <from> <grid>" — only one callsign found, that's the sender
        return result.Count > 0 ? [result[0]] : [];
    }
}
