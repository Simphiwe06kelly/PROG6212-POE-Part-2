using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models
{
    public class Venue
    {
        public int VenueID { get; set; }

        [Required, MaxLength(150)]
        public string VenueName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Address { get; set; }

        [Required, MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Province { get; set; } = string.Empty;

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        public ICollection<Event> Events { get; set; } = new List<Event>();
    }
}