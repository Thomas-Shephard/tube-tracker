using System.Text.Json.Serialization;

namespace TubeTracker.API.Models.Tfl;

public class TflStationDisruption
{
    [JsonPropertyName("atcoCode")]
    public string AtcoCode { get; set; } = string.Empty;

    [JsonPropertyName("commonName")]
    public string CommonName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    // Sometimes the ID is in stationAtcoCode
    [JsonPropertyName("stationAtcoCode")]
    public string StationAtcoCode { get; set; } = string.Empty;
}
