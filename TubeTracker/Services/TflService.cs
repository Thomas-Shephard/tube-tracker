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
    private const string BaseUrl = "https://api.tfl.gov.uk";
    private const string Modes = "tube,overground,dlr,elizabeth-line";

    public TflService(HttpClient httpClient, TflSettings tflSettings)
    {
        _httpClient = httpClient;
        _tflSettings = tflSettings;
        _httpClient.BaseAddress = new Uri(BaseUrl);
    }

    public async Task<List<TflLine>> GetLineStatusesAsync()
    {
        HttpResponseMessage response = await _httpClient.GetAsync($"/Line/Mode/{Modes}/Status?app_key={_tflSettings.AppKey}");
        
        response.EnsureSuccessStatusCode();

        string content = await response.Content.ReadAsStringAsync();
        
        List<TflLine>? lines = JsonSerializer.Deserialize<List<TflLine>>(content);
        
        return lines ?? [];
    }

    public async Task<List<TflStationDisruption>> GetStationDisruptionsAsync()
    {
        HttpResponseMessage response = await _httpClient.GetAsync($"/StopPoint/Mode/{Modes}/Disruption?app_key={_tflSettings.AppKey}");
        
        response.EnsureSuccessStatusCode();

        string content = await response.Content.ReadAsStringAsync();
        List<TflStationDisruption>? disruptions = JsonSerializer.Deserialize<List<TflStationDisruption>>(content);
        
        return disruptions ?? [];
    }

    public async Task<List<TflStopPoint>> GetStationsAsync()
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
    
    private sealed class TflStopPointResponse
    {
        [JsonPropertyName("stopPoints")]
        public List<TflStopPoint> StopPoints { get; init; } = [];
    }
}
