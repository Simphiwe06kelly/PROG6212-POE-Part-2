using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class HomeController : AppController
{
    public HomeController(IRaceDayApi api, UserSession currentUser) : base(api, currentUser)
    {
    }

    /// <summary>Home page: all upcoming events, with filters. Public.</summary>
    public async Task<IActionResult> Index([FromQuery] EventFilter filter)
    {
        var events = await Api.GetEventsAsync(filter);
        var venues = await Api.GetVenuesAsync();

        var model = new HomeViewModel
        {
            Filter = filter,
            Events = events.Data ?? new List<EventDto>(),
            Provinces = (venues.Data ?? new List<VenueDto>())
                .Select(v => v.Province)
                .Distinct()
                .OrderBy(p => p)
                .ToList(),
            LoadError = events.Success ? null : events.Error
        };

        return View(model);
    }

    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    /// <summary>Friendly page for 404 and other status codes (wired up with UseStatusCodePagesWithReExecute).</summary>
    [IgnoreAntiforgeryToken]
    public IActionResult StatusPage(int code)
    {
        var model = code switch
        {
            404 => new ErrorViewModel { Code = 404, Title = "Page not found", Message = "We couldn't find the page you were looking for. It may have moved or no longer exists." },
            403 => new ErrorViewModel { Code = 403, Title = "Access denied", Message = "You don't have permission to see that page." },
            400 => new ErrorViewModel { Code = 400, Title = "That didn't work", Message = "Something about that request wasn't right. Please go back and try again." },
            _ => new ErrorViewModel { Code = code }
        };

        Response.StatusCode = code;
        return View("Error", model);
    }
}
