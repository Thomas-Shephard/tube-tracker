using System.Text.Json;
using TubeTracker.API.Models;

namespace TubeTracker.API.Services;

public interface ITflService
{
    Task<List<TflLine>> GetTubeStatusesAsync();
}

public class TflService : ITflService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://api.tfl.gov.uk";

    public TflService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(BaseUrl);
    }

    public async Task<List<TflLine>> GetTubeStatusesAsync()
    {
        // endpoint for all tube lines status
        var response = await _httpClient.GetAsync("/Line/Mode/tube/Status");
        
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        
        var lines = JsonSerializer.Deserialize<List<TflLine>>(content);
        
        return lines ?? new List<TflLine>();
    }
}
