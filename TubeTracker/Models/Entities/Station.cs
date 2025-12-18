namespace TubeTracker.API.Models.Entities;

public class Station
{
    public int StationId { get; init; }
    public required string TflId { get; set; }
    public required string CommonName { get; set; }
    public double? Lat { get; set; }
    public double? Lon { get; set; }
}
