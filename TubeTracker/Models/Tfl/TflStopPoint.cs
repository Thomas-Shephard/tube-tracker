using System.Text.Json.Serialization;

namespace TubeTracker.API.Models.Tfl;

public class TflStopPoint
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("commonName")]
    public string CommonName { get; set; } = string.Empty;

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lon")]
    public double Lon { get; set; }
}
