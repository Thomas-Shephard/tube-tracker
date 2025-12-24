using TubeTracker.API.Models.Tfl;

namespace TubeTracker.API.Services;

public interface ITflService
{
    Task<List<TflLine>> GetLineStatusesAsync();
    Task<List<TflStationDisruption>> GetStationDisruptionsAsync();
    Task<List<TflStopPoint>> GetStationsAsync();
}
