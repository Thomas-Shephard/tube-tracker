using System.Net.Http.Json;
using TubeTracker.API.Models.Classification;
using TubeTracker.API.Settings;

namespace TubeTracker.API.Services.Background;

public class OllamaModelInitializer(
    IHttpClientFactory httpClientFactory,
    OllamaSettings settings,
    ILogger<OllamaModelInitializer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for Ollama to be ready
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        try
        {
            using HttpClient client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(settings.BaseUrl);
            client.Timeout = TimeSpan.FromMinutes(30);

            logger.LogInformation("Checking if Ollama model '{ModelName}' is available...", settings.ModelName);

            bool modelExists = false;
            try
            {
                OllamaModelListResponse? response = await client.GetFromJsonAsync<OllamaModelListResponse>("/api/tags", stoppingToken);
                if (response?.Models != null)
                {
                    modelExists = response.Models.Any(m => 
                        m.Name.Equals(settings.ModelName, StringComparison.OrdinalIgnoreCase) || 
                        m.Name.Equals(settings.ModelName + ":latest", StringComparison.OrdinalIgnoreCase));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Failed to check existing models: {Message}. Attempting pull anyway.", ex.Message);
            }

            if (modelExists)
            {
                logger.LogInformation("Ollama model '{ModelName}' is already available.", settings.ModelName);
                return;
            }

            logger.LogInformation("Model '{ModelName}' not found. Initiating pull...", settings.ModelName);

            OllamaPullRequest pullRequest = new() { Name = settings.ModelName, Stream = false };
            HttpResponseMessage pullResponse = await client.PostAsJsonAsync("/api/pull", pullRequest, stoppingToken);
            
            if (pullResponse.IsSuccessStatusCode)
            {
                logger.LogInformation("Successfully pulled Ollama model '{ModelName}'.", settings.ModelName);
            }
            else
            {
                string error = await pullResponse.Content.ReadAsStringAsync(stoppingToken);
                logger.LogError("Failed to pull Ollama model '{ModelName}'. Status: {Status}. Details: {Error}", settings.ModelName, pullResponse.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing Ollama model.");
        }
    }
}
