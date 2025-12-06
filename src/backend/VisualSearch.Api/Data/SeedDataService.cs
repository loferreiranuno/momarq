using Microsoft.EntityFrameworkCore;
using VisualSearch.Api.Data.Entities;

namespace VisualSearch.Api.Data;

/// <summary>
/// Background service that seeds the database with initial data on first startup.
/// Creates sample providers, products, and product images with CLIP embeddings.
/// </summary>
public class SeedDataService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SeedDataService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeedDataService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for creating scoped services.</param>
    /// <param name="logger">The logger instance.</param>
    public SeedDataService(IServiceProvider serviceProvider, ILogger<SeedDataService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VisualSearchDbContext>();

        // Apply migrations with retry logic
        _logger.LogInformation("Applying database migrations...");
        
        var maxRetries = 5;
        var retryDelay = TimeSpan.FromSeconds(5);
        
        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                await dbContext.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Database migrations applied successfully.");
                break;
            }
            catch (Exception ex) when (i < maxRetries - 1)
            {
                _logger.LogWarning(ex, "Migration attempt {Attempt} failed, retrying in {Delay}s...", i + 1, retryDelay.TotalSeconds);
                await Task.Delay(retryDelay, cancellationToken);
            }
        }

        // Reload the data source types to pick up the vector extension
        // This is necessary because the vector extension is created during migration
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection() as Npgsql.NpgsqlConnection;
        if (connection is not null)
        {
            await connection.ReloadTypesAsync();
        }

        // Check if data already exists with retry logic for eventual consistency
        try
        {
            var hasData = await dbContext.Providers.AnyAsync(cancellationToken);
            if (hasData)
            {
                _logger.LogInformation("Database already seeded, skipping.");
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking for existing data, proceeding with seeding...");
        }

        _logger.LogInformation("Seeding database with initial data...");
        
        await SeedSettingsAsync(dbContext, cancellationToken);
        await SeedAdminUserAsync(dbContext, cancellationToken);
        await SeedProvidersAsync(dbContext, cancellationToken);

        _logger.LogInformation("Database seeding completed.");
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedSettingsAsync(VisualSearchDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.Settings.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Settings already exist, skipping settings seed.");
            return;
        }

        var defaultSettings = new List<Setting>
        {
            new()
            {
                Key = "search.maxImageSize",
                Value = "800",
                Type = SettingType.Integer,
                Category = "search",
                Description = "Maximum image dimension (width/height) in pixels for preprocessing before search"
            },
            new()
            {
                Key = "search.jpegQuality",
                Value = "85",
                Type = SettingType.Integer,
                Category = "search",
                Description = "JPEG quality (1-100) for image compression during preprocessing"
            },
            new()
            {
                Key = "search.maxResults",
                Value = "20",
                Type = SettingType.Integer,
                Category = "search",
                Description = "Maximum number of search results to return"
            },
            new()
            {
                Key = "ui.siteName",
                Value = "Visual Search",
                Type = SettingType.String,
                Category = "ui",
                Description = "The site name displayed in the header and browser title"
            },
            new()
            {
                Key = "ui.welcomeMessage",
                Value = "Discover products through visual search. Upload an image to find similar items.",
                Type = SettingType.String,
                Category = "ui",
                Description = "Welcome message displayed on the home page"
            },
            new()
            {
                Key = "ui.primaryColor",
                Value = "#8B7355",
                Type = SettingType.String,
                Category = "ui",
                Description = "Primary accent color for the UI"
            },
            new()
            {
                Key = "ui.showSimilarityScore",
                Value = "true",
                Type = SettingType.Boolean,
                Category = "ui",
                Description = "Whether to show similarity percentage on search results"
            }
        };

        dbContext.Settings.AddRange(defaultSettings);
        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} default settings", defaultSettings.Count);
    }

    private async Task SeedAdminUserAsync(VisualSearchDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.AdminUsers.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Admin user already exists, skipping admin seed.");
            return;
        }

        // Default admin user with password that must be changed on first login
        var adminUser = new AdminUser
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            MustChangePassword = true
        };

        dbContext.AdminUsers.Add(adminUser);
        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created default admin user. Username: admin, Password: admin123 (must change on first login)");
    }

    private static async Task SeedProvidersAsync(VisualSearchDbContext dbContext, CancellationToken cancellationToken)
    {
        var providers = new List<Provider>
        {
            new()
            {
                Name = "Zara Home",
                WebsiteUrl = "https://www.zarahome.com"
            },
            new()
            {
                Name = "IKEA",
                WebsiteUrl = "https://www.ikea.com"
            },
            new()
            {
                Name = "H&M Home",
                WebsiteUrl = "https://www.hm.com/home"
            }
        };

        dbContext.Providers.AddRange(providers);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

}
