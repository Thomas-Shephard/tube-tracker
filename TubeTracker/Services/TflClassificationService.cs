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
                                  Current Date/Time: {dateString}
                                  
                                  LOGIC RULES:
                                  1. Identify the START time/date mentioned. If it is later than {dateString}, is_future MUST be TRUE.
                                  2. "After [Time] tonight" is FUTURE if that time has not arrived yet.
                                  3. "Until [Date]" (even with years like 2026) is ACTIVE NOW. -> is_future: false.
                                  4. "This station is closed until..." is active now. -> is_future: false.
                                  
                                  REQUIRED THINKING PROCESS:
                                  - Current Time: {dateString}
                                  - Mentioned Start: [Identify it]
                                  - Is Mentioned Start in the future? [Yes/No]
                                  
                                  EXAMPLES:
                                  - "Tonight after 23:10, no service" (Current time 15:00) -> is_future: true
                                  - "From 21:40 today, queuing in operation" (Current time 15:00) -> is_future: true
                                  - "Station is closed until spring 2026" -> is_future: false
                                  - "Until early March 2026, exit 1 will be closed" -> is_future: false
                                  - "From Monday 20 Oct (Past) until Nov 2026, route closed" -> is_future: false
                                  """;

            string userPrompt = $$"""
                                Classify: "{{description}}"
                                Categories: {{string.Join(", ", categories)}}
                                Respond with: { "reasoning": "...", "category": "...", "is_future": bool }
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
