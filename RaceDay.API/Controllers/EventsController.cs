using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Attributes;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private static readonly string[] ValidTypes = { "Run", "Walk", "Cycle" };
    private readonly RaceDayDbContext _db;
    private readonly IImageStorage _images;
    private readonly ILogger<EventsController> _logger;

    public EventsController(RaceDayDbContext db, IImageStorage images, ILogger<EventsController> logger)
    {
        _db = db;
        _images = images;
        _logger = logger;
    }

    /// <summary>Returns all upcoming events so participants can browse them.</summary>
    /// <remarks>
    /// Public. Only events dated today or later are returned, soonest first. No request body.
    /// All query parameters are optional and can be combined to filter the list.
    /// </remarks>
    /// <param name="type">Only events of this type: Run, Walk or Cycle.</param>
    /// <param name="province">Only events held at a venue in this province, e.g. Western Cape.</param>
    /// <param name="search">Only events whose name, description or venue city contains this text.</param>
    /// <param name="fromDate">Only events on or after this date (yyyy-MM-dd).</param>
    /// <param name="toDate">Only events on or before this date (yyyy-MM-dd).</param>
    /// <response code="200">Array of events, each with its venue and categories.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<EventResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? type,
        [FromQuery] string? province,
        [FromQuery] string? search,
        [FromQuery(Name = "from")] DateTime? fromDate,
        [FromQuery(Name = "to")] DateTime? toDate)
    {
        var today = DateTime.Today;
        var query = _db.Events
            .Include(e => e.Venue)
            .Include(e => e.Categories)
            .Where(e => e.EventDate >= today);

        if (!string.IsNullOrWhiteSpace(type))
        {
            var t = type.Trim().ToLower();
            query = query.Where(e => e.EventType.ToLower() == t);
        }

        if (!string.IsNullOrWhiteSpace(province))
        {
            var p = province.Trim().ToLower();
            query = query.Where(e => e.Venue!.Province.ToLower() == p);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(e => e.EventName.ToLower().Contains(s)
                || (e.Description != null && e.Description.ToLower().Contains(s))
                || e.Venue!.City.ToLower().Contains(s));
        }

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(e => e.EventDate >= from);
        }

        if (toDate.HasValue)
        {
            var to = toDate.Value.Date;
            query = query.Where(e => e.EventDate <= to);
        }

        var events = await query.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(ToDto));
    }

    /// <summary>Returns every event the logged-in Organiser has created, past and upcoming.</summary>
    /// <remarks>
    /// Organisers only. No request body. Each event includes enrolmentCount, the number of confirmed
    /// enrolments across all of its categories, for the dashboard. Newest date first.
    /// </remarks>
    /// <response code="200">Array of the Organiser's events.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser.</response>
    [HttpGet("mine")]
    [RequireRole("Organiser")]
    [ProducesResponseType(typeof(List<EventResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Mine()
    {
        var userId = HttpContext.Session.GetUserId()!.Value;

        var events = await _db.Events
            .Include(e => e.Venue)
            .Include(e => e.Categories)
            .Where(e => e.OrganiserID == userId)
            .OrderByDescending(e => e.EventDate)
            .ToListAsync();

        var eventIds = events.Select(e => e.EventID).ToList();
        var counts = await _db.Enrolments
            .Where(en => en.Status == "Confirmed" && eventIds.Contains(en.Category!.EventID))
            .GroupBy(en => en.Category!.EventID)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count);

        var result = events.Select(e =>
        {
            var dto = ToDto(e);
            dto.EnrolmentCount = counts.GetValueOrDefault(e.EventID);
            return dto;
        });

        return Ok(result);
    }

    /// <summary>Returns full detail for a single event, including its venue and categories.</summary>
    /// <param name="id">The event ID.</param>
    /// <response code="200">The event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Venue)
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.EventID == id);

        if (ev == null) return NotFound(new { message = "Event not found." });
        return Ok(ToDto(ev));
    }

    /// <summary>Creates a new event owned by the logged-in Organiser.</summary>
    /// <remarks>Expects eventName, description, eventDate, eventType (Run, Walk or Cycle) and venueId (must exist).</remarks>
    /// <response code="201">The created event.</response>
    /// <response code="400">Validation failed (bad event type or unknown venue).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser.</response>
    [HttpPost]
    [RequireRole("Organiser")]
    [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] EventRequestDto dto)
    {
        var error = await ValidateAsync(dto);
        if (error != null) return BadRequest(new { message = error });

        var ev = new Event
        {
            OrganiserID = HttpContext.Session.GetUserId()!.Value,
            VenueID = dto.VenueId,
            EventName = dto.EventName.Trim(),
            Description = dto.Description,
            EventDate = dto.EventDate.Date,
            EventType = dto.EventType
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        await _db.Entry(ev).Reference(e => e.Venue).LoadAsync();
        return CreatedAtAction(nameof(GetById), new { id = ev.EventID }, ToDto(ev));
    }

    /// <summary>Updates an event the logged-in Organiser owns.</summary>
    /// <param name="id">The event ID.</param>
    /// <response code="200">The updated event.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owning Organiser.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpPut("{id:int}")]
    [RequireRole("Organiser")]
    [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] EventRequestDto dto)
    {
        var ev = await _db.Events
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.EventID == id);
        if (ev == null) return NotFound(new { message = "Event not found." });

        if (ev.OrganiserID != HttpContext.Session.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not own this event." });

        var error = await ValidateAsync(dto);
        if (error != null) return BadRequest(new { message = error });

        ev.VenueID = dto.VenueId;
        ev.EventName = dto.EventName.Trim();
        ev.Description = dto.Description;
        ev.EventDate = dto.EventDate.Date;
        ev.EventType = dto.EventType;
        await _db.SaveChangesAsync();

        await _db.Entry(ev).Reference(e => e.Venue).LoadAsync();
        return Ok(ToDto(ev));
    }

    /// <summary>Uploads (or replaces) the banner image of an event the logged-in Organiser owns.</summary>
    /// <remarks>
    /// Organisers only. Send the image as multipart/form-data in a field called "file".
    /// JPG, PNG, GIF or WEBP, up to 5 MB. The API stores the image in Azure Blob Storage and saves its URL
    /// on the event as bannerUrl.
    /// </remarks>
    /// <param name="id">The event ID.</param>
    /// <param name="file">The image file.</param>
    /// <response code="200">The updated event, including bannerUrl.</response>
    /// <response code="400">No file, wrong file type, or file larger than 5 MB.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owning Organiser.</response>
    /// <response code="404">Event does not exist.</response>
    /// <response code="503">Image storage is unavailable.</response>
    [HttpPost("{id:int}/banner")]
    [RequireRole("Organiser")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadBanner(int id, IFormFile file)
    {
        var ev = await _db.Events
            .Include(e => e.Venue)
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.EventID == id);
        if (ev == null) return NotFound(new { message = "Event not found." });

        if (ev.OrganiserID != HttpContext.Session.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not own this event." });

        var (image, error) = await ImageFileValidator.InspectAsync(file);
        if (image == null) return BadRequest(new { message = error });

        try
        {
            await using var stream = file.OpenReadStream();
            ev.BannerUrl = await _images.UploadAsync(stream, image.ContentType,
                $"events/{id}/{Guid.NewGuid():N}{image.Extension}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Banner upload failed for event {EventId}", id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Image storage is unavailable. Please try again later." });
        }

        await _db.SaveChangesAsync();
        return Ok(ToDto(ev));
    }

    /// <summary>Deletes an event the logged-in Organiser owns.</summary>
    /// <remarks>The event's categories are deleted with it. An event that already has enrolments cannot be deleted.</remarks>
    /// <param name="id">The event ID.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owning Organiser.</response>
    /// <response code="404">Event does not exist.</response>
    /// <response code="409">The event has enrolments and cannot be deleted.</response>
    [HttpDelete("{id:int}")]
    [RequireRole("Organiser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventID == id);
        if (ev == null) return NotFound(new { message = "Event not found." });

        if (ev.OrganiserID != HttpContext.Session.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not own this event." });

        var hasEnrolments = await _db.Enrolments.AnyAsync(en => en.Category!.EventID == id);
        if (hasEnrolments)
            return Conflict(new { message = "This event has enrolments and cannot be deleted." });

        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string?> ValidateAsync(EventRequestDto dto)
    {
        if (!ValidTypes.Contains(dto.EventType))
            return "EventType must be 'Run', 'Walk' or 'Cycle'.";

        if (!await _db.Venues.AnyAsync(v => v.VenueID == dto.VenueId))
            return "Venue does not exist.";

        return null;
    }

    private static EventResponseDto ToDto(Event e) => new()
    {
        EventID = e.EventID,
        OrganiserID = e.OrganiserID,
        EventName = e.EventName,
        Description = e.Description,
        EventDate = e.EventDate,
        EventType = e.EventType,
        BannerUrl = e.BannerUrl,
        Venue = e.Venue == null ? null : new VenueResponseDto
        {
            VenueID = e.Venue.VenueID,
            VenueName = e.Venue.VenueName,
            Address = e.Venue.Address,
            City = e.Venue.City,
            Province = e.Venue.Province,
            Latitude = e.Venue.Latitude,
            Longitude = e.Venue.Longitude
        },
        Categories = e.Categories.Select(c => new CategoryResponseDto
        {
            CategoryID = c.CategoryID,
            EventID = c.EventID,
            CategoryName = c.CategoryName,
            DistanceKm = c.DistanceKm,
            EntryFee = c.EntryFee,
            MaxParticipants = c.MaxParticipants
        }).ToList()
    };
}
