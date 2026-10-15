using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Attributes;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
public class ResultsController : ControllerBase
{
    private static readonly string[] ValidStatuses = { "Finished", "DNF", "DQ" };
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Captures a finish-time result for a participant's enrolment.</summary>
    /// <remarks>
    /// Expects enrolmentId, finishTime (hh:mm:ss), overallPosition, categoryPosition and status (Finished, DNF or DQ).
    /// finishTime is required when status is Finished. Only the Organiser who owns the event can capture its results.
    /// </remarks>
    /// <response code="201">The created result.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owning Organiser.</response>
    /// <response code="404">Enrolment does not exist.</response>
    /// <response code="409">A result has already been captured for this enrolment.</response>
    [HttpPost("api/results")]
    [RequireRole("Organiser")]
    [ProducesResponseType(typeof(ResultResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] ResultCreateDto dto)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Category).ThenInclude(c => c!.Event)
            .FirstOrDefaultAsync(e => e.EnrolmentID == dto.EnrolmentId);
        if (enrolment == null) return NotFound(new { message = "Enrolment not found." });

        if (enrolment.Category!.Event!.OrganiserID != HttpContext.Session.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not own this event." });

        if (await _db.Results.AnyAsync(r => r.EnrolmentID == dto.EnrolmentId))
            return Conflict(new { message = "A result has already been captured for this enrolment." });

        if (enrolment.Status != "Confirmed")
            return BadRequest(new { message = "Results can only be captured for confirmed enrolments." });

        var error = Validate(dto.Status, dto.FinishTime);
        if (error != null) return BadRequest(new { message = error });

        var result = new Result
        {
            EnrolmentID = dto.EnrolmentId,
            FinishTime = dto.FinishTime,
            OverallPosition = dto.OverallPosition,
            CategoryPosition = dto.CategoryPosition,
            Status = dto.Status
        };

        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var saved = await Query().FirstAsync(r => r.ResultID == result.ResultID);
        return StatusCode(StatusCodes.Status201Created, ToDto(saved));
    }

    /// <summary>Corrects a previously captured result.</summary>
    /// <remarks>Expects finishTime (hh:mm:ss), overallPosition, categoryPosition and status. Only the owning Organiser can correct it.</remarks>
    /// <param name="id">The result ID.</param>
    /// <response code="200">The updated result.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owning Organiser.</response>
    /// <response code="404">Result does not exist.</response>
    [HttpPut("api/results/{id:int}")]
    [RequireRole("Organiser")]
    [ProducesResponseType(typeof(ResultResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] ResultUpdateDto dto)
    {
        var result = await Query().FirstOrDefaultAsync(r => r.ResultID == id);
        if (result == null) return NotFound(new { message = "Result not found." });

        if (result.Enrolment!.Category!.Event!.OrganiserID != HttpContext.Session.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not own this event." });

        var error = Validate(dto.Status, dto.FinishTime);
        if (error != null) return BadRequest(new { message = error });

        result.FinishTime = dto.FinishTime;
        result.OverallPosition = dto.OverallPosition;
        result.CategoryPosition = dto.CategoryPosition;
        result.Status = dto.Status;
        await _db.SaveChangesAsync();

        return Ok(ToDto(result));
    }

    /// <summary>Returns the full results list for an event, for public leaderboard display.</summary>
    /// <remarks>Public. No request body. Ordered by overall position, with unplaced results last.</remarks>
    /// <param name="eventId">The event ID.</param>
    /// <response code="200">Array of results.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("api/events/{eventId:int}/results")]
    [ProducesResponseType(typeof(List<ResultResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventID == eventId))
            return NotFound(new { message = "Event not found." });

        var list = await Query()
            .Where(r => r.Enrolment!.Category!.EventID == eventId)
            .OrderBy(r => r.OverallPosition == null)
            .ThenBy(r => r.OverallPosition)
            .ToListAsync();

        return Ok(list.Select(ToDto));
    }

    /// <summary>Returns the logged-in Participant's own personal result history.</summary>
    /// <remarks>No request body.</remarks>
    /// <response code="200">Array of results.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not a Participant.</response>
    [HttpGet("api/results/mine")]
    [RequireRole("Participant")]
    [ProducesResponseType(typeof(List<ResultResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Mine()
    {
        var userId = HttpContext.Session.GetUserId()!.Value;
        var list = await Query()
            .Where(r => r.Enrolment!.ParticipantID == userId)
            .ToListAsync();

        return Ok(list.Select(ToDto));
    }

    private static string? Validate(string status, TimeSpan? finishTime)
    {
        if (!ValidStatuses.Contains(status))
            return "Status must be 'Finished', 'DNF' or 'DQ'.";

        if (status == "Finished" && finishTime == null)
            return "FinishTime is required when the status is 'Finished'.";

        return null;
    }

    private IQueryable<Result> Query() => _db.Results
        .Include(r => r.Enrolment).ThenInclude(e => e!.Participant)
        .Include(r => r.Enrolment).ThenInclude(e => e!.Category).ThenInclude(c => c!.Event);

    private static ResultResponseDto ToDto(Result r) => new()
    {
        ResultID = r.ResultID,
        EnrolmentID = r.EnrolmentID,
        ParticipantName = r.Enrolment?.Participant?.FullName ?? string.Empty,
        EventID = r.Enrolment?.Category?.EventID ?? 0,
        EventName = r.Enrolment?.Category?.Event?.EventName ?? string.Empty,
        CategoryName = r.Enrolment?.Category?.CategoryName ?? string.Empty,
        FinishTime = r.FinishTime,
        OverallPosition = r.OverallPosition,
        CategoryPosition = r.CategoryPosition,
        Status = r.Status
    };
}