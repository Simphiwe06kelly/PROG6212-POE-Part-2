using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

public class EnrolmentRequestDto
{
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
}

public class EnrolmentResponseDto
{
    public int EnrolmentID { get; set; }
    public int ParticipantID { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int CategoryID { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int EventID { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public DateTime EnrolmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
}