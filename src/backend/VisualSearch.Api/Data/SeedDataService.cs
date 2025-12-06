using Microsoft.EntityFrameworkCore;
using Pgvector;
using VisualSearch.Api.Data.Entities;

namespace VisualSearch.Api.Data;

/// <summary>
/// Background service that seeds the database with initial data on first startup.
/// Creates sample providers, products, and product images with random embeddings.
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
        if (connection != null)
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
        await SeedProductsAsync(dbContext, cancellationToken);

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
                LogoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/f/fd/Zara_Logo.svg/200px-Zara_Logo.svg.png",
                WebsiteUrl = "https://www.zarahome.com"
            },
            new()
            {
                Name = "IKEA",
                LogoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c5/Ikea_logo.svg/200px-Ikea_logo.svg.png",
                WebsiteUrl = "https://www.ikea.com"
            },
            new()
            {
                Name = "H&M Home",
                LogoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/5/53/H%26M-Logo.svg/200px-H%26M-Logo.svg.png",
                WebsiteUrl = "https://www.hm.com/home"
            }
        };

        dbContext.Providers.AddRange(providers);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedProductsAsync(VisualSearchDbContext dbContext, CancellationToken cancellationToken)
    {
        var providers = await dbContext.Providers.ToListAsync(cancellationToken);
        var random = new Random(42); // Fixed seed for reproducibility

        var categories = new[]
        {
            "sofa", "bed", "chair", "table", "lamp", "plant", "rug", "shelf", "tv", "pillow",
            "mirror", "vase", "curtain", "blanket", "desk", "bookshelf", "armchair", "ottoman",
            "nightstand", "dresser"
        };

        var adjectives = new[]
        {
            "Modern", "Classic", "Minimalist", "Rustic", "Elegant", "Cozy", "Scandinavian",
            "Industrial", "Bohemian", "Contemporary", "Vintage", "Luxurious"
        };

        var materials = new[]
        {
            "Wood", "Metal", "Fabric", "Leather", "Velvet", "Rattan", "Glass", "Ceramic",
            "Cotton", "Linen", "Wool", "Marble"
        };

        var products = new List<Product>();
        var productImages = new List<ProductImage>();

        // Generate 100 products across all providers
        for (int i = 1; i <= 100; i++)
        {
            var provider = providers[random.Next(providers.Count)];
            var category = categories[random.Next(categories.Length)];
            var adjective = adjectives[random.Next(adjectives.Length)];
            var material = materials[random.Next(materials.Length)];

            var product = new Product
            {
                ProviderId = provider.Id,
                ExternalId = $"{provider.Name[..2].ToUpperInvariant()}-{i:D5}",
                Name = $"{adjective} {material} {char.ToUpperInvariant(category[0])}{category[1..]}",
                Description = $"A beautiful {adjective.ToLowerInvariant()} {category} made from high-quality {material.ToLowerInvariant()}. Perfect for any modern home.",
                Price = Math.Round((decimal)(random.NextDouble() * 500 + 29.99), 2),
                Currency = "EUR",
                Category = category,
                ProductUrl = $"{provider.WebsiteUrl}/product/{i}"
            };

            products.Add(product);
        }

        dbContext.Products.AddRange(products);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Generate 1-3 images per product with random normalized embeddings
        foreach (var product in products)
        {
            var imageCount = random.Next(1, 4);

            for (int j = 0; j < imageCount; j++)
            {
                var embedding = GenerateRandomNormalizedEmbedding(random);

                var productImage = new ProductImage
                {
                    ProductId = product.Id,
                    ImageUrl = $"https://picsum.photos/seed/{product.Id}-{j}/400/400",
                    Embedding = new Vector(embedding),
                    IsPrimary = j == 0
                };

                productImages.Add(productImage);
            }
        }

        dbContext.ProductImages.AddRange(productImages);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Generates a random 512-dimensional unit vector (L2-normalized).
    /// </summary>
    private static float[] GenerateRandomNormalizedEmbedding(Random random)
    {
        var embedding = new float[512];
        float sumSquares = 0;

        for (int i = 0; i < 512; i++)
        {
            // Generate random values from normal distribution using Box-Muller transform
            var u1 = random.NextDouble();
            var u2 = random.NextDouble();
            var z = (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
            embedding[i] = z;
            sumSquares += z * z;
        }

        // L2 normalize to unit vector
        var magnitude = (float)Math.Sqrt(sumSquares);
        for (int i = 0; i < 512; i++)
        {
            embedding[i] /= magnitude;
        }

        return embedding;
    }
}
