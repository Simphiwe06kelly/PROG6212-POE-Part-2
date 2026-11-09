using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Attributes;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IImageStorage _images;
    private readonly ILogger<UsersController> _logger;

    public UsersController(RaceDayDbContext db, IImageStorage images, ILogger<UsersController> logger)
    {
        _db = db;
        _images = images;
        _logger = logger;
    }

    /// <summary>Returns the profile of the currently logged-in user.</summary>
    /// <remarks>Any logged-in user (Organiser or Participant). No request body.</remarks>
    /// <response code="200">The user's profile (no password), including profilePictureUrl.</response>
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

    /// <summary>Uploads (or replaces) the logged-in user's profile picture.</summary>
    /// <remarks>
    /// Any logged-in user. Send the image as multipart/form-data in a field called "file".
    /// JPG, PNG, GIF or WEBP, up to 5 MB. The API stores the image in Azure Blob Storage and saves its URL
    /// on the user as profilePictureUrl.
    /// </remarks>
    /// <param name="file">The image file.</param>
    /// <response code="200">The updated profile, including profilePictureUrl.</response>
    /// <response code="400">No file, wrong file type, or file larger than 5 MB.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="503">Image storage is unavailable.</response>
    [HttpPost("me/picture")]
    [RequireRole]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadPicture(IFormFile file)
    {
        var userId = HttpContext.Session.GetUserId()!.Value;
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized(new { message = "User no longer exists." });

        var (image, error) = await ImageFileValidator.InspectAsync(file);
        if (image == null) return BadRequest(new { message = error });

        try
        {
            await using var stream = file.OpenReadStream();
            user.ProfilePictureUrl = await _images.UploadAsync(stream, image.ContentType,
                $"profiles/{userId}/{Guid.NewGuid():N}{image.Extension}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Profile picture upload failed for user {UserId}", userId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Image storage is unavailable. Please try again later." });
        }

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
                CreatedAt = u.CreatedAt,
                ProfilePictureUrl = u.ProfilePictureUrl
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
        CreatedAt = u.CreatedAt,
        ProfilePictureUrl = u.ProfilePictureUrl
    };
}
