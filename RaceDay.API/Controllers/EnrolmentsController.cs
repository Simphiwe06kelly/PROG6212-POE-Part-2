using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Attributes;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
public class EnrolmentsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Enrols the logged-in Participant into a chosen category.</summary>
    /// <remarks>
    /// Expects categoryId. The link between the participant, the event (through the category) and the category is recorded.
    /// A category that is full, or belongs to an event that has already taken place, is rejected.
    /// A previously cancelled enrolment is re-activated.
    /// </remarks>
    /// <response code="201">The enrolment.</response>
    /// <response code="400">Validation failed (category full or event already past).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not a Participant.</response>
    /// <response code="404">Category does not exist.</response>
    /// <response code="409">Already enrolled in this category.</response>
    [HttpPost("api/enrolments")]
    [RequireRole("Participant")]
    [ProducesResponseType(typeof(EnrolmentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol([FromBody] EnrolmentRequestDto dto)
    {
        var userId = HttpContext.Session.GetUserId()!.Value;

        var category = await _db.Categories.Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.CategoryID == dto.CategoryId);
        if (category == null) return NotFound(new { message = "Category not found." });

        var existing = await _db.Enrolments
            .FirstOrDefaultAsync(e => e.ParticipantID == userId && e.CategoryID == dto.CategoryId);
        if (existing != null && existing.Status == "Confirmed")
            return Conflict(new { message = "You are already enrolled in this category." });

        if (category.Event!.EventDate < DateTime.Today)
            return BadRequest(new { message = "This event has already taken place." });

        var confirmedCount = await _db.Enrolments
            .CountAsync(e => e.CategoryID == dto.CategoryId && e.Status == "Confirmed");
        if (confirmedCount >= category.MaxParticipants)
            return BadRequest(new { message = "This category is full." });

        Enrolment enrolment;
        if (existing != null) // re-activate a cancelled enrolment (unique index allows only one row per participant+category)
        {
            existing.Status = "Confirmed";
            existing.EnrolmentDate = DateTime.Now;
            enrolment = existing;
        }
        else
        {
            enrolment = new Enrolment { ParticipantID = userId, CategoryID = dto.CategoryId };
            _db.Enrolments.Add(enrolment);
        }

        await _db.SaveChangesAsync();

        var saved = await Query().FirstAsync(e => e.EnrolmentID == enrolment.EnrolmentID);
        return StatusCode(StatusCodes.Status201Created, ToDto(saved));
    }

    /// <summary>Returns the logged-in Participant's own enrolment history.</summary>
    /// <remarks>No request body. Includes cancelled enrolments.</remarks>
    /// <response code="200">Array of enrolments.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not a Participant.</response>
    [HttpGet("api/enrolments/mine")]
    [RequireRole("Participant")]
    [ProducesResponseType(typeof(List<EnrolmentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Mine()
    {
        var userId = HttpContext.Session.GetUserId()!.Value;
        var list = await Query()
            .Where(e => e.ParticipantID == userId)
            .OrderByDescending(e => e.EnrolmentDate)
            .ToListAsync();

        return Ok(list.Select(ToDto));
    }

    /// <summary>Returns every enrolment for an event the Organiser owns, for roster management.</summary>
    /// <param name="eventId">The event ID.</param>
    /// <response code="200">Array of enrolments.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owning Organiser.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("api/events/{eventId:int}/enrolments")]
    [RequireRole("Organiser")]
    [ProducesResponseType(typeof(List<EnrolmentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ForEvent(int eventId)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventID == eventId);
        if (ev == null) return NotFound(new { message = "Event not found." });

        if (ev.OrganiserID != HttpContext.Session.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not own this event." });

        var list = await Query()
            .Where(e => e.Category!.EventID == eventId)
            .OrderBy(e => e.CategoryID).ThenBy(e => e.EnrolmentDate)
            .ToListAsync();

        return Ok(list.Select(ToDto));
    }

    /// <summary>Cancels the logged-in Participant's own enrolment.</summary>
    /// <remarks>The enrolment is kept for history and its status is set to Cancelled.</remarks>
    /// <param name="id">The enrolment ID.</param>
    /// <response code="204">Cancelled.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not a Participant, or not the owning Participant.</response>
    /// <response code="404">Enrolment does not exist.</response>
    [HttpDelete("api/enrolments/{id:int}")]
    [RequireRole("Participant")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(int id)
    {
        var enrolment = await _db.Enrolments.FirstOrDefaultAsync(e => e.EnrolmentID == id);
        if (enrolment == null) return NotFound(new { message = "Enrolment not found." });

        if (enrolment.ParticipantID != HttpContext.Session.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "This is not your enrolment." });

        enrolment.Status = "Cancelled";
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private IQueryable<Enrolment> Query() => _db.Enrolments
        .Include(e => e.Participant)
        .Include(e => e.Category).ThenInclude(c => c!.Event);

    private static EnrolmentResponseDto ToDto(Enrolment e) => new()
    {
        EnrolmentID = e.EnrolmentID,
        ParticipantID = e.ParticipantID,
        ParticipantName = e.Participant?.FullName ?? string.Empty,
        CategoryID = e.CategoryID,
        CategoryName = e.Category?.CategoryName ?? string.Empty,
        EventID = e.Category?.EventID ?? 0,
        EventName = e.Category?.Event?.EventName ?? string.Empty,
        EventDate = e.Category?.Event?.EventDate ?? default,
        EnrolmentDate = e.EnrolmentDate,
        Status = e.Status
    };
}