using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;

// Always use one culture so "21.1" and dates are read the same on every machine
// (South African regional settings use a comma as the decimal separator).
var culture = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    // Every POST must carry the anti-forgery token the forms include
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// ---- Session: the MVC app keeps the user's ID, role and name on the SERVER ----
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    // A different cookie name from the API's, so the two apps never overwrite each other's cookie on localhost
    options.Cookie.Name = ".RaceDay.Web.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(60);
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UserSession>();

// ---- The Part 2 API: the ONLY place this app gets data from ----
builder.Services.AddTransient<ApiSessionCookieHandler>();

var apiSettings = builder.Configuration.GetSection("ApiSettings");
var baseUrl = apiSettings["BaseUrl"] ?? "https://localhost:7228/";
if (!baseUrl.EndsWith('/')) baseUrl += "/";
var allowUntrustedCertificate = apiSettings.GetValue<bool>("AllowUntrustedCertificate");

builder.Services.AddHttpClient<IRaceDayApi, RaceDayApi>(client =>
    {
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        // Cookies are forwarded by ApiSessionCookieHandler, so the handler's own cookie jar is switched off
        var handler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false };
        if (allowUntrustedCertificate)
        {
            // Only for running in Docker against the API's local development certificate
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }
        return handler;
    })
    .AddHttpMessageHandler<ApiSessionCookieHandler>();

var app = builder.Build();

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture),
    SupportedCultures = new List<CultureInfo> { culture },
    SupportedUICultures = new List<CultureInfo> { culture }
});

// Friendly pages instead of raw errors (in every environment)
app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/StatusPage", "?code={0}");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
