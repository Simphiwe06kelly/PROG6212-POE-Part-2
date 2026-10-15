using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models
{
    public class Category
    {
        public int CategoryID { get; set; }
        public int EventID { get; set; }

        [Required, MaxLength(50)]
        public string CategoryName { get; set; } = string.Empty;

        public decimal DistanceKm { get; set; }
        public decimal EntryFee { get; set; }
        public int MaxParticipants { get; set; } = 100;

        public Event? Event { get; set; }
        public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
    }
}