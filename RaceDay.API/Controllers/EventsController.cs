using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Attributes;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private static readonly string[] ValidTypes = { "Run", "Walk", "Cycle" };
    private readonly RaceDayDbContext _db;

    public EventsController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Returns all upcoming events so participants can browse them.</summary>
    /// <remarks>Public. Only events dated today or later are returned, soonest first. No request body.</remarks>
    /// <response code="200">Array of events, each with its venue and categories.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<EventResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var today = DateTime.Today;
        var events = await _db.Events
            .Include(e => e.Venue)
            .Include(e => e.Categories)
            .Where(e => e.EventDate >= today)
            .OrderBy(e => e.EventDate)
            .ToListAsync();

        return Ok(events.Select(ToDto));
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