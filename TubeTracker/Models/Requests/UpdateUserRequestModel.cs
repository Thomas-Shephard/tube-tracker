using System.ComponentModel.DataAnnotations;

namespace TubeTracker.API.Models.Requests;

public class UpdateUserRequestModel
{
    [Required]
    [StringLength(70, MinimumLength = 2)]
    public required string Name { get; init; }
}
