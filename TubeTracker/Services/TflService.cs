using System.Text.Json;
using System.Text.Json.Serialization;
using TubeTracker.API.Models.Tfl;
using TubeTracker.API.Settings;

namespace TubeTracker.API.Services;

public interface ITflService
{
    Task<List<TflLine>> GetLineStatusesAsync();
    Task<List<TflStationDisruption>> GetStationDisruptionsAsync();
    Task<List<TflStopPoint>> GetStationsAsync();
}

public class TflService : ITflService
{
    private readonly HttpClient _httpClient;
    private readonly TflSettings _tflSettings;
    private readonly ILogger<TflService> _logger;
    private const string BaseUrl = "https://api.tfl.gov.uk";
    private const string Modes = "tube,overground,dlr,elizabeth-line";

    public TflService(HttpClient httpClient, TflSettings tflSettings, ILogger<TflService> logger)
    {
        _httpClient = httpClient;
        _tflSettings = tflSettings;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(BaseUrl);
    }

    public async Task<List<TflLine>> GetLineStatusesAsync()
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/Line/Mode/{Modes}/Status?app_key={_tflSettings.AppKey}");
            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync();
            List<TflLine>? lines = JsonSerializer.Deserialize<List<TflLine>>(content);
            return lines ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch line statuses from TFL API.");
            return [];
        }
    }

    public async Task<List<TflStationDisruption>> GetStationDisruptionsAsync()
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/StopPoint/Mode/{Modes}/Disruption?app_key={_tflSettings.AppKey}");
            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync();
            List<TflStationDisruption>? disruptions = JsonSerializer.Deserialize<List<TflStationDisruption>>(content);
            return disruptions ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch station disruptions from TFL API.");
            return [];
        }
    }

    public async Task<List<TflStopPoint>> GetStationsAsync()
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/StopPoint/Mode/{Modes}?app_key={_tflSettings.AppKey}");
            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync();

            try
            {
                List<TflStopPoint>? stopPoints = JsonSerializer.Deserialize<List<TflStopPoint>>(content);
                return stopPoints ?? [];
            }
            catch (JsonException)
            {
                TflStopPointResponse? wrapper = JsonSerializer.Deserialize<TflStopPointResponse>(content);
                return wrapper?.StopPoints ?? [];
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch stations from TFL API.");
            return [];
        }
    }
    
    private sealed class TflStopPointResponse
    {
        [JsonPropertyName("stopPoints")]
        public List<TflStopPoint> StopPoints { get; init; } = [];
    }
}
