using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

public class RaceDayApi : IRaceDayApi
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<RaceDayApi> _logger;

    public RaceDayApi(HttpClient http, ILogger<RaceDayApi> logger)
    {
        _http = http;
        _logger = logger;
    }

    // ---------------- Authentication and profile ----------------

    public Task<ApiResult<UserDto>> RegisterAsync(RegisterRequest request) =>
        SendAsync<UserDto>(HttpMethod.Post, "api/auth/register", request);

    public Task<ApiResult<LoginResultDto>> LoginAsync(string email, string password) =>
        SendAsync<LoginResultDto>(HttpMethod.Post, "api/auth/login", new { email, password });

    public Task<ApiResult> LogoutAsync() => SendNoContentAsync(HttpMethod.Post, "api/auth/logout");

    public Task<ApiResult<UserDto>> GetProfileAsync() => SendAsync<UserDto>(HttpMethod.Get, "api/users/me");

    public Task<ApiResult<UserDto>> UpdateProfileAsync(string fullName, string email) =>
        SendAsync<UserDto>(HttpMethod.Put, "api/users/me", new { fullName, email });

    public Task<ApiResult<UserDto>> UploadProfilePictureAsync(IFormFile file) =>
        UploadAsync<UserDto>("api/users/me/picture", file);

    // ---------------- Venues ----------------

    public Task<ApiResult<List<VenueDto>>> GetVenuesAsync() => SendAsync<List<VenueDto>>(HttpMethod.Get, "api/venues");

    public Task<ApiResult<VenueDto>> CreateVenueAsync(VenueRequest request) =>
        SendAsync<VenueDto>(HttpMethod.Post, "api/venues", request);

    // ---------------- Events ----------------

    public Task<ApiResult<List<EventDto>>> GetEventsAsync(EventFilter filter)
    {
        var query = new List<string>();

        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) query.Add($"{key}={Uri.EscapeDataString(value.Trim())}");
        }

        Add("search", filter.Search);
        Add("type", filter.Type);
        Add("province", filter.Province);
        if (filter.From.HasValue) Add("from", filter.From.Value.ToString("yyyy-MM-dd"));
        if (filter.To.HasValue) Add("to", filter.To.Value.ToString("yyyy-MM-dd"));

        var url = "api/events" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);
        return SendAsync<List<EventDto>>(HttpMethod.Get, url);
    }

    public Task<ApiResult<List<EventDto>>> GetMyEventsAsync() => SendAsync<List<EventDto>>(HttpMethod.Get, "api/events/mine");

    public Task<ApiResult<EventDto>> GetEventAsync(int id) => SendAsync<EventDto>(HttpMethod.Get, $"api/events/{id}");

    public Task<ApiResult<EventDto>> CreateEventAsync(EventRequest request) =>
        SendAsync<EventDto>(HttpMethod.Post, "api/events", request);

    public Task<ApiResult<EventDto>> UpdateEventAsync(int id, EventRequest request) =>
        SendAsync<EventDto>(HttpMethod.Put, $"api/events/{id}", request);

    public Task<ApiResult> DeleteEventAsync(int id) => SendNoContentAsync(HttpMethod.Delete, $"api/events/{id}");

    public Task<ApiResult<EventDto>> UploadBannerAsync(int eventId, IFormFile file) =>
        UploadAsync<EventDto>($"api/events/{eventId}/banner", file);

    // ---------------- Categories ----------------

    public Task<ApiResult<CategoryDto>> CreateCategoryAsync(int eventId, CategoryRequest request) =>
        SendAsync<CategoryDto>(HttpMethod.Post, $"api/events/{eventId}/categories", request);

    public Task<ApiResult<CategoryDto>> UpdateCategoryAsync(int categoryId, CategoryRequest request) =>
        SendAsync<CategoryDto>(HttpMethod.Put, $"api/categories/{categoryId}", request);

    public Task<ApiResult> DeleteCategoryAsync(int categoryId) =>
        SendNoContentAsync(HttpMethod.Delete, $"api/categories/{categoryId}");

    // ---------------- Enrolments ----------------

    public Task<ApiResult<EnrolmentDto>> EnrolAsync(int categoryId) =>
        SendAsync<EnrolmentDto>(HttpMethod.Post, "api/enrolments", new { categoryId });

    public Task<ApiResult<List<EnrolmentDto>>> GetMyEnrolmentsAsync() =>
        SendAsync<List<EnrolmentDto>>(HttpMethod.Get, "api/enrolments/mine");

    public Task<ApiResult<List<EnrolmentDto>>> GetEventEnrolmentsAsync(int eventId) =>
        SendAsync<List<EnrolmentDto>>(HttpMethod.Get, $"api/events/{eventId}/enrolments");

    public Task<ApiResult> CancelEnrolmentAsync(int enrolmentId) =>
        SendNoContentAsync(HttpMethod.Delete, $"api/enrolments/{enrolmentId}");

    // ---------------- Results ----------------

    public Task<ApiResult<ResultDto>> CreateResultAsync(ResultRequest request) =>
        SendAsync<ResultDto>(HttpMethod.Post, "api/results", request);

    public Task<ApiResult<ResultDto>> UpdateResultAsync(int resultId, ResultRequest request) =>
        SendAsync<ResultDto>(HttpMethod.Put, $"api/results/{resultId}", request);

    public Task<ApiResult<List<ResultDto>>> GetEventResultsAsync(int eventId) =>
        SendAsync<List<ResultDto>>(HttpMethod.Get, $"api/events/{eventId}/results");

    public Task<ApiResult<List<ResultDto>>> GetMyResultsAsync() =>
        SendAsync<List<ResultDto>>(HttpMethod.Get, "api/results/mine");

    // ---------------- Plumbing ----------------

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        try
        {
            using var request = new HttpRequestMessage(method, url);
            if (body != null)
            {
                request.Content = JsonContent.Create(body, body.GetType(), null, Json);
            }

            using var response = await _http.SendAsync(request);
            var status = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                return ApiResults.Fail<T>(status, await ReadErrorAsync(response));
            }

            if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
            {
                return ApiResults.Ok<T>(status, default);
            }

            var data = await response.Content.ReadFromJsonAsync<T>(Json);
            return ApiResults.Ok(status, data);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Call to the RaceDay API failed: {Method} {Url}", method, url);
            return ApiResults.Down<T>();
        }
    }

    private async Task<ApiResult> SendNoContentAsync(HttpMethod method, string url)
    {
        try
        {
            using var request = new HttpRequestMessage(method, url);
            using var response = await _http.SendAsync(request);
            var status = (int)response.StatusCode;

            return response.IsSuccessStatusCode
                ? ApiResults.Ok(status)
                : ApiResults.Fail(status, await ReadErrorAsync(response));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Call to the RaceDay API failed: {Method} {Url}", method, url);
            return ApiResults.Down();
        }
    }

    /// <summary>Sends a file to the API as multipart/form-data. The API (not this app) stores it in Azure Blob Storage.</summary>
    private async Task<ApiResult<T>> UploadAsync<T>(string url, IFormFile file)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            await using var stream = file.OpenReadStream();

            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
            content.Add(fileContent, "file", file.FileName);

            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            using var response = await _http.SendAsync(request);
            var status = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                return ApiResults.Fail<T>(status, await ReadErrorAsync(response));
            }

            var data = await response.Content.ReadFromJsonAsync<T>(Json);
            return ApiResults.Ok(status, data);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "File upload to the RaceDay API failed: {Url}", url);
            return ApiResults.Down<T>();
        }
    }

    /// <summary>Turns an API error response into one short, readable sentence (never raw JSON or a stack trace).</summary>
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;

        try
        {
            var text = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                // The API's own errors look like { "message": "..." }
                if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                {
                    return message.GetString()!;
                }

                // Automatic validation errors look like { "errors": { "Field": ["..."] } }
                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    var messages = new List<string>();
                    foreach (var field in errors.EnumerateObject())
                    {
                        if (field.Value.ValueKind != JsonValueKind.Array) continue;
                        foreach (var item in field.Value.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String) messages.Add(item.GetString()!);
                        }
                    }

                    if (messages.Count > 0) return string.Join(" ", messages);
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON: fall through to the default message
        }

        return status switch
        {
            401 => "Please log in to continue.",
            403 => "You don't have permission to do that.",
            404 => "We couldn't find what you were looking for.",
            409 => "That conflicts with something that already exists.",
            >= 300 and < 400 => "The RaceDay service address is not set up correctly.",
            >= 500 => "Something went wrong on our side. Please try again.",
            _ => "Something went wrong. Please check your details and try again."
        };
    }
}
