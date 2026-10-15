using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs;

public class ResultCreateDto
{
    [Range(1, int.MaxValue)]
    public int EnrolmentId { get; set; }

    /// <summary>Format hh:mm:ss, e.g. 00:22:14. Required when status is Finished.</summary>
    public TimeSpan? FinishTime { get; set; }

    [Range(1, int.MaxValue)]
    public int? OverallPosition { get; set; }

    [Range(1, int.MaxValue)]
    public int? CategoryPosition { get; set; }

    /// <summary>Finished, DNF or DQ.</summary>
    [Required]
    public string Status { get; set; } = "Finished";
}

public class ResultUpdateDto
{
    /// <summary>Format hh:mm:ss, e.g. 00:22:14. Required when status is Finished.</summary>
    public TimeSpan? FinishTime { get; set; }

    [Range(1, int.MaxValue)]
    public int? OverallPosition { get; set; }

    [Range(1, int.MaxValue)]
    public int? CategoryPosition { get; set; }

    /// <summary>Finished, DNF or DQ.</summary>
    [Required]
    public string Status { get; set; } = "Finished";
}

public class ResultResponseDto
{
    public int ResultID { get; set; }
    public int EnrolmentID { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int EventID { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public TimeSpan? FinishTime { get; set; }
    public int? OverallPosition { get; set; }
    public int? CategoryPosition { get; set; }
    public string Status { get; set; } = string.Empty;
}