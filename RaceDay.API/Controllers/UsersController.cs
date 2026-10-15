using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Attributes;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public UsersController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Returns the profile of the currently logged-in user.</summary>
    /// <remarks>Any logged-in user (Organiser or Participant). No request body.</remarks>
    /// <response code="200">The user's profile (no password).</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet("me")]
    [RequireRole]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe()
    {
        var userId = HttpContext.Session.GetUserId()!.Value;
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized(new { message = "User no longer exists." });

        return Ok(ToDto(user));
    }

    /// <summary>Updates the logged-in user's own profile.</summary>
    /// <remarks>Expects fullName and email. The email must not belong to another user.</remarks>
    /// <response code="200">The updated profile.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="409">Email is already used by another account.</response>
    [HttpPut("me")]
    [RequireRole]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileDto dto)
    {
        var userId = HttpContext.Session.GetUserId()!.Value;
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized(new { message = "User no longer exists." });

        var email = dto.Email.Trim().ToLower();
        if (await _db.Users.AnyAsync(u => u.Email == email && u.UserID != userId))
            return Conflict(new { message = "Email is already used by another account." });

        user.FullName = dto.FullName.Trim();
        user.Email = email;
        await _db.SaveChangesAsync();

        return Ok(ToDto(user));
    }

    /// <summary>Lists all registered users (administrative oversight).</summary>
    /// <remarks>Organisers only. No request body.</remarks>
    /// <response code="200">Array of users (no passwords).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser.</response>
    [HttpGet]
    [RequireRole("Organiser")]
    [ProducesResponseType(typeof(List<UserResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.Users
            .OrderBy(u => u.UserID)
            .Select(u => new UserResponseDto
            {
                UserID = u.UserID,
                FullName = u.FullName,
                Email = u.Email,
                Role = u.Role,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    private static UserResponseDto ToDto(Models.User u) => new()
    {
        UserID = u.UserID,
        FullName = u.FullName,
        Email = u.Email,
        Role = u.Role,
        CreatedAt = u.CreatedAt
    };
}