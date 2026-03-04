namespace Lexie.Models;

public class CountryInfo
{
    public required string Name { get; set; }
    public int Count { get; set; }
    public List<string> Callsigns { get; } = [];
    public DateTime LastSeen { get; set; }
}
