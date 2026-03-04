namespace Lexie.Services;

public class DxccLookupService
{
    private readonly GridSquareService _grid;

    private record DxccEntity(string Name, double Lat, double Lon);

    private readonly Dictionary<string, DxccEntity> _exactMatch = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DxccEntity> _entities = new(StringComparer.OrdinalIgnoreCase);
    private (string Prefix, DxccEntity Entity)[] _prefixTable = [];

    public List<string> LoadResults { get; } = [];

    public DxccLookupService(GridSquareService grid)
    {
        _grid = grid;
    }

    public void LoadCtyDat()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new List<string>
        {
            Path.Combine(localAppData, "WSJT-X", "cty.dat"),
            Path.Combine(localAppData, "decodium", "cty.dat"),
        };

        // Linux system install fallback
        candidates.Add("/usr/share/wsjtx/cty.dat");

        // Bundled fallback (next to executable)
        var exeDir = AppContext.BaseDirectory;
        candidates.Add(Path.Combine(exeDir, "Data", "cty.dat"));
        candidates.Add(Path.Combine(exeDir, "cty.dat"));

        // Find the most recently modified file
        string? path = null;
        DateTime newest = DateTime.MinValue;
        foreach (var c in candidates)
        {
            if (File.Exists(c))
            {
                var mod = File.GetLastWriteTimeUtc(c);
                if (mod > newest)
                {
                    newest = mod;
                    path = c;
                }
            }
        }

        if (path == null)
        {
            LoadResults.Add("No cty.dat found — entity lookup disabled");
            return;
        }

        ParseCtyDat(path);
        LoadResults.Add($"Loaded {_entities.Count} entities, {_prefixTable.Length} prefixes from {path}");
    }

    private void ParseCtyDat(string path)
    {
        var prefixes = new List<(string Prefix, DxccEntity Entity)>();
        DxccEntity? currentEntity = null;

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (!char.IsWhiteSpace(line[0]))
            {
                // Entity header line
                // Format: "Entity Name:  CQ:  ITU:  Cont:  Lat:  Lon:  UTC:  Prefix:"
                var parts = line.Split(':');
                if (parts.Length >= 8)
                {
                    var name = parts[0].Trim();
                    if (double.TryParse(parts[4].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var lat) &&
                        double.TryParse(parts[5].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var lonWest))
                    {
                        // CTY.DAT uses west-positive longitude — negate for standard convention
                        var lon = -lonWest;
                        currentEntity = new DxccEntity(name, lat, lon);
                        _entities[name] = currentEntity;

                        // Primary prefix (strip leading * for reinstated entities)
                        var primaryPrefix = parts[7].Trim().TrimStart('*');
                        if (!string.IsNullOrEmpty(primaryPrefix) && !primaryPrefix.Contains('/'))
                            prefixes.Add((primaryPrefix, currentEntity));
                    }
                }
            }
            else if (currentEntity != null)
            {
                // Prefix line (starts with whitespace)
                var trimmed = line.Trim().TrimEnd(';');
                foreach (var entry in trimmed.Split(','))
                {
                    var raw = entry.Trim();
                    if (string.IsNullOrEmpty(raw)) continue;

                    // Strip zone/entity overrides: (CQ) [ITU] {DXCC#}
                    var prefix = StripModifiers(raw);

                    if (prefix.StartsWith('='))
                    {
                        // Exact callsign match
                        var callsign = prefix[1..];
                        if (!string.IsNullOrEmpty(callsign))
                            _exactMatch[callsign] = currentEntity;
                    }
                    else if (!string.IsNullOrEmpty(prefix) && !prefix.Contains('/'))
                    {
                        prefixes.Add((prefix, currentEntity));
                    }
                }
            }
        }

        // Sort by prefix length descending for longest-match-first
        _prefixTable = prefixes
            .OrderByDescending(p => p.Prefix.Length)
            .ThenBy(p => p.Prefix, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string StripModifiers(string prefix)
    {
        // Remove (CQ), [ITU], {DXCC#} zone/entity overrides
        var sb = new System.Text.StringBuilder(prefix.Length);
        var depth = 0;
        foreach (var c in prefix)
        {
            if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}') depth--;
            else if (depth == 0) sb.Append(c);
        }
        return sb.ToString();
    }

    public string? Lookup(string callsign) => FindEntity(callsign)?.Name;

    /// <summary>
    /// Finds the entity for a callsign and returns both name and distance in one call.
    /// </summary>
    public (string Name, int DistanceKm)? LookupWithDistance(string callsign, string? myGrid)
    {
        var entity = FindEntity(callsign);
        if (entity == null) return null;
        if (string.IsNullOrEmpty(myGrid)) return null;
        var myPos = _grid.LatLonFromGrid(myGrid);
        if (myPos == null) return null;
        var dist = _grid.DistanceKm(myPos.Value.Lat, myPos.Value.Lon, entity.Lat, entity.Lon);
        return (entity.Name, dist);
    }

    /// <summary>
    /// Returns the grid square for the center of a DXCC entity, or null if unknown.
    /// </summary>
    public string? GetEntityGrid(string entityName)
    {
        if (_entities.TryGetValue(entityName, out var entity))
            return _grid.GridFromLatLon(entity.Lat, entity.Lon);
        return null;
    }

    private DxccEntity? FindEntity(string callsign)
    {
        if (string.IsNullOrEmpty(callsign)) return null;
        if (_exactMatch.TryGetValue(callsign, out var entity)) return entity;
        foreach (var (prefix, ent) in _prefixTable)
        {
            if (prefix.Length > callsign.Length) continue;
            if (callsign.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return ent;
        }
        return null;
    }
}
