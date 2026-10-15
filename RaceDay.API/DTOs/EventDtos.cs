using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

public class EventRequestDto
{
    [Required, MaxLength(150)]
    public string EventName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public DateTime EventDate { get; set; }

    /// <summary>Run, Walk or Cycle.</summary>
    [Required]
    public string EventType { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int VenueId { get; set; }
}

public class VenueResponseDto
{
    public int VenueID { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public class EventResponseDto
{
    public int EventID { get; set; }
    public int OrganiserID { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string EventType { get; set; } = string.Empty;
    public VenueResponseDto? Venue { get; set; }
    public List<CategoryResponseDto> Categories { get; set; } = new();
}