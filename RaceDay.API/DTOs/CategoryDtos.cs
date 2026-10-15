using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

public class CategoryRequestDto
{
    [Required, MaxLength(50)]
    public string CategoryName { get; set; } = string.Empty;

    [Range(0.01, 999.99)]
    public decimal DistanceKm { get; set; }

    [Range(0, 999999.99)]
    public decimal EntryFee { get; set; }

    [Range(1, int.MaxValue)]
    public int MaxParticipants { get; set; } = 100;
}

public class CategoryResponseDto
{
    public int CategoryID { get; set; }
    public int EventID { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public decimal EntryFee { get; set; }
    public int MaxParticipants { get; set; }
}