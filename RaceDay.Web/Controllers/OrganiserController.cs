using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Helpers;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>
/// Everything an Organiser can do: dashboard, events (with banner upload), categories, enrolments, results and venues.
/// Participants are turned away. The API checks the role and event ownership again on every call.
/// </summary>
[SessionAuthorize(Roles.Organiser)]
public class OrganiserController : AppController
{
    private const string GenericError = "Something went wrong. Please try again.";

    public OrganiserController(IRaceDayApi api, UserSession currentUser) : base(api, currentUser)
    {
    }

    // ======================= Dashboard =======================

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var result = await Api.GetMyEventsAsync();
        if (RedirectIfNotAllowed(result) is { } redirect) return redirect;

        var events = result.Data ?? new List<EventDto>();
        var today = DateTime.Today;

        return View(new DashboardViewModel
        {
            Events = events,
            Upcoming = events.Where(e => e.EventDate.Date >= today).OrderBy(e => e.EventDate).ToList(),
            TotalEnrolments = events.Sum(e => e.EnrolmentCount ?? 0),
            LoadError = result.Success ? null : result.Error
        });
    }

    // ======================= Events =======================

    [HttpGet]
    public async Task<IActionResult> Events()
    {
        var result = await Api.GetMyEventsAsync();
        if (RedirectIfNotAllowed(result) is { } redirect) return redirect;

        if (!result.Success) ShowError(result.Error ?? GenericError);
        return View(result.Data ?? new List<EventDto>());
    }

    [HttpGet]
    public async Task<IActionResult> CreateEvent()
    {
        var model = new EventFormViewModel { EventDate = DateTime.Today.AddDays(30) };
        await FillVenuesAsync(model);
        return View("EventForm", model);
    }

    [HttpPost]
    public async Task<IActionResult> CreateEvent(EventFormViewModel model)
    {
        await FillVenuesAsync(model);
        if (!ModelState.IsValid) return View("EventForm", model);

        var result = await Api.CreateEventAsync(ToRequest(model));
        if (RedirectIfNotAllowed(result) is { } redirect) return redirect;

        if (!result.Success || result.Data == null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? GenericError);
            return View("EventForm", model);
        }

        // The banner is sent to the API too; the API stores it in Azure Blob Storage
        if (model.Banner is { Length: > 0 })
        {
            var banner = await Api.UploadBannerAsync(result.Data.EventID, model.Banner);
            if (!banner.Success)
            {
                ShowError($"The event was created, but its banner could not be uploaded: {banner.Error}");
                return RedirectToAction(nameof(EditEvent), new { id = result.Data.EventID });
            }
        }

        ShowSuccess("Event created. Now add the race categories so participants can enter.");
        return RedirectToAction(nameof(Categories), new { id = result.Data.EventID });
    }

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        var result = await Api.GetEventAsync(id);
        if (result.StatusCode == 404) return NotFound();
        if (RedirectIfNotAllowed(result) is { } redirect) return redirect;

        if (!result.Success || result.Data == null)
        {
            ShowError(result.Error ?? GenericError);
            return RedirectToAction(nameof(Events));
        }

        if (result.Data.OrganiserID != CurrentUser.UserId) return RedirectToAction("AccessDenied", "Account");

        var model = new EventFormViewModel
        {
            EventId = result.Data.EventID,
            EventName = result.Data.EventName,
            Description = result.Data.Description,
            EventDate = result.Data.EventDate.Date,
            EventType = result.Data.EventType,
            VenueId = result.Data.Venue?.VenueID ?? 0,
            CurrentBannerUrl = result.Data.BannerUrl
        };
        await FillVenuesAsync(model);
        return View("EventForm", model);
    }

    [HttpPost]
    public async Task<IActionResult> EditEvent(int id, EventFormViewModel model)
    {
        model.EventId = id;
        await FillVenuesAsync(model);
        if (!ModelState.IsValid) return View("EventForm", model);

        var result = await Api.UpdateEventAsync(id, ToRequest(model));
        if (RedirectIfNotAllowed(result) is { } redirect) return redirect;

        if (!result.Success || result.Data == null)
        {
            model.CurrentBannerUrl = (await Api.GetEventAsync(id)).Data?.BannerUrl;
            ModelState.AddModelError(string.Empty, result.Error ?? GenericError);
            return View("EventForm", model);
        }

        if (model.Banner is { Length: > 0 })
        {
            var banner = await Api.UploadBannerAsync(id, model.Banner);
            if (!banner.Success)
            {
                ShowError($"Your changes were saved, but the banner could not be uploaded: {banner.Error}");
                return RedirectToAction(nameof(EditEvent), new { id });
            }
        }

        ShowSuccess("Event updated.");
        return RedirectToAction(nameof(Events));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var result = await Api.DeleteEventAsync(id);
        if (RedirectIfNotAllowed(result) is { } redirect) return redirect;

        if (result.Success) ShowSuccess("Event deleted.");
        else ShowError(result.Error ?? GenericError);

        return RedirectToAction(nameof(Events));
    }

    // ======================= Categories =======================

    [HttpGet]
    public async Task<IActionResult> Categories(int id)
    {
        var ev = await LoadOwnedEventAsync(id);
        if (ev.Redirect != null) return ev.Redirect;

        return View(new CategoriesViewModel
        {
            Event = ev.Event!,
            NewCategory = new CategoryFormViewModel { EventId = id }
        });
    }

    /// <summary>Adds a category (CategoryId = 0) or updates an existing one.</summary>
    [HttpPost]
    public async Task<IActionResult> SaveCategory(CategoryFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ShowError(ModelErrors());
            return RedirectToAction(nameof(Categories), new { id = model.EventId });
        }

        var request = new CategoryRequest(model.CategoryName.Trim(), model.DistanceKm, model.EntryFee, model.MaxParticipants);
        var result = model.CategoryId == 0
            ? await Api.CreateCategoryAsync(model.EventId, request)
            : await Api.UpdateCategoryAsync(model.CategoryId, request);
        if (RedirectIfNotAllowed(result) is { } redirect) return redirect;

        if (result.Success) ShowSuccess(model.CategoryId == 0 ? "Category added." : "Category updated.");
        else ShowError(result.Error ?? GenericError);

        return RedirectToAction(nameof(Categories), new { id = model.EventId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCategory(int eventId, int categoryId)
    {
        var result = await Api.DeleteCategoryAsync(categoryId);
        if (RedirectIfNotAllowed(result) is { } redirect) return redirect;

        if (result.Success) ShowSuccess("Category deleted.");
        else ShowError(result.Error ?? GenericError);

        return RedirectToAction(nameof(Categories), new { id = eventId });
    }

    // ======================= Enrolments =======================

    [HttpGet]
    public async Task<IActionResult> Enrolments(int id)
    {
        var ev = await LoadOwnedEventAsync(id);
        if (ev.Redirect != null) return ev.Redirect;

        var enrolments = await Api.GetEventEnrolmentsAsync(id);
        if (RedirectIfNotAllowed(enrolments) is { } redirect) return redirect;
        if (!enrolments.Success) ShowError(enrolments.Error ?? GenericError);

        return View(new EnrolmentsViewModel
        {
            Event = ev.Event!,
            Enrolments = enrolments.Data ?? new List<EnrolmentDto>()
        });
    }

    // ======================= Results =======================

    [HttpGet]
    public async Task<IActionResult> Results(int id)
    {
        var ev = await LoadOwnedEventAsync(id);
        if (ev.Redirect != null) return ev.Redirect;

        var enrolments = await Api.GetEventEnrolmentsAsync(id);
        if (RedirectIfNotAllowed(enrolments) is { } redirect) return redirect;
        if (!enrolments.Success) ShowError(enrolments.Error ?? GenericError);

        var results = (await Api.GetEventResultsAsync(id)).Data ?? new List<ResultDto>();

        // One row per CONFIRMED enrolment, filled in with the result if one has been captured
        var rows = (enrolments.Data ?? new List<EnrolmentDto>())
            .Where(e => e.Status == "Confirmed")
            .Select(e =>
            {
                var existing = results.FirstOrDefault(r => r.EnrolmentID == e.EnrolmentID);
                return new ResultRow
                {
                    EnrolmentId = e.EnrolmentID,
                    ResultId = existing?.ResultID ?? 0,
                    ParticipantName = e.ParticipantName,
                    CategoryName = e.CategoryName,
                    FinishTime = existing?.FinishTime.HasValue == true ? Format.Time(existing.FinishTime) : null,
                    OverallPosition = existing?.OverallPosition,
                    CategoryPosition = existing?.CategoryPosition,
                    Status = existing?.Status ?? "Finished"
                };
            })
            .OrderBy(r => r.CategoryName)
            .ThenBy(r => r.OverallPosition ?? int.MaxValue)
            .ThenBy(r => r.ParticipantName)
            .ToList();

        return View(new ResultsEntryViewModel { Event = ev.Event!, Rows = rows });
    }

    /// <summary>Captures a new result (ResultId = 0) or corrects an existing one.</summary>
    [HttpPost]
    public async Task<IActionResult> SaveResult(ResultFormViewModel model)
    {
        var backToResults = RedirectToAction(nameof(Results), new { id = model.EventId });

        if (!ModelState.IsValid)
        {
            ShowError(ModelErrors());
            return backToResults;
        }

        TimeSpan? finishTime = null;
        if (!string.IsNullOrWhiteSpace(model.FinishTime))
        {
            if (!Format.TryParseFinishTime(model.FinishTime, out var parsed))
            {
                ShowError("Enter the finish time as hh:mm:ss (for example 01:02:45) or mm:ss (for example 22:14).");
                return backToResults;
            }

            finishTime = parsed;
        }

        if (model.Status == "Finished" && finishTime == null)
        {
            ShowError("A finish time is needed when the status is Finished. Use DNF or DQ if they did not finish.");
            return backToResults;
        }

        var request = new ResultRequest(model.EnrolmentId, finishTime, model.OverallPosition, model.CategoryPosition, model.Status);
        var result = model.ResultId == 0
            ? await Api.CreateResultAsync(request)
            : await Api.UpdateResultAsync(model.ResultId, request);
        if (RedirectIfNotAllowed(result) is { } redirect) return redirect;

        if (result.Success) ShowSuccess(model.ResultId == 0 ? "Result saved." : "Result corrected.");
        else ShowError(result.Error ?? GenericError);

        return backToResults;
    }

    // ======================= Venues =======================

    [HttpGet]
    public async Task<IActionResult> Venues()
    {
        var result = await Api.GetVenuesAsync();
        if (!result.Success) ShowError(result.Error ?? GenericError);

        return View(new VenuesViewModel { Venues = result.Data ?? new List<VenueDto>() });
    }

    [HttpPost]
    public async Task<IActionResult> CreateVenue(VenueFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ShowError(ModelErrors());
            return RedirectToAction(nameof(Venues));
        }

        var result = await Api.CreateVenueAsync(new VenueRequest(
            model.VenueName.Trim(),
            string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim(),
            model.City.Trim(),
            model.Province.Trim()));
        if (RedirectIfNotAllowed(result) is { } redirect) return redirect;

        if (result.Success) ShowSuccess("Venue added. You can now choose it when you create an event.");
        else ShowError(result.Error ?? GenericError);

        return RedirectToAction(nameof(Venues));
    }

    // ======================= Helpers =======================

    private async Task FillVenuesAsync(EventFormViewModel model)
    {
        var venues = await Api.GetVenuesAsync();
        model.Venues = venues.Data ?? new List<VenueDto>();
    }

    private static EventRequest ToRequest(EventFormViewModel model) => new(
        model.EventName.Trim(),
        string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
        model.EventDate!.Value.Date,
        model.EventType,
        model.VenueId);

    /// <summary>Loads an event and checks it belongs to the logged-in Organiser. Returns a redirect when it should not be shown.</summary>
    private async Task<(EventDto? Event, IActionResult? Redirect)> LoadOwnedEventAsync(int id)
    {
        var result = await Api.GetEventAsync(id);
        if (result.StatusCode == 404) return (null, NotFound());
        if (RedirectIfNotAllowed(result) is { } redirect) return (null, redirect);

        if (!result.Success || result.Data == null)
        {
            ShowError(result.Error ?? GenericError);
            return (null, RedirectToAction(nameof(Events)));
        }

        if (result.Data.OrganiserID != CurrentUser.UserId)
        {
            return (null, RedirectToAction("AccessDenied", "Account"));
        }

        return (result.Data, null);
    }
}
