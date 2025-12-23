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

            string systemPrompt = $$"""
                                  You are a London Underground disruption classifier.
                                  Current Date: {{dateString}}
                                  
                                  RULES:
                                  1. CATEGORY:
                                     - "Closed": Entire station or line is shut.
                                     - "Partially Closed": Specific parts are shut (exits, platforms, one direction, escalators).
                                     - "Accessibility Issue": Lifts out of service, or specific step-free access routes unavailable.
                                     - "Information": Minor things (toilets, queuing, ticket halls).
                                     - "Other": Anything else.
                                  
                                  2. STATUS ("ActiveNow", "StartingLater", "Recurring"):
                                     - "Recurring": A disruption with a daily/nightly TIME window (e.g., "after 2100", "at 2335", "each evening", "until 0500").
                                     - "StartingLater": Disruptions starting on a FUTURE DATE (e.g., "Starts this Saturday", "from Monday 29 Dec").
                                     - "ActiveNow": 
                                        a) Continuous work with NO specific daily start/end times (e.g., "Until 2026", "Until further notice").
                                        b) Immediate incidents (e.g., "flooding", "signal failure").
                                        c) Permanent station rules (e.g., "travel in front 7 coaches").
                                  
                                  3. TIME RULE: Only use "Recurring" if you see specific clock times (e.g. 2335, 21:00) or daily periods (e.g. "each evening", "every night"). DO NOT use it for coach numbers or other counts.
                                  
                                  OUTPUT: Respond ONLY with JSON.
                                  { "category": "string", "status": "ActiveNow|StartingLater|Recurring", "reasoning": "string" }
                                  """;

            string userPrompt = $"""
                                Classify this: "{description}"
                                Allowed Categories: {string.Join(", ", categories)}
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
                    IsFuture = status == "StartingLater" || status == "Recurring"
                };

                _logger.LogInformation("Classified disruption: '{Description}' -> Category: {Category}, Status: {Status}, IsFuture: {IsFuture}, Reasoning: {Reasoning}",
                    description, categoryName ?? "Unknown", status ?? "Unknown", finalResult.IsFuture,
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
