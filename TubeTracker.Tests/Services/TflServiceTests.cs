using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using TubeTracker.API.Models.Tfl;
using TubeTracker.API.Services;
using TubeTracker.API.Settings;

namespace TubeTracker.Tests.Services;

public class TflServiceTests
{
    private MockHttpMessageHandler _mockHttp;
    private Mock<ILogger<TflService>> _loggerMock;
    private TflSettings _settings;
    private TflService _service;

    [SetUp]
    public void Setup()
    {
        _mockHttp = new MockHttpMessageHandler();
        _loggerMock = new Mock<ILogger<TflService>>();
        _settings = new TflSettings { AppKey = "test-key" };

        HttpClient httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://api.tfl.gov.uk");

        _service = new TflService(httpClient, _settings, _loggerMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _mockHttp.Dispose();
    }

    [Test]
    public async Task GetLineStatusesAsync_ReturnsLines_WhenApiSuccess()
    {
        List<TflLine> lines =
        [
            new() { Id = "bakerloo", Name = "Bakerloo" },
            new() { Id = "victoria", Name = "Victoria" }
        ];
        string json = JsonSerializer.Serialize(lines);

        _mockHttp.When("https://api.tfl.gov.uk/*")
            .Respond("application/json", json);

        List<TflLine> result = await _service.GetLineStatusesAsync();

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Name, Is.EqualTo("Bakerloo"));
    }

    [Test]
    public async Task GetLineStatusesAsync_ReturnsEmptyList_WhenApiFails()
    {
        _mockHttp.When("https://api.tfl.gov.uk/*")
            .Respond(HttpStatusCode.InternalServerError);

        List<TflLine> result = await _service.GetLineStatusesAsync();

        Assert.That(result, Is.Empty);

        _loggerMock.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), 
            Times.Once);
    }

    [Test]
    public async Task GetStationsAsync_ParsesJsonArray()
    {
        List<TflStopPoint> stations = [new() { Id = "1", CommonName = "Station A" }];
        string json = JsonSerializer.Serialize(stations);

        _mockHttp.When("https://api.tfl.gov.uk/*")
            .Respond("application/json", json);

        List<TflStopPoint> result = await _service.GetStationsAsync();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].CommonName, Is.EqualTo("Station A"));
    }

    [Test]
    public async Task GetStationsAsync_ParsesWrappedJsonObject()
    {
        var wrapper = new { stopPoints = new[] { new { id = "2", commonName = "Station B" } } };
        string json = JsonSerializer.Serialize(wrapper);

        _mockHttp.When("https://api.tfl.gov.uk/*")
            .Respond("application/json", json);

        List<TflStopPoint> result = await _service.GetStationsAsync();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].CommonName, Is.EqualTo("Station B"));
    }
}
