using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RaceDay.Api.Data;

namespace RaceDay.Api.Tests.Helpers;

/// <summary>Starts the real API with an in-memory database instead of SQL Server.</summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "RaceDayTests-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddDbContext<RaceDayDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Creates the schema and applies the seed data from OnModelCreating
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<RaceDayDbContext>().Database.EnsureCreated();

        return host;
    }
}