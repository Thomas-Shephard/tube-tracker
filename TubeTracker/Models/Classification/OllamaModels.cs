using System.Text.Json.Serialization;

namespace TubeTracker.API.Models.Classification;

public class OllamaRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("messages")]
    public required List<OllamaMessage> Messages { get; init; }

    [JsonPropertyName("format")]
    public object? Format { get; init; }

    [JsonPropertyName("stream")]
    public bool Stream { get; init; }
}

public class OllamaMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }
}

public class OllamaResponse
{
    [JsonPropertyName("message")]
    public OllamaMessage? Message { get; init; }
}

public class OllamaClassificationResult
{
    [JsonPropertyName("category")]
    public string Category { get; init; } = "Other";

    [JsonPropertyName("priority")]
    public int Priority { get; init; }

    [JsonPropertyName("isAccessibility")]
    public bool IsAccessibility { get; init; }
}
