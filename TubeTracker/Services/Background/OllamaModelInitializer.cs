using TubeTracker.API.Models.Classification;
using TubeTracker.API.Settings;

namespace TubeTracker.API.Services.Background;

public class OllamaModelInitializer(
    IHttpClientFactory httpClientFactory,
    OllamaSettings settings,
    IOllamaStatusService statusService,
    ILogger<OllamaModelInitializer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await InitializeInternalAsync(stoppingToken);
        }
        finally
        {
            statusService.SetReady();
        }
    }

    private async Task InitializeInternalAsync(CancellationToken stoppingToken)
    {
        using HttpClient client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(settings.BaseUrl);
        client.Timeout = TimeSpan.FromMinutes(30);

        logger.LogInformation("Waiting for Ollama to be ready at {Url}...", settings.BaseUrl);

        bool isReady = false;
        int retries = 0;
        // Poll until Ollama is responsive (max 300s)
        while (!stoppingToken.IsCancellationRequested && retries < 300)
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync("/", stoppingToken);
                if (response.IsSuccessStatusCode)
                {
                    isReady = true;
                    break;
                }
            }
            catch
            {
                // Ignore connection errors while waiting
            }
            
            await Task.Delay(1000, stoppingToken);
            retries++;
        }

        if (!isReady)
        {
            logger.LogError("Ollama failed to start within timeout. Model initialization aborted.");
            return;
        }

        try
        {
            logger.LogInformation("Ollama is ready. Checking model '{ModelName}'...", settings.ModelName);

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
                logger.LogWarning("Failed to list models: {Message}. Attempting pull anyway.", ex.Message);
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
