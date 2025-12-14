using System.ComponentModel.DataAnnotations;

namespace TubeTracker.API.Models.Requests;

public class TrackedLineRequestModel
{
    [Required]
    public int LineId { get; init; }

    public bool Notify { get; init; } = false;
    
    public int MinUrgency { get; init; } = 2;
}
