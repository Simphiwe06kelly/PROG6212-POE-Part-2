using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Attributes;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public AuthController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Registers a new user as an Organiser or a Participant.</summary>
    /// <remarks>
    /// Expects fullName, email, password (min 6 characters) and role ("Organiser" or "Participant").
    /// The password is hashed with BCrypt and never stored in plain text.
    /// </remarks>
    /// <response code="201">User created. Returns the user (no password).</response>
    /// <response code="400">Validation failed (missing fields or invalid role).</response>
    /// <response code="409">Email is already registered.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (dto.Role != "Organiser" && dto.Role != "Participant")
            return BadRequest(new { message = "Role must be 'Organiser' or 'Participant'." });

        var email = dto.Email.Trim().ToLower();

        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "Email is already registered." });

        var user = new User
        {
            FullName = dto.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var response = new UserResponseDto
        {
            UserID = user.UserID,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };

        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>Logs in with email and password and starts a server-side session.</summary>
    /// <remarks>
    /// On success the session stores the user's ID and role, and a session cookie is returned.
    /// Swagger sends the cookie automatically on later requests.
    /// </remarks>
    /// <response code="200">Login successful. Returns userId and role.</response>
    /// <response code="400">Email or password missing.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var email = dto.Email.Trim().ToLower();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || !IsValidHash(dto.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        HttpContext.Session.SetInt32(RaceDaySession.UserIdKey, user.UserID);
        HttpContext.Session.SetString(RaceDaySession.RoleKey, user.Role);

        return Ok(new LoginResponseDto
        {
            UserId = user.UserID,
            Role = user.Role,
            Message = "Login successful."
        });
    }

    /// <summary>Logs out the current user and clears the session.</summary>
    /// <response code="204">Logged out.</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return NoContent();
    }

    // The seeded users have placeholder hashes (not real BCrypt), so verifying them
    // would throw. Treat those as a failed login instead of crashing.
    private static bool IsValidHash(string password, string hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(password, hash); }
        catch { return false; }
    }
}