namespace TubeTracker.API.Models.Entities;

public class Line
{
    public int LineId { get; init; }
    public required string TflId { get; init; }
    public required string Name { get; set; }
    public required string ModeName { get; set; }
    public string? Colour { get; set; }
}
