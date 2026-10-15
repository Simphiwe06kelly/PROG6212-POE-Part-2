using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Attributes;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
public class CategoriesController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Lists all race categories available for a specific event.</summary>
    /// <remarks>Public. No request body.</remarks>
    /// <param name="eventId">The event ID.</param>
    /// <response code="200">Array of categories.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("api/events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(List<CategoryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventID == eventId))
            return NotFound(new { message = "Event not found." });

        var categories = await _db.Categories
            .Where(c => c.EventID == eventId)
            .OrderBy(c => c.DistanceKm)
            .ToListAsync();

        return Ok(categories.Select(ToDto));
    }

    /// <summary>Adds a new race category (e.g. 10km) to an event the Organiser owns.</summary>
    /// <remarks>Expects categoryName, distanceKm, entryFee and maxParticipants.</remarks>
    /// <param name="eventId">The event ID.</param>
    /// <response code="201">The created category.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owning Organiser.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpPost("api/events/{eventId:int}/categories")]
    [RequireRole("Organiser")]
    [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(int eventId, [FromBody] CategoryRequestDto dto)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventID == eventId);
        if (ev == null) return NotFound(new { message = "Event not found." });

        if (ev.OrganiserID != HttpContext.Session.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not own this event." });

        var category = new Category
        {
            EventID = eventId,
            CategoryName = dto.CategoryName.Trim(),
            DistanceKm = dto.DistanceKm,
            EntryFee = dto.EntryFee,
            MaxParticipants = dto.MaxParticipants
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, ToDto(category));
    }

    /// <summary>Updates a category on an event the Organiser owns.</summary>
    /// <param name="id">The category ID.</param>
    /// <response code="200">The updated category.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owning Organiser.</response>
    /// <response code="404">Category does not exist.</response>
    [HttpPut("api/categories/{id:int}")]
    [RequireRole("Organiser")]
    [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] CategoryRequestDto dto)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryID == id);
        if (category == null) return NotFound(new { message = "Category not found." });

        if (category.Event!.OrganiserID != HttpContext.Session.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not own this event." });

        category.CategoryName = dto.CategoryName.Trim();
        category.DistanceKm = dto.DistanceKm;
        category.EntryFee = dto.EntryFee;
        category.MaxParticipants = dto.MaxParticipants;
        await _db.SaveChangesAsync();

        return Ok(ToDto(category));
    }

    /// <summary>Removes a category from an event the Organiser owns.</summary>
    /// <remarks>A category that already has enrolments cannot be deleted.</remarks>
    /// <param name="id">The category ID.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or not the owning Organiser.</response>
    /// <response code="404">Category does not exist.</response>
    /// <response code="409">The category has enrolments and cannot be deleted.</response>
    [HttpDelete("api/categories/{id:int}")]
    [RequireRole("Organiser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryID == id);
        if (category == null) return NotFound(new { message = "Category not found." });

        if (category.Event!.OrganiserID != HttpContext.Session.GetUserId())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not own this event." });

        if (await _db.Enrolments.AnyAsync(en => en.CategoryID == id))
            return Conflict(new { message = "This category has enrolments and cannot be deleted." });

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static CategoryResponseDto ToDto(Category c) => new()
    {
        CategoryID = c.CategoryID,
        EventID = c.EventID,
        CategoryName = c.CategoryName,
        DistanceKm = c.DistanceKm,
        EntryFee = c.EntryFee,
        MaxParticipants = c.MaxParticipants
    };
}