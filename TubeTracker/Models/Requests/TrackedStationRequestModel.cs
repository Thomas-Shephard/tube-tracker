using System.ComponentModel.DataAnnotations;

namespace TubeTracker.API.Models.Requests;

public class TrackedStationRequestModel
{
    [Required]
    public int StationId { get; init; }

    public bool Notify { get; init; } = false;

    public int MinUrgency { get; init; } = 2;
}
