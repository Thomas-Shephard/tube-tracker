using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<string, StationClassificationResult> _cache = new();

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
        if (_cache.TryGetValue(description, out var cached)) return cached;

        try
        {
            _cachedSeverities ??= (await _severityRepository.GetAllAsync()).ToArray();
            StationStatusSeverity? otherSeverity = _cachedSeverities.FirstOrDefault(s => s.Description.Equals("Other", StringComparison.OrdinalIgnoreCase));
            if (otherSeverity is null) throw new InvalidOperationException("Critical: 'Other' category missing.");

            string[] categories = _cachedSeverities.Select(c => c.Description).ToArray();
            
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            string dateString = now.ToString("dddd dd MMMM yyyy HH:mm");

            string prompt = $$"""
                             Current Date/Time: {{dateString}}

                             Classify this London Underground disruption.
                             Input: "{{description}}"

                             Allowed Categories:
                             {{string.Join(", ", categories)}}

                             Rules for "is_future":
                             - "is_future" is ONLY true if the text specifies a future start date or time (e.g., "From Monday", "Starting at 22:00", "Between 25th and 27th").
                             - If the text describes a current state (e.g., "is not available", "is closed", "is faulty"), "is_future" MUST be false.
                             - IMPORTANT: "Planned maintenance" or "Engineering work" that is happening NOW (e.g., "is not available due to planned maintenance") is NOT a future event. Set "is_future" to false.

                             Category Rules:
                             - "Closed" is for full station closures (e.g., "Station Closed").
                             - "Partially Closed" for entrance/exit closures or partial restrictions.
                             - "Accessibility Issue" for lift faults, step-free access unavailable.
                             - "Information" for general advice or minor notes.
                             - "No Disruptions" if the message explicitly says Good Service or no issues.
                             - "Other" for anything else.
                             
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
            var finalResult = new StationClassificationResult 
            { 
                CategoryId = matchedId ?? otherSeverity.SeverityId, 
                IsFuture = classification.IsFuture
            };

            _cache[description] = finalResult;
            return finalResult;
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
