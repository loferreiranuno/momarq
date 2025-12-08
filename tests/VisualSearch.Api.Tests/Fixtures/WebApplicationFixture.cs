using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VisualSearch.Api.Data;

namespace VisualSearch.Api.Tests.Fixtures;

/// <summary>
/// Custom WebApplicationFactory that replaces the database connection
/// with the Testcontainers PostgreSQL instance.
/// </summary>
public class WebApplicationFixture : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public WebApplicationFixture(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            services.RemoveAll<DbContextOptions<VisualSearchDbContext>>();
            services.RemoveAll<VisualSearchDbContext>();

            // Add DbContext with test container connection string
            services.AddDbContext<VisualSearchDbContext>(options =>
            {
                options.UseNpgsql(_connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.UseVector();
                });
            });

            // Ensure database is created and migrated
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VisualSearchDbContext>();
            db.Database.Migrate();
        });

        builder.UseEnvironment("Testing");
    }
}
