using Microsoft.EntityFrameworkCore;
using VisualSearch.Worker.Crawling;
using VisualSearch.Worker.Crawling.Strategies;
using VisualSearch.Worker.Data;
using VisualSearch.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Worker options
builder.Services.Configure<WorkerOptions>(options =>
{
    var workerSection = builder.Configuration.GetSection("Worker");
    options.WorkerId = workerSection.GetValue<string>("InstanceId") ?? $"worker-{Environment.MachineName}";
    options.PollIntervalSeconds = workerSection.GetValue<int?>("PollIntervalSeconds") ?? 10;
    options.LeaseMinutes = workerSection.GetValue<int?>("LeaseMinutes") ?? 5;
});

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContextFactory<WorkerDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
    }));

// HttpClient factory for crawling
builder.Services.AddHttpClient("Crawler", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = true,
    MaxAutomaticRedirections = 5,
    AutomaticDecompression = System.Net.DecompressionMethods.All
});

// HttpClient for Zara Home (with specific headers for sitemap fetching)
builder.Services.AddHttpClient("ZaraHome", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.Add("Accept", "application/xml,text/xml,*/*;q=0.9");
    client.DefaultRequestHeaders.Add("Accept-Language", "es-ES,es;q=0.9");
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = true,
    MaxAutomaticRedirections = 5,
    AutomaticDecompression = System.Net.DecompressionMethods.All
});

// Playwright browser service (singleton - manages browser pool)
builder.Services.AddSingleton<PlaywrightBrowserService>();

// Product extractor
builder.Services.AddSingleton<IProductExtractor, DefaultProductExtractor>();

// Crawler strategies (order matters - first matching wins in factory)
builder.Services.AddSingleton<ICrawlerStrategy, ZaraHomeCrawlerStrategy>(); // Zara Home specific
builder.Services.AddSingleton<ICrawlerStrategy, GenericCrawlerStrategy>();   // Fallback for others
// Add more custom strategies here:
// builder.Services.AddSingleton<ICrawlerStrategy, IkeaCrawlerStrategy>();

// Strategy factory
builder.Services.AddSingleton<ICrawlerStrategyFactory, CrawlerStrategyFactory>();

// Background worker service
builder.Services.AddHostedService<CrawlJobWorkerService>();

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

if (!builder.Environment.IsDevelopment())
{
    builder.Logging.SetMinimumLevel(LogLevel.Information);
}

var host = builder.Build();

// Ensure database can be reached on startup
using (var scope = host.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<WorkerDbContext>>();
    using var db = await dbFactory.CreateDbContextAsync();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        logger.LogInformation("Testing database connection...");
        await db.Database.CanConnectAsync();
        logger.LogInformation("Database connection successful");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to connect to database. Worker will retry on each job poll.");
    }
}

await host.RunAsync();
