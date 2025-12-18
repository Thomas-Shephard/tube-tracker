using System.Text.Json.Serialization;

namespace TubeTracker.API.Models.Tfl;

public class TflLine
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("modeName")]
    public string ModeName { get; set; } = string.Empty;

    [JsonPropertyName("lineStatuses")]
    public IReadOnlyList<LineStatus> LineStatuses { get; set; } = [];
}

public class LineStatus
{
    [JsonPropertyName("statusSeverity")]
    public int StatusSeverity { get; set; }

    [JsonPropertyName("statusSeverityDescription")]
    public string StatusSeverityDescription { get; set; } = string.Empty;
}
