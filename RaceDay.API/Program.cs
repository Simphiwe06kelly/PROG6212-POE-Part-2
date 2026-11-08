using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// The test project supplies its own in-memory database in the "Testing" environment
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<RaceDayDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
}

builder.Services.AddControllers();

// Image uploads go to Azure Blob Storage. Only the API talks to Azure; the MVC app sends files to the API.
// The test project supplies a fake storage in the "Testing" environment.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddSingleton<IImageStorage, BlobImageStorage>();
}

// Session (server-side storage of UserID + Role)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "API for managing running, walking and cycling events."
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// While developing, the web app calls the API over plain http, so the redirect to https is skipped there
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseSession();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Lets the test project start the API in memory
public partial class Program { }