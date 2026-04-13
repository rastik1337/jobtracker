using LiteDB;

namespace JobTracker.Models;

public class Label
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#808080";
}