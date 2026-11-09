using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Attributes;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Extra endpoints added in Part 3. An event needs a venueId (its location), so the web app needs a way to
/// list venues for the event form and to add a new one.
/// </summary>
[ApiController]
[Route("api/venues")]
public class VenuesController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public VenuesController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Lists all venues, so an event's location can be chosen or filtered.</summary>
    /// <remarks>Public. No request body. Ordered by province, city and name.</remarks>
    /// <response code="200">Array of venues.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<VenueResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var venues = await _db.Venues
            .OrderBy(v => v.Province).ThenBy(v => v.City).ThenBy(v => v.VenueName)
            .ToListAsync();

        return Ok(venues.Select(ToDto));
    }

    /// <summary>Adds a new venue.</summary>
    /// <remarks>Organisers only. Expects venueName, city, province and optionally address, latitude and longitude.</remarks>
    /// <response code="201">The created venue.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser.</response>
    [HttpPost]
    [RequireRole("Organiser")]
    [ProducesResponseType(typeof(VenueResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] VenueRequestDto dto)
    {
        var venue = new Venue
        {
            VenueName = dto.VenueName.Trim(),
            Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim(),
            City = dto.City.Trim(),
            Province = dto.Province.Trim(),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude
        };

        _db.Venues.Add(venue);
        await _db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, ToDto(venue));
    }

    private static VenueResponseDto ToDto(Venue v) => new()
    {
        VenueID = v.VenueID,
        VenueName = v.VenueName,
        Address = v.Address,
        City = v.City,
        Province = v.Province,
        Latitude = v.Latitude,
        Longitude = v.Longitude
    };
}
