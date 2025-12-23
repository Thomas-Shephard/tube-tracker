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
                                  Current Time: {dateString}
                                  
                                  STATUS DEFINITIONS:
                                  - "ActiveNow": The disruption is happening AT THIS EXACT MOMENT ({dateString}), or is a continuous closure (e.g., "until 2026").
                                  - "StartingLater": The NEXT occurrence of this disruption starts in the future relative to {dateString}.
                                  
                                  RULES:
                                  1. RECURRING EVENTS: For "each evening after [Time]", if the time hasn't arrived TODAY yet, use "StartingLater" (even if yesterday's session is over).
                                  2. Identify the NEXT start time. If it is in the future, use "StartingLater".
                                  3. If it says "Until [Future Date]", it means it is a continuous closure happening now. Use "ActiveNow".
                                  
                                  EXAMPLES:
                                  - "Mon 22 and Tue 23, after 23:10 each evening" (Current time: Tue 15:00) -> status: StartingLater (Next starts at 23:10 tonight).
                                  - "Today after 21:40, queuing starts" (Current time 15:00) -> status: StartingLater
                                  - "Station closed until spring 2026" -> status: ActiveNow
                                  
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
