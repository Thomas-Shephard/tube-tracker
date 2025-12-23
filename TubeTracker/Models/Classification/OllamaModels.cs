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
    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; init; }

    [JsonPropertyName("category")]
    public string Category { get; init; } = "Other";

    [JsonPropertyName("is_future")]
    public bool IsFuture { get; init; }

    [JsonPropertyName("priority")]
    public int Priority { get; init; }
}

public class OllamaModelListResponse
{
    [JsonPropertyName("models")]
    public List<OllamaModelInfo> Models { get; set; } = [];
}

public class OllamaModelInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public class OllamaPullRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    
    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;
}