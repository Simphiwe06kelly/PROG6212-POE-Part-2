using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models
{
    public class Event
    {
        public int EventID { get; set; }
        public int OrganiserID { get; set; }
        public int VenueID { get; set; }

        [Required, MaxLength(150)]
        public string EventName { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public DateTime EventDate { get; set; }

        [Required, MaxLength(20)]
        public string EventType { get; set; } = string.Empty; // Run / Walk / Cycle

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public User? Organiser { get; set; }
        public Venue? Venue { get; set; }
        public ICollection<Category> Categories { get; set; } = new List<Category>();
    }
}