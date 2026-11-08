using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace RaceDay.Web.Models;

// ---------------- Account ----------------

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "That doesn't look like a valid email address.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Please enter your full name.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Your name must be between 2 and 50 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "That doesn't look like a valid email address.")]
    [StringLength(100, ErrorMessage = "Your email address is too long.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose a password.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Your password must be at least 6 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please repeat your password.")]
    [Compare(nameof(Password), ErrorMessage = "The two passwords don't match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose how you will use RaceDay.")]
    [RegularExpression("^(Organiser|Participant)$", ErrorMessage = "Please choose Participant or Organiser.")]
    public string Role { get; set; } = Roles.Participant;
}

public class ProfileViewModel
{
    [Required(ErrorMessage = "Please enter your full name.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Your name must be between 2 and 50 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "That doesn't look like a valid email address.")]
    [StringLength(100, ErrorMessage = "Your email address is too long.")]
    public string Email { get; set; } = string.Empty;

    // Display only (not posted)
    public string Role { get; set; } = string.Empty;
    public string? PictureUrl { get; set; }
    public DateTime MemberSince { get; set; }
}

public class ErrorViewModel
{
    public int Code { get; set; }
    public string Title { get; set; } = "Something went wrong";
    public string Message { get; set; } = "Sorry, something went wrong on our side. Please try again.";
    public string? RequestId { get; set; }
}

// ---------------- Browsing ----------------

public class EventFilter
{
    public string? Search { get; set; }
    public string? Type { get; set; }
    public string? Province { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public bool IsActive =>
        !string.IsNullOrWhiteSpace(Search) || !string.IsNullOrWhiteSpace(Type) ||
        !string.IsNullOrWhiteSpace(Province) || From.HasValue || To.HasValue;
}

public class HomeViewModel
{
    public EventFilter Filter { get; set; } = new();
    public List<EventDto> Events { get; set; } = new();
    public List<string> Provinces { get; set; } = new();
    public string? LoadError { get; set; }
}

public class EventDetailsViewModel
{
    public EventDto Event { get; set; } = new();
    public List<ResultDto> Results { get; set; } = new();
    public HashSet<int> EnrolledCategoryIds { get; set; } = new();
}

// ---------------- Participant ----------------

public class MyEnrolmentsViewModel
{
    public List<EnrolmentDto> Enrolments { get; set; } = new();
}

public class MyResultsViewModel
{
    public List<ResultDto> Results { get; set; } = new();
}

// ---------------- Organiser ----------------

public class DashboardViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public List<EventDto> Upcoming { get; set; } = new();
    public int TotalEnrolments { get; set; }
    public string? LoadError { get; set; }
}

public class EventFormViewModel
{
    public int? EventId { get; set; }

    [Required(ErrorMessage = "Please enter the event name.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "The event name must be between 3 and 150 characters.")]
    [Display(Name = "Event name")]
    public string EventName { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "The description can be at most 1000 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Please choose the event date.")]
    [DataType(DataType.Date)]
    [Display(Name = "Date")]
    public DateTime? EventDate { get; set; }

    [Required(ErrorMessage = "Please choose the event type.")]
    [RegularExpression("^(Run|Walk|Cycle)$", ErrorMessage = "Please choose Run, Walk or Cycle.")]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = EventTypes.Run;

    [Range(1, int.MaxValue, ErrorMessage = "Please choose a venue (the event location).")]
    [Display(Name = "Venue (location)")]
    public int VenueId { get; set; }

    [Display(Name = "Banner image")]
    public IFormFile? Banner { get; set; }

    public string? CurrentBannerUrl { get; set; }

    [ValidateNever]
    public List<VenueDto> Venues { get; set; } = new();

    public bool IsEdit => EventId.HasValue;
}

public static class EventTypes
{
    public const string Run = "Run";
    public const string Walk = "Walk";
    public const string Cycle = "Cycle";
    public static readonly string[] All = { Run, Walk, Cycle };
}

public class CategoryFormViewModel
{
    public int EventId { get; set; }

    /// <summary>0 means "add a new category".</summary>
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Please enter a category name, for example 10km or Under 20.")]
    [StringLength(50, ErrorMessage = "The category name can be at most 50 characters.")]
    [Display(Name = "Category name")]
    public string CategoryName { get; set; } = string.Empty;

    [Range(0.01, 999.99, ErrorMessage = "The distance must be between 0.01 and 999.99 km.")]
    [Display(Name = "Distance (km)")]
    public decimal DistanceKm { get; set; }

    [Range(0, 999999.99, ErrorMessage = "The entry fee must be zero or more.")]
    [Display(Name = "Entry fee (R)")]
    public decimal EntryFee { get; set; }

    [Range(1, 100000, ErrorMessage = "The maximum number of participants must be at least 1.")]
    [Display(Name = "Max participants")]
    public int MaxParticipants { get; set; } = 100;
}

public class CategoriesViewModel
{
    public EventDto Event { get; set; } = new();
    public CategoryFormViewModel NewCategory { get; set; } = new();
}

public class EnrolmentsViewModel
{
    public EventDto Event { get; set; } = new();
    public List<EnrolmentDto> Enrolments { get; set; } = new();
}

public class ResultRow
{
    public int EnrolmentId { get; set; }
    public int ResultId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? FinishTime { get; set; }
    public int? OverallPosition { get; set; }
    public int? CategoryPosition { get; set; }
    public string Status { get; set; } = "Finished";
    public bool HasResult => ResultId > 0;
}

public class ResultsEntryViewModel
{
    public EventDto Event { get; set; } = new();
    public List<ResultRow> Rows { get; set; } = new();
}

public class ResultFormViewModel
{
    public int EventId { get; set; }
    public int EnrolmentId { get; set; }

    /// <summary>0 means "no result yet" (create); otherwise correct the existing result.</summary>
    public int ResultId { get; set; }

    public string? FinishTime { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Positions must be 1 or higher.")]
    public int? OverallPosition { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Positions must be 1 or higher.")]
    public int? CategoryPosition { get; set; }

    [Required(ErrorMessage = "Please choose a status.")]
    [RegularExpression("^(Finished|DNF|DQ)$", ErrorMessage = "Status must be Finished, DNF or DQ.")]
    public string Status { get; set; } = "Finished";
}

public class VenuesViewModel
{
    public List<VenueDto> Venues { get; set; } = new();
    public VenueFormViewModel NewVenue { get; set; } = new();
}

public class VenueFormViewModel
{
    [Required(ErrorMessage = "Please enter the venue name.")]
    [StringLength(150, ErrorMessage = "The venue name can be at most 150 characters.")]
    [Display(Name = "Venue name")]
    public string VenueName { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "The address can be at most 200 characters.")]
    public string? Address { get; set; }

    [Required(ErrorMessage = "Please enter the city.")]
    [StringLength(100, ErrorMessage = "The city can be at most 100 characters.")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose the province.")]
    [StringLength(100, ErrorMessage = "The province can be at most 100 characters.")]
    public string Province { get; set; } = string.Empty;
}
