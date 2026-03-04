namespace Lexie.Services;

public class GridSquareService
{
    private const double EarthRadiusKm = 6371.0;

    public (double Lat, double Lon)? LatLonFromGrid(string? grid)
    {
        if (string.IsNullOrEmpty(grid) || grid.Length < 4) return null;
        grid = grid.ToUpperInvariant();
        if (grid[0] < 'A' || grid[0] > 'R' || grid[1] < 'A' || grid[1] > 'R') return null;
        if (!char.IsDigit(grid[2]) || !char.IsDigit(grid[3])) return null;

        double lon = (grid[0] - 'A') * 20.0 - 180.0;
        double lat = (grid[1] - 'A') * 10.0 - 90.0;
        lon += (grid[2] - '0') * 2.0;
        lat += (grid[3] - '0') * 1.0;
        // Center of the grid square
        lon += 1.0;
        lat += 0.5;

        return (lat, lon);
    }

    public string? GridFromLatLon(double lat, double lon)
    {
        lon += 180.0;
        lat += 90.0;
        if (lon < 0 || lon >= 360 || lat < 0 || lat >= 180) return null;
        var lonField = (char)('A' + (int)(lon / 20.0));
        var latField = (char)('A' + (int)(lat / 10.0));
        var lonSquare = (char)('0' + (int)((lon % 20.0) / 2.0));
        var latSquare = (char)('0' + (int)((lat % 10.0) / 1.0));
        return $"{lonField}{latField}{lonSquare}{latSquare}";
    }

    public int? DistanceKm(string? grid1, string? grid2)
    {
        var pos1 = LatLonFromGrid(grid1);
        var pos2 = LatLonFromGrid(grid2);
        if (pos1 == null || pos2 == null) return null;
        return DistanceKm(pos1.Value.Lat, pos1.Value.Lon, pos2.Value.Lat, pos2.Value.Lon);
    }

    public int DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        var rlat1 = lat1 * Math.PI / 180.0;
        var rlat2 = lat2 * Math.PI / 180.0;
        var dLat = rlat2 - rlat1;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(rlat1) * Math.Cos(rlat2) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return (int)Math.Round(EarthRadiusKm * c);
    }
}
