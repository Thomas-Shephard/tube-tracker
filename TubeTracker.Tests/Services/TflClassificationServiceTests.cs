using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Moq.Protected;
using RichardSzalay.MockHttp;
using TubeTracker.API.Models.Classification;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;
using TubeTracker.API.Services;
using TubeTracker.API.Settings;

namespace TubeTracker.Tests.Services;

[TestFixture]
public class TflClassificationServiceTests
{
    private MockHttpMessageHandler _mockHttp;
    private Mock<IStationStatusSeverityRepository> _mockSeverityRepo;
    private Mock<IOllamaStatusService> _mockStatusService;
    private Mock<ILogger<TflClassificationService>> _mockLogger;
    private FakeTimeProvider _timeProvider;
    private OllamaSettings _settings;
    private IMemoryCache _memoryCache;
    private TflClassificationService _service;

    [SetUp]
    public void Setup()
    {
        _mockHttp = new MockHttpMessageHandler();
        _mockSeverityRepo = new Mock<IStationStatusSeverityRepository>();
        _mockStatusService = new Mock<IOllamaStatusService>();
        _mockLogger = new Mock<ILogger<TflClassificationService>>();
        _timeProvider = new FakeTimeProvider();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        
        _settings = new OllamaSettings 
        {
            BaseUrl = "http://test-ollama", 
            ModelName = "test-model",
            SystemPrompt = "Test Prompt"
        };

        var severities = new List<StationStatusSeverity>
        {
            new() { SeverityId = 1, Description = "Closed", Urgency = 3 },
            new() { SeverityId = 2, Description = "Partially Closed", Urgency = 2 },
            new() { SeverityId = 12, Description = "Other", Urgency = 0 }
        };
        _mockSeverityRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(severities);
        _mockStatusService.Setup(s => s.WaitUntilReadyAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var httpClient = _mockHttp.ToHttpClient();
        
        _service = new TflClassificationService(
            httpClient, 
            _settings, 
            _mockSeverityRepo.Object, 
            _mockStatusService.Object,
            _mockLogger.Object,
            _timeProvider,
            _memoryCache
        );
    }

        [TearDown]
        public void TearDown()
        {
            _mockHttp?.Dispose();
            _memoryCache?.Dispose();
        }
    
            [Test]
            public async Task ClassifyStationDisruptionAsync_ReturnsCachedResult_IfAvailable()
            {
                // Arrange
                var description = "Test disruption";
                
                var request = _mockHttp.When("http://test-ollama/api/chat")
                    .Respond("application/json", JsonSerializer.Serialize(new OllamaResponse 
                    { 
                        Message = new OllamaMessage { Role = "assistant", Content = "{\"category\": \"Closed\", \"status\": \"ActiveNow\"}" } 
                    }));
        
                // Act
                await _service.ClassifyStationDisruptionAsync(description);
                await _service.ClassifyStationDisruptionAsync(description);
        
                // Assert
                Assert.That(_mockHttp.GetMatchCount(request), Is.EqualTo(1));
            }    
        [Test]
        public async Task ClassifyStationDisruptionAsync_CallsOllama_AndReturnsResult()
        {
            // Arrange
            var description = "Station closed due to flooding";
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
    
            // Act
            var result = await _service.ClassifyStationDisruptionAsync(description);
    
            // Assert
            Assert.That(result.CategoryId, Is.EqualTo(1));
            Assert.That(result.IsFuture, Is.False);
        }
    }