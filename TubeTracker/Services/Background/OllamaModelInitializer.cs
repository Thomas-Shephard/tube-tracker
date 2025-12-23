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
        client.Timeout = TimeSpan.FromHours(1); // Pulling large models takes time

        logger.LogInformation("Waiting for Ollama to be responsive at {Url}...", settings.BaseUrl);

        bool isReady = false;
        int retries = 0;
        while (!stoppingToken.IsCancellationRequested && retries < 300)
        {
            try
            {
                // Simple head request or empty get to check if the server is up
                using var response = await client.GetAsync("/", stoppingToken);
                if (response.IsSuccessStatusCode)
                {
                    isReady = true;
                    break;
                }
            }
            catch (Exception ex)
            {
                if (retries % 10 == 0) // Log every 10 seconds to avoid spam
                {
                    logger.LogInformation("Still waiting for Ollama... (Error: {Message})", ex.Message);
                }
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
            logger.LogInformation("Ollama is responsive. Checking model '{ModelName}'...", settings.ModelName);

            bool modelExists = false;
            try
            {
                var response = await client.GetFromJsonAsync<OllamaModelListResponse>("/api/tags", stoppingToken);
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

            logger.LogInformation("Model '{ModelName}' not found. Initiating pull (this may take several minutes)...", settings.ModelName);

            OllamaPullRequest pullRequest = new() { Name = settings.ModelName, Stream = true };
            using HttpResponseMessage pullResponse = await client.PostAsJsonAsync("/api/pull", pullRequest, stoppingToken);
            pullResponse.EnsureSuccessStatusCode();

            await using Stream stream = await pullResponse.Content.ReadAsStreamAsync(stoppingToken);
            using StreamReader reader = new(stream);

            while (!stoppingToken.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(stoppingToken);
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (!line.Contains("\"status\":\"success\"")) continue;
                logger.LogInformation("Successfully pulled Ollama model '{ModelName}'.", settings.ModelName);
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing Ollama model.");
        }
    }
}
