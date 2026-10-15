using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models
{
    public class Result
    {
        public int ResultID { get; set; }
        public int EnrolmentID { get; set; }
        public TimeSpan? FinishTime { get; set; }
        public int? OverallPosition { get; set; }
        public int? CategoryPosition { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Finished"; // Finished / DNF / DQ

        public Enrolment? Enrolment { get; set; }
    }
}