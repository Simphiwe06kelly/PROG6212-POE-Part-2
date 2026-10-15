using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

public class UpdateProfileDto
{
    [Required, MaxLength(50)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(100)]
    public string Email { get; set; } = string.Empty;
}