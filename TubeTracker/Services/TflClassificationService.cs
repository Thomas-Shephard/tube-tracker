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
    private readonly TimeProvider _timeProvider;

    private StationStatusSeverity[]? _cachedSeverities;

    public TflClassificationService(
        HttpClient httpClient, 
        OllamaSettings settings, 
        IStationStatusSeverityRepository severityRepository, 
        ILogger<TflClassificationService> logger,
        TimeProvider timeProvider)
    {
        _httpClient = httpClient;
        _settings = settings;
        _severityRepository = severityRepository;
        _logger = logger;
        _timeProvider = timeProvider;
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
            
            // Debug check for critical category
            if (!categories.Contains("No Step Free Access", StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Category 'No Step Free Access' is MISSING from database. Model cannot select it.");
            }

            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            string dateString = now.ToString("dddd dd MMMM yyyy HH:mm");

            string prompt = $$"""
                             Current Date/Time: {{dateString}}

                             Classify this London Underground disruption.
                             Input: "{{description}}"

                             Allowed Categories:
                             {{string.Join(", ", categories)}}

                             Rules:
                             - Set "is_future" to true IF the event is planned for later dates/times relative to Current Date/Time.
                             - If happening NOW, "is_future" is false.
                             - "Station Closed" is for full closures only.
                             - If step-free access is unavailable (ANY REASON: staff, lift, etc) -> "No Step Free Access".
                             - "Partially Closed" for entrance/exit issues.
                             - "Information" for advice.
                             
                             Respond with JSON: { "category": "string", "is_future": boolean }
                             """;

            OllamaRequest request = new()
            {
                Model = _settings.ModelName,
                Messages =
                [
                    new OllamaMessage { Role = "system", Content = "Output valid JSON only." },
                    new OllamaMessage { Role = "user", Content = prompt }
                ],
                Format = "json",
                Stream = false
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/chat", request);
            response.EnsureSuccessStatusCode();
            OllamaResponse? result = await response.Content.ReadFromJsonAsync<OllamaResponse>();

            if (result?.Message?.Content is null) return new StationClassificationResult { CategoryId = otherSeverity.SeverityId };

            OllamaClassificationResult? classification = JsonSerializer.Deserialize<OllamaClassificationResult>(result.Message.Content);
            if (classification is null) return new StationClassificationResult { CategoryId = otherSeverity.SeverityId };

            int? matchedId = GetSeverityId(classification.Category);
            return new StationClassificationResult 
            { 
                CategoryId = matchedId ?? otherSeverity.SeverityId, 
                IsFuture = classification.IsFuture
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying disruption: {Description}", description);
            int fallbackId = _cachedSeverities?.FirstOrDefault(s => s.Description.Equals("Other", StringComparison.OrdinalIgnoreCase))?.SeverityId ?? 12;
            return new StationClassificationResult { CategoryId = fallbackId };
        }
    }

    private int? GetSeverityId(string name)
    {
        return _cachedSeverities?.FirstOrDefault(s => s.Description.Equals(name, StringComparison.OrdinalIgnoreCase))?.SeverityId;
    }
}