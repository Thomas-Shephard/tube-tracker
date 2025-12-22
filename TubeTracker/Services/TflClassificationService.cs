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
            // This is often more reliable for 4B models than strict JSON schemas
            string prompt = $"""
                             Classify this London Underground disruption.
                             Input: "{description}"

                             Allowed Categories:
                             {string.Join("\n", categories.Select(c => $"- {c}"))}

                             Rules:
                             - If the station is FULLY closed -> \"Station Closed\"
                             - If a lift is broken -> \"Lift Fault\"
                             - If an escalator is broken -> \"Escalator Fault\"
                             - If advising on travel (e.g. "front coaches", "ticket hall") -> "Information"
                             - If generic restriction (e.g. "exit only", "partially closed") -> "Other"
                             - "Signal Failure" and "Train Fault" are for train services, not station assets.
                             
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

            // Log raw response to help debug "Station Closed" bias
            _logger.LogInformation("Ollama Classification: '{Input}' -> '{Output}'", description, rawContent);

            if (string.IsNullOrEmpty(rawContent)) return new StationClassificationResult { CategoryId = otherSeverity.SeverityId };

            // Clean up response (remove dots, quotes)
            string cleanContent = rawContent.Replace(".", "").Replace("\"", "").Trim();

            // Fuzzy match the category
            StationStatusSeverity? matchedSeverity = _cachedSeverities
                .FirstOrDefault(c => cleanContent.Equals(c.Description, StringComparison.OrdinalIgnoreCase));
            
            // Try Contains if exact match failed (e.g. "Category: Lift Fault")
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
            // Fallback
            int fallbackId = _cachedSeverities?.FirstOrDefault(s => s.Description.Equals("Other", StringComparison.OrdinalIgnoreCase))?.SeverityId ?? 12;
            return new StationClassificationResult { CategoryId = fallbackId };
        }
    }
}
