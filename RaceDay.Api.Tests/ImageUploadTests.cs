using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Api.Tests.Helpers;
using Xunit;

namespace RaceDay.Api.Tests;

public class ImageUploadTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ImageUploadTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ---------- Event banner ----------

    [Fact]
    public async Task UploadBanner_AsOwningOrganiser_Returns200_AndSavesTheUrlOnTheEvent()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var eventId = await TestHelper.CreateEventAsync(organiser);

        var response = await organiser.PostAsync($"/api/events/{eventId}/banner", ImageTestData.Content(ImageTestData.Png));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var url = json.GetProperty("bannerUrl").GetString();
        Assert.StartsWith("https://fake-storage.test/", url);
        Assert.Contains($"events/{eventId}/", url);
        Assert.EndsWith(".png", url);

        // The URL is stored in the database, so it comes back when the event is fetched again
        var detail = await (await organiser.GetAsync($"/api/events/{eventId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(url, detail.GetProperty("bannerUrl").GetString());

        // ...and the file really was sent to storage
        Assert.Contains(_factory.Storage.Uploads, u => u.BlobName.StartsWith($"events/{eventId}/") && u.ContentType == "image/png");
    }

    [Fact]
    public async Task UploadBanner_FileTypeIsDetectedFromTheFile_NotFromTheNameOrClaimedType()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var eventId = await TestHelper.CreateEventAsync(organiser);

        // A real JPEG sent with a misleading name and content type
        var response = await organiser.PostAsync($"/api/events/{eventId}/banner",
            ImageTestData.Content(ImageTestData.Jpeg, "banner.png", "image/png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.EndsWith(".jpg", json.GetProperty("bannerUrl").GetString());
    }

    [Fact]
    public async Task UploadBanner_ForAnotherOrganisersEvent_Returns403()
    {
        var owner = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var eventId = await TestHelper.CreateEventAsync(owner);
        var intruder = await TestHelper.LoginAsAsync(_factory, "Organiser");

        var response = await intruder.PostAsync($"/api/events/{eventId}/banner", ImageTestData.Content(ImageTestData.Png));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_AsParticipant_Returns403()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var eventId = await TestHelper.CreateEventAsync(organiser);
        var participant = await TestHelper.LoginAsAsync(_factory, "Participant");

        var response = await participant.PostAsync($"/api/events/{eventId}/banner", ImageTestData.Content(ImageTestData.Png));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_WhenNotLoggedIn_Returns401()
    {
        var anonymous = TestHelper.NewClient(_factory);

        var response = await anonymous.PostAsync("/api/events/1/banner", ImageTestData.Content(ImageTestData.Png));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_ForAnUnknownEvent_Returns404()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");

        var response = await organiser.PostAsync("/api/events/999999/banner", ImageTestData.Content(ImageTestData.Png));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_WithATextFileDisguisedAsAnImage_Returns400()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var eventId = await TestHelper.CreateEventAsync(organiser);

        var response = await organiser.PostAsync($"/api/events/{eventId}/banner",
            ImageTestData.Content(ImageTestData.NotAnImage, "fake.png", "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_LargerThan5MB_Returns400()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var eventId = await TestHelper.CreateEventAsync(organiser);

        var response = await organiser.PostAsync($"/api/events/{eventId}/banner",
            ImageTestData.Content(ImageTestData.OversizedPng()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_WithNoFile_Returns400()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var eventId = await TestHelper.CreateEventAsync(organiser);

        var response = await organiser.PostAsync($"/api/events/{eventId}/banner", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Profile picture ----------

    [Theory]
    [InlineData("Participant")]
    [InlineData("Organiser")]
    public async Task UploadPicture_AsAnyLoggedInUser_Returns200_AndTheProfileShowsTheUrl(string role)
    {
        var user = await TestHelper.LoginAsAsync(_factory, role);

        var response = await user.PostAsync("/api/users/me/picture", ImageTestData.Content(ImageTestData.Jpeg, "me.jpg", "image/jpeg"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var uploaded = await response.Content.ReadFromJsonAsync<JsonElement>();
        var url = uploaded.GetProperty("profilePictureUrl").GetString();
        Assert.StartsWith("https://fake-storage.test/", url);
        Assert.Contains("profiles/", url);

        var profile = await (await user.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(url, profile.GetProperty("profilePictureUrl").GetString());
    }

    [Fact]
    public async Task UploadPicture_WhenNotLoggedIn_Returns401()
    {
        var anonymous = TestHelper.NewClient(_factory);

        var response = await anonymous.PostAsync("/api/users/me/picture", ImageTestData.Content(ImageTestData.Png));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadPicture_WithANonImage_Returns400()
    {
        var user = await TestHelper.LoginAsAsync(_factory, "Participant");

        var response = await user.PostAsync("/api/users/me/picture",
            ImageTestData.Content(ImageTestData.NotAnImage, "me.png", "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NewUsersAndEvents_HaveNoImageUntilOneIsUploaded()
    {
        var organiser = await TestHelper.LoginAsAsync(_factory, "Organiser");
        var eventId = await TestHelper.CreateEventAsync(organiser);

        var detail = await (await organiser.GetAsync($"/api/events/{eventId}")).Content.ReadFromJsonAsync<JsonElement>();
        var profile = await (await organiser.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(JsonValueKind.Null, detail.GetProperty("bannerUrl").ValueKind);
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("profilePictureUrl").ValueKind);
    }
}
