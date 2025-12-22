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
            if (otherSeverity is null) throw new InvalidOperationException("Critical: 'Other' category missing.");

            string[] categories = _cachedSeverities.Select(c => c.Description).ToArray();

            // Simplified Prompt - Text Only, No JSON
            string prompt = $"""
                             Classify this London Underground disruption.
                             Input: "{description}"

                             Allowed Categories:
                             {string.Join("\n", categories.Select(c => $"- {c}"))}

                             Rules:
                             - "Station Closed" is ONLY for total station closure.
                             - If only an ENTRANCE or EXIT is closed -> "Partially Closed"
                             - If step-free access is unavailable -> "No Step Free Access"
                             - If advising on travel (e.g. "front coaches") -> "Information"
                             - If a lift is broken -> "Lift Fault"
                             - If an escalator is broken -> "Escalator Fault"
                             
                             Reply ONLY with the exact Category Name. No other text.
                             """;

            OllamaRequest request = new()
            {
                Model = _settings.ModelName,
                Messages =
                [
                    new OllamaMessage { Role = "user", Content = prompt }
                ],
                Stream = false
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/chat", request);
            response.EnsureSuccessStatusCode();

            OllamaResponse? result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            string? rawContent = result?.Message?.Content?.Trim();

            _logger.LogInformation("Ollama Classification: '{Input}' -> '{Output}'", description, rawContent);

            if (string.IsNullOrEmpty(rawContent)) return new StationClassificationResult { CategoryId = otherSeverity.SeverityId };

            string cleanContent = rawContent.Replace(".", "").Replace("\"", "").Trim();

            StationStatusSeverity? matchedSeverity = _cachedSeverities
                .FirstOrDefault(c => cleanContent.Equals(c.Description, StringComparison.OrdinalIgnoreCase));
            
            if (matchedSeverity is null)
            {
                matchedSeverity = _cachedSeverities
                    .FirstOrDefault(c => cleanContent.Contains(c.Description, StringComparison.OrdinalIgnoreCase));
            }

            return matchedSeverity is not null 
                ? new StationClassificationResult { CategoryId = matchedSeverity.SeverityId } 
                : new StationClassificationResult { CategoryId = otherSeverity.SeverityId };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying disruption: {Description}", description);
            int fallbackId = _cachedSeverities?.FirstOrDefault(s => s.Description.Equals("Other", StringComparison.OrdinalIgnoreCase))?.SeverityId ?? 12;
            return new StationClassificationResult { CategoryId = fallbackId };
        }
    }
}
