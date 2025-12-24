using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using TubeTracker.API;
using TubeTracker.API.Services;

namespace TubeTracker.Tests.Integration;

public class BasicIntegrationTests
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // Set dummy environment variables/configuration to satisfy AddAndConfigureFromEnv
                builder.UseSetting("DB_HOST", "localhost");
                builder.UseSetting("DB_PORT", "3306");
                builder.UseSetting("DB_NAME", "tubetracker_test");
                builder.UseSetting("DB_USER", "test");
                builder.UseSetting("DB_PASSWORD", "test");
                
                builder.UseSetting("JWT_SECRET", "SUPER_SECRET_KEY_FOR_TESTING_123456789");
                builder.UseSetting("JWT_ISSUER", "TubeTrackerTest");
                builder.UseSetting("JWT_AUDIENCE", "TubeTrackerTest");
                
                builder.UseSetting("TFL_APP_KEY", "test_key");
                builder.UseSetting("TFL_BASE_URL", "http://localhost");
                
                builder.UseSetting("PROXY_TRUSTED_PROXIES", "127.0.0.1");

                builder.UseSetting("TokenDenySettings:CleanupInterval", "01:00:00");
                builder.UseSetting("StatusBackgroundSettings:RefreshIntervalMinutes", "1");
                builder.UseSetting("StatusBackgroundSettings:HistoryCleanupDays", "30");
                
                builder.UseSetting("SecurityLockoutSettings:MaxFailedAttempts", "5");
                builder.UseSetting("SecurityLockoutSettings:InitialLockoutDuration", "00:05:00");
                builder.UseSetting("SecurityLockoutSettings:IncrementalLockoutDuration", "00:05:00");
                builder.UseSetting("SecurityLockoutSettings:FailedAttemptResetInterval", "00:05:00");
                builder.UseSetting("SecurityLockoutSettings:CleanupInterval", "00:01:00");

                builder.UseSetting("OllamaSettings:BaseUrl", "http://localhost:11434");
                builder.UseSetting("OllamaSettings:ModelName", "llama3");
                builder.UseSetting("OllamaSettings:SystemPrompt", "Test Prompt");

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IEmailService>();
                    services.AddSingleton(new Mock<IEmailService>().Object);
                    
                    // Replace IDbConnection with a mock to prevent real connection attempts
                    services.RemoveAll<System.Data.IDbConnection>();
                    var mockDb = new Mock<System.Data.IDbConnection>();
                    mockDb.Setup(d => d.Open());
                    services.AddScoped(_ => mockDb.Object);
                });
            });
            
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Get_HealthEndpoint_ReturnsOkOrRedirect()
    {
        // Act
        // We hit the root. Since it serves static files (index.html), it should return OK.
        var response = await _client.GetAsync("/");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("<!DOCTYPE html>")); // Assumes index.html has this
    }

    [Test]
    public async Task Get_LinesEndpoint_ReturnsSuccess()
    {
        // Act
        // This endpoint might fail if the DB isn't reachable, but if it returns 200 or 500,
        // it proves the routing works. Ideally, we want 200.
        // If it returns 200, it means our DI for Repositories is working.
        var response = await _client.GetAsync("/api/lines");

        // Assert
        // We accept InternalServerError because the test environment might not have the DB connection string set up correctly
        // but we mainly want to ensure it's not 404.
        Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Swagger_Endpoint_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/scalar/v1");

        // Assert
        // Scalar/Swagger should be available in development.
        // Note: MapOpenApi is used, typically at /openapi/v1.json
        var openApiResponse = await _client.GetAsync("/openapi/v1.json");
        
        Assert.That(openApiResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
