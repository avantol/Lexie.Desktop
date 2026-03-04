using System.Text.RegularExpressions;

namespace Lexie.Services;

public partial class AdiLogService
{
    [GeneratedRegex(@"<CALL:(\d+)>([^<\s]+)", RegexOptions.IgnoreCase)]
    private static partial Regex CallFieldPattern();

    [GeneratedRegex(@"<BAND:(\d+)>([^<\s]+)", RegexOptions.IgnoreCase)]
    private static partial Regex BandFieldPattern();

    private readonly DxccLookupService _dxcc;
    private readonly HashSet<string> _workedCountries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _workedCountriesByBand = new(StringComparer.OrdinalIgnoreCase);

    public AdiLogService(DxccLookupService dxcc)
    {
        _dxcc = dxcc;
    }

    public int WorkedCountryCount => _workedCountries.Count;
    public List<string> LoadResults { get; } = [];

    public bool IsNewCountry(string country)
    {
        return !_workedCountries.Contains(country);
    }

    public bool IsNewOnBand(string country, string band)
    {
        if (string.IsNullOrEmpty(band)) return false;
        if (!_workedCountriesByBand.TryGetValue(band, out var countries))
            return true;
        return !countries.Contains(country);
    }

    public void LoadLogs()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var paths = new[]
        {
            Path.Combine(localAppData, "decodium", "decodium_log.adi"),
            Path.Combine(localAppData, "WSJT-X", "wsjtx_log.adi"),
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                var count = ParseAdiFile(path);
                LoadResults.Add($"Loaded {count:N0} QSOs from {Path.GetFileName(path)}");
            }
        }
        LoadResults.Add($"{_workedCountries.Count} countries across {_workedCountriesByBand.Count} bands");
    }

    private int ParseAdiFile(string path)
    {
        using var reader = new StreamReader(path);
        var inHeader = true;
        var recordCount = 0;
        string? callsign = null;
        string? band = null;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (inHeader)
            {
                if (line.Contains("<eoh>", StringComparison.OrdinalIgnoreCase))
                    inHeader = false;
                continue;
            }

            // Look for CALL field
            var callMatch = CallFieldPattern().Match(line);
            if (callMatch.Success)
            {
                var length = int.Parse(callMatch.Groups[1].Value);
                var raw = callMatch.Groups[2].Value;
                callsign = raw.Length > length ? raw[..length] : raw;
            }

            // Look for BAND field
            var bandMatch = BandFieldPattern().Match(line);
            if (bandMatch.Success)
            {
                var length = int.Parse(bandMatch.Groups[1].Value);
                var raw = bandMatch.Groups[2].Value;
                band = raw.Length > length ? raw[..length] : raw;
            }

            // End of record
            if (line.Contains("<eor>", StringComparison.OrdinalIgnoreCase))
            {
                recordCount++;
                if (callsign != null)
                {
                    var country = _dxcc.Lookup(callsign);
                    if (country != null)
                    {
                        _workedCountries.Add(country);

                        if (band != null)
                        {
                            var normalizedBand = band.ToUpperInvariant();
                            if (!_workedCountriesByBand.TryGetValue(normalizedBand, out var countries))
                            {
                                countries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                _workedCountriesByBand[normalizedBand] = countries;
                            }
                            countries.Add(country);
                        }
                    }
                }
                callsign = null;
                band = null;
            }
        }
        return recordCount;
    }
}
