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
    private readonly IOllamaStatusService _statusService;
    private readonly ILogger<TflClassificationService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, StationClassificationResult> _cache = new();

    private StationStatusSeverity[]? _cachedSeverities;

    public TflClassificationService(
        HttpClient httpClient, 
        OllamaSettings settings, 
        IStationStatusSeverityRepository severityRepository, 
        IOllamaStatusService statusService,
        ILogger<TflClassificationService> logger,
        TimeProvider timeProvider)
    {
        _httpClient = httpClient;
        _settings = settings;
        _severityRepository = severityRepository;
        _statusService = statusService;
        _logger = logger;
        _timeProvider = timeProvider;
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<StationClassificationResult> ClassifyStationDisruptionAsync(string description)
    {
        if (_cache.TryGetValue(description, out StationClassificationResult? cached)) return cached;

        try
        {
            await _statusService.WaitUntilReadyAsync(CancellationToken.None);

            _cachedSeverities ??= (await _severityRepository.GetAllAsync()).ToArray();
            StationStatusSeverity? otherSeverity = _cachedSeverities.FirstOrDefault(s => s.Description.Equals("Other", StringComparison.OrdinalIgnoreCase));
            if (otherSeverity is null) throw new InvalidOperationException("Critical: 'Other' category missing.");

            string[] categories = _cachedSeverities.Select(c => c.Description).ToArray();
            
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            string dateString = now.ToString("dddd dd MMMM yyyy HH:mm");

            string systemPrompt = $"""
                                  STATUS DEFINITIONS:
                                  - "ActiveNow": The disruption is happening RIGHT NOW at {dateString}. This includes continuous closures (e.g., "until further notice", "until 2026") or if the current time falls within a specific stated window (e.g., it is 22:00 and the disruption is "from 21:00 to 05:00").
                                  - "StartingLater": The NEXT scheduled occurrence of this disruption starts in the future relative to {dateString}. 
                                  
                                  CRITICAL LOGIC:
                                  1. TIMED CLOSURES: If a disruption happens "each evening" or "after [Time]", you MUST compare [Time] with the current time ({dateString}).
                                  2. If the current time is BEFORE the start time today, it is "StartingLater".
                                  3. If the current time is AFTER the start time but BEFORE the end time (usually next morning), it is "ActiveNow".
                                  4. If the disruption is for specific dates (e.g., "Monday 22 and Tuesday 23") and today is Tuesday but before the evening start time, it is "StartingLater".
                                  
                                  EXAMPLES:
                                  - "Mon 22 and Tue 23, after 23:10 each evening" (Current time: Tue 15:00) -> status: StartingLater.
                                  - "Station closed until spring 2026" -> status: ActiveNow.
                                  - "Today after 21:00, station closed" (Current time: 22:00) -> status: ActiveNow.
                                  
                                  OUTPUT INSTRUCTIONS:
                                  - Respond ONLY with a valid JSON object.
                                  - No preamble, no explanation outside the JSON.
                                  - JSON MUST contain "category", "status", and "reasoning" keys.
                                  """;

            string userPrompt = $$"""
                                Classify this disruption: "{{description}}"
                                Allowed Categories: {{string.Join(", ", categories)}}
                                Expected JSON format: { "category": "string", "status": "ActiveNow|StartingLater", "reasoning": "string" }
                                """;

            OllamaRequest request = new()
            {
                Model = _settings.ModelName,
                Messages =
                [
                    new OllamaMessage { Role = "system", Content = systemPrompt },
                    new OllamaMessage { Role = "user", Content = userPrompt }
                ],
                Format = "json",
                Stream = false
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/chat", request);
            response.EnsureSuccessStatusCode();
            OllamaResponse? result = await response.Content.ReadFromJsonAsync<OllamaResponse>();

            if (string.IsNullOrWhiteSpace(result?.Message?.Content))
            {
                _logger.LogWarning("Ollama returned empty content for: {Description}", description);
                return new StationClassificationResult { CategoryId = otherSeverity.SeverityId };
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(result.Message.Content);
                JsonElement root = doc.RootElement;
                
                string? categoryName = root.TryGetProperty("category", out JsonElement categoryElement) ? categoryElement.GetString() : null;
                string? status = root.TryGetProperty("status", out JsonElement statusElement) ? statusElement.GetString() : null;
                
                if (categoryName == null || status == null)
                {
                    _logger.LogWarning("Ollama JSON missing required fields. Content: {Content}", result.Message.Content);
                }

                int? matchedId = categoryName != null ? GetSeverityId(categoryName) : null;
                StationClassificationResult finalResult = new()
                { 
                    CategoryId = matchedId ?? otherSeverity.SeverityId, 
                    IsFuture = status == "StartingLater"
                };

                _logger.LogInformation("Classified disruption: '{Description}' -> Category: {Category}, IsFuture: {IsFuture}, Reasoning: {Reasoning}",
                    description, categoryName ?? "Unknown", finalResult.IsFuture, 
                    root.TryGetProperty("reasoning", out JsonElement reasoning) ? reasoning.GetString() : "None");

                _cache[description] = finalResult;
                return finalResult;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Ollama JSON: {Content}", result.Message.Content);
                return new StationClassificationResult { CategoryId = otherSeverity.SeverityId };
            }
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
