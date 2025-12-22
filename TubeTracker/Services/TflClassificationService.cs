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
                             - **RECURRING EVENTS (e.g., "each evening", "Mon-Fri 23:00 to 05:00"):** 
                               - If Current Time is INSIDE an active window -> "is_future": false, "valid_until": <end of THIS specific window>.
                               - If Current Time is OUTSIDE an active window (e.g., it's morning, event starts tonight) -> "is_future": true, "valid_from": <start of NEXT specific window>.
                             - If "is_future" is true, extract the specific start date/time as "valid_from" (ISO 8601 format). If ambiguous or not specified, leave null.
                             - If happening NOW, "is_future" is false.
                             - If happening NOW, try to extract when this specific active period ENDS as "valid_until" (ISO 8601). E.g., if "23:10 each evening", and it's 23:15, valid_until is when service resumes (usually 04:30 next day) or when the text says it ends.
                             - "Station Closed" is for full closures only.
                             - If step-free access is unavailable (ANY REASON: staff, lift, etc) -> "No Step Free Access".
                             - "Partially Closed" for entrance/exit issues.
                             - "Information" for advice.
                             
                             Respond with JSON: { "category": "string", "is_future": boolean, "valid_from": "string (ISO 8601) or null", "valid_until": "string (ISO 8601) or null" }
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
                IsFuture = classification.IsFuture,
                ValidFrom = classification.ValidFrom,
                ValidUntil = classification.ValidUntil
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