namespace JobTracker.Models;

public class Label
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string ColorHex { get; set; } = "#808080";
}