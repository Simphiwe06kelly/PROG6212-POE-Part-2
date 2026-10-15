using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models
{
    public class User
    {
        public int UserID { get; set; }

        [Required, MaxLength(50)]
        public string FullName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(250)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Role { get; set; } = string.Empty; // Organiser / Participant

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<Event> OrganisedEvents { get; set; } = new List<Event>();
        public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
    }
}