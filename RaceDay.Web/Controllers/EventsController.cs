using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Full event information, available categories and (once finished) the results. Public.</summary>
public class EventsController : AppController
{
    public EventsController(IRaceDayApi api, UserSession currentUser) : base(api, currentUser)
    {
    }

    public async Task<IActionResult> Details(int id)
    {
        var ev = await Api.GetEventAsync(id);
        if (ev.StatusCode == 404) return NotFound();

        if (!ev.Success || ev.Data == null)
        {
            ShowError(ev.Error ?? "We couldn't load that event.");
            return RedirectToAction("Index", "Home");
        }

        var model = new EventDetailsViewModel { Event = ev.Data };

        // A logged-in Participant sees which categories they have already entered
        if (CurrentUser.IsParticipant)
        {
            var mine = await Api.GetMyEnrolmentsAsync();
            if (RedirectIfNotAllowed(mine) is { } redirect) return redirect;

            model.EnrolledCategoryIds = (mine.Data ?? new List<EnrolmentDto>())
                .Where(e => e.Status == "Confirmed")
                .Select(e => e.CategoryID)
                .ToHashSet();
        }

        var results = await Api.GetEventResultsAsync(id);
        model.Results = results.Data ?? new List<ResultDto>();

        return View(model);
    }
}
