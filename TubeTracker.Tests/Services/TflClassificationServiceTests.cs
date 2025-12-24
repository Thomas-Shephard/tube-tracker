using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using RichardSzalay.MockHttp;
using TubeTracker.API.Models.Classification;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;
using TubeTracker.API.Services;
using TubeTracker.API.Settings;

namespace TubeTracker.Tests.Services;

public class TflClassificationServiceTests
{
    private MockHttpMessageHandler _mockHttp;
    private Mock<IStationStatusSeverityRepository> _mockSeverityRepo;
    private Mock<IOllamaStatusService> _mockStatusService;
    private Mock<ILogger<TflClassificationService>> _mockLogger;
    private FakeTimeProvider _timeProvider;
    private OllamaSettings _settings;
    private TflClassificationService _service;

    [SetUp]
    public void Setup()
    {
        _mockHttp = new MockHttpMessageHandler();
        _mockSeverityRepo = new Mock<IStationStatusSeverityRepository>();
        _mockStatusService = new Mock<IOllamaStatusService>();
        _mockLogger = new Mock<ILogger<TflClassificationService>>();
        _timeProvider = new FakeTimeProvider();

        _settings = new OllamaSettings
        {
            BaseUrl = "http://test-ollama",
            ModelName = "test-model"
        };

        List<StationStatusSeverity> severities =
        [
            new() { SeverityId = 1, Description = "Closed", Urgency = 3 },
            new() { SeverityId = 2, Description = "Partially Closed", Urgency = 2 },
            new() { SeverityId = 12, Description = "Other", Urgency = 0 }
        ];
        _mockSeverityRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(severities);
        _mockStatusService.Setup(s => s.WaitUntilReadyAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        HttpClient httpClient = _mockHttp.ToHttpClient();

        _service = new TflClassificationService(
            httpClient,
            _settings,
            _mockSeverityRepo.Object,
            _mockStatusService.Object,
            _mockLogger.Object,
            _timeProvider
        );
    }

    [TearDown]
    public void TearDown()
    {
        _mockHttp.Dispose();
    }

    [Test]
    public async Task ClassifyStationDisruptionAsync_ReturnsCachedResult_IfAvailable()
    {
        const string description = "Test disruption";

        MockedRequest request = _mockHttp.When("http://test-ollama/api/chat")
                                         .Respond("application/json", JsonSerializer.Serialize(new OllamaResponse
                                         {
                                             Message = new OllamaMessage { Role = "assistant", Content = "{\"category\": \"Closed\", \"status\": \"ActiveNow\"}" }
                                         }));

        await _service.ClassifyStationDisruptionAsync(description);
        await _service.ClassifyStationDisruptionAsync(description);

        Assert.That(_mockHttp.GetMatchCount(request), Is.EqualTo(1));
    }

    [Test]
    public async Task ClassifyStationDisruptionAsync_CallsOllama_AndReturnsResult()
    {
        const string description = "Station closed due to flooding";
        var responseContent = new
        {
            category = "Closed",
            status = "ActiveNow",
            reasoning = "Flooding implies closure"
        };

        _mockHttp.When("http://test-ollama/api/chat")
                 .Respond("application/json", JsonSerializer.Serialize(new OllamaResponse
                 {
                     Message = new OllamaMessage { Role = "assistant", Content = JsonSerializer.Serialize(responseContent) }
                 }));

        StationClassificationResult result = await _service.ClassifyStationDisruptionAsync(description);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.CategoryId, Is.EqualTo(1));
            Assert.That(result.IsFuture, Is.False);
        }
    }
}
