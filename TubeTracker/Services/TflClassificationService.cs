using System.Text.Json;
using TubeTracker.API.Models.Classification;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;
using TubeTracker.API.Settings;

namespace TubeTracker.API.Services;

public class TflClassificationService : ITflClassificationService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaSettings _settings;
    private readonly IStationStatusSeverityRepository _severityRepository;
    private readonly ILogger<TflClassificationService> _logger;

    private StationStatusSeverity[]? _cachedSeverities;

    public TflClassificationService(
        HttpClient httpClient, 
        OllamaSettings settings, 
        IStationStatusSeverityRepository severityRepository, 
        ILogger<TflClassificationService> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _severityRepository = severityRepository;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<StationClassificationResult> ClassifyStationDisruptionAsync(string description)
    {
        try
        {
            _cachedSeverities ??= (await _severityRepository.GetAllAsync()).ToArray();
            
            StationStatusSeverity? otherSeverity = _cachedSeverities.FirstOrDefault(s => s.Description.Equals("Other", StringComparison.OrdinalIgnoreCase));

            if (otherSeverity is null)
            {
                throw new InvalidOperationException("Critical configuration missing: 'Other' severity category not found in database.");
            }

            StationClassificationResult fallbackResult = new() { CategoryId = otherSeverity.SeverityId };

            string[] categories = _cachedSeverities.Select(c => c.Description).ToArray();

            string prompt = $"""
                             Classify the following London Underground disruption into exactly one of these categories:
                             [{string.Join(", ", categories)}]

                             Input: "{description}"

                             Guidelines:
                             1. "Station Closed" is ONLY for when the ENTIRE station is closed to the public.
                             2. If a lift is broken, use "Lift Fault".
                             3. If an escalator is broken, use "Escalator Fault".
                             4. If the station is open but entrance/exit is restricted, use "Station Closed" or "Other" depending on severity, but NEVER if it's just a lift/escalator issue.
                             5. "Signal Failure" and "Train Fault" are for train services, not station assets.

                             Examples:
                             - "Covent Garden: Station closed due to overcrowding." -> "Station Closed"
                             - "Westminster: No step-free access due to lift fault." -> "Lift Fault"
                             - "Victoria: Escalator 4 is out of service." -> "Escalator Fault"
                             - "Baker Street: Severe delays due to signal failure." -> "Signal Failure"
                             
                             Respond with JSON only.
                             """;

            OllamaRequest request = new()
            {
                Model = _settings.ModelName,
                Messages =
                [
                    new OllamaMessage { Role = "system", Content = "You are a precise classification engine. Output only valid JSON." },
                    new OllamaMessage { Role = "user", Content = prompt }
                ],
                Format = new
                {
                    type = "object",
                    properties = new
                    {
                        category = new
                        {
                            type = "string",
                            @enum = categories
                        }
                    },
                    required = new[] { "category" }
                },
                Stream = false
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/chat", request);
            response.EnsureSuccessStatusCode();

            OllamaResponse? result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            
            if (result?.Message?.Content is null)
            {
                _logger.LogWarning("Ollama returned null content for description: {Description}", description);
                return fallbackResult;
            }

            OllamaClassificationResult? classification = JsonSerializer.Deserialize<OllamaClassificationResult>(result.Message.Content);
            
            if (classification is null)
            {
                _logger.LogWarning("Failed to deserialize Ollama response: {Response}", result.Message.Content);
                return fallbackResult;
            }

            StationStatusSeverity? matchedSeverity = _cachedSeverities.FirstOrDefault(c => c.Description.Equals(classification.Category, StringComparison.OrdinalIgnoreCase));
            
            return matchedSeverity is not null 
                ? new StationClassificationResult { CategoryId = matchedSeverity.SeverityId } 
                : fallbackResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying disruption: {Description}", description);

            StationStatusSeverity? other = _cachedSeverities?.FirstOrDefault(s => s.Description.Equals("Other", StringComparison.OrdinalIgnoreCase));
            if (other is null)
            {
                throw;
            }

            return new StationClassificationResult { CategoryId = other.SeverityId };
        }
    }
}
