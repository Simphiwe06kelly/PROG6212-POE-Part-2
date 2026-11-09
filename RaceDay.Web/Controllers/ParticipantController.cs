using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Everything a Participant can do after logging in. Organisers are turned away.</summary>
[SessionAuthorize(Roles.Participant)]
public class ParticipantController : AppController
{
    public ParticipantController(IRaceDayApi api, UserSession currentUser) : base(api, currentUser)
    {
    }

    /// <summary>Enter an event: records the Participant, the event and the chosen category immediately.</summary>
    [HttpPost]
    public async Task<IActionResult> Enrol(int categoryId, int eventId)
    {
        var result = await Api.EnrolAsync(categoryId);
        if (RedirectIfNotAllowed(result) is { } redirect) return redirect;

        if (!result.Success)
        {
            ShowError(result.Error ?? "We couldn't enter you into that category.");
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        ShowSuccess("You're in! Your entry has been confirmed.");
        return RedirectToAction(nameof(MyEnrolments));
    }

    [HttpGet]
    public async Task<IActionResult> MyEnrolments()
    {
        var result = await Api.GetMyEnrolmentsAsync();
        if (RedirectIfNotAllowed(result) is { } redirect) return redirect;

        if (!result.Success) ShowError(result.Error ?? "We couldn't load your enrolments.");

        return View(new MyEnrolmentsViewModel { Enrolments = result.Data ?? new List<EnrolmentDto>() });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await Api.CancelEnrolmentAsync(id);
        if (RedirectIfNotAllowed(result) is { } redirect) return redirect;

        if (result.Success) ShowSuccess("Your entry has been cancelled.");
        else ShowError(result.Error ?? "We couldn't cancel that entry.");

        return RedirectToAction(nameof(MyEnrolments));
    }

    [HttpGet]
    public async Task<IActionResult> MyResults()
    {
        var result = await Api.GetMyResultsAsync();
        if (RedirectIfNotAllowed(result) is { } redirect) return redirect;

        if (!result.Success) ShowError(result.Error ?? "We couldn't load your results.");

        return View(new MyResultsViewModel { Results = result.Data ?? new List<ResultDto>() });
    }
}
