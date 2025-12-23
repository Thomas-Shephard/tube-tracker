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
                                  You are a London Underground disruption classifier.
                                  
                                  STATUS DEFINITIONS:
                                  - "ActiveNow": The disruption is happening RIGHT NOW. It is currently in progress.
                                  - "StartingLater": The disruption is NOT yet in progress, but the NEXT occurrence starts in the future.
                                  
                                  RULES:
                                  1. CATEGORY SELECTION: Be precise. 
                                     - Use "Closed" ONLY if the entire station or line segment is completely inaccessible.
                                     - Use "Partially Closed" if specific exits, platforms, or directions are closed, but the station/line remains somewhat functional.
                                     - Use "Information" or "Other" for minor things like toilets, lifts, or queuing systems if the station remains open.
                                  2. TIME COMPARISON (MOST IMPORTANT):
                                     - Compare the "Current Time" provided in the user prompt with the disruption's start/end times.
                                     - If the current time is BEFORE the start time, it is "StartingLater".
                                     - If the current time is BETWEEN the start and end times, it is "ActiveNow".
                                  3. TIME FORMATS: Times like "2140" mean 21:40 (24-hour clock).
                                  4. UNTIL FURTHER NOTICE: If no specific daily time window is given (e.g., "until spring 2026"), it is "ActiveNow".
                                  
                                  OUTPUT INSTRUCTIONS:
                                  - Respond ONLY with a valid JSON object.
                                  - No preamble, no explanation outside the JSON.
                                  - JSON MUST contain "category", "status", "time_analysis", and "reasoning" keys.
                                  """;

            string userPrompt = $$"""
                                Current Time: {{dateString}}
                                Classify this disruption: "{{description}}"
                                Allowed Categories: {{string.Join(", ", categories)}}
                                Expected JSON format: { "category": "string", "status": "ActiveNow|StartingLater", "time_analysis": "string", "reasoning": "string" }
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

                _logger.LogInformation("Classified disruption: '{Description}' -> Category: {Category}, IsFuture: {IsFuture}, Analysis: {Analysis}, Reasoning: {Reasoning}",
                    description, categoryName ?? "Unknown", finalResult.IsFuture,
                    root.TryGetProperty("time_analysis", out JsonElement analysis) ? analysis.GetString() : "N/A",
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
