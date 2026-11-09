using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

/// <summary>
/// Everything the web app can ask of the RaceDay API. The MVC project has no database and no business rules of
/// its own: every page gets its data from, and sends its changes to, these calls.
/// </summary>
public interface IRaceDayApi
{
    // Authentication and profile
    Task<ApiResult<UserDto>> RegisterAsync(RegisterRequest request);
    Task<ApiResult<LoginResultDto>> LoginAsync(string email, string password);
    Task<ApiResult> LogoutAsync();
    Task<ApiResult<UserDto>> GetProfileAsync();
    Task<ApiResult<UserDto>> UpdateProfileAsync(string fullName, string email);
    Task<ApiResult<UserDto>> UploadProfilePictureAsync(IFormFile file);

    // Venues
    Task<ApiResult<List<VenueDto>>> GetVenuesAsync();
    Task<ApiResult<VenueDto>> CreateVenueAsync(VenueRequest request);

    // Events
    Task<ApiResult<List<EventDto>>> GetEventsAsync(EventFilter filter);
    Task<ApiResult<List<EventDto>>> GetMyEventsAsync();
    Task<ApiResult<EventDto>> GetEventAsync(int id);
    Task<ApiResult<EventDto>> CreateEventAsync(EventRequest request);
    Task<ApiResult<EventDto>> UpdateEventAsync(int id, EventRequest request);
    Task<ApiResult> DeleteEventAsync(int id);
    Task<ApiResult<EventDto>> UploadBannerAsync(int eventId, IFormFile file);

    // Categories
    Task<ApiResult<CategoryDto>> CreateCategoryAsync(int eventId, CategoryRequest request);
    Task<ApiResult<CategoryDto>> UpdateCategoryAsync(int categoryId, CategoryRequest request);
    Task<ApiResult> DeleteCategoryAsync(int categoryId);

    // Enrolments
    Task<ApiResult<EnrolmentDto>> EnrolAsync(int categoryId);
    Task<ApiResult<List<EnrolmentDto>>> GetMyEnrolmentsAsync();
    Task<ApiResult<List<EnrolmentDto>>> GetEventEnrolmentsAsync(int eventId);
    Task<ApiResult> CancelEnrolmentAsync(int enrolmentId);

    // Results
    Task<ApiResult<ResultDto>> CreateResultAsync(ResultRequest request);
    Task<ApiResult<ResultDto>> UpdateResultAsync(int resultId, ResultRequest request);
    Task<ApiResult<List<ResultDto>>> GetEventResultsAsync(int eventId);
    Task<ApiResult<List<ResultDto>>> GetMyResultsAsync();
}
