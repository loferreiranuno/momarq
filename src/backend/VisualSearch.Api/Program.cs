using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.Npgsql;
using VisualSearch.Api.Data;
using VisualSearch.Api.Endpoints;
using VisualSearch.Api.Services;

// Configure Npgsql global type mapper for pgvector BEFORE any connections are made
#pragma warning disable CS0618 // GlobalTypeMapper is obsolete but needed for EF Core integration
NpgsqlConnection.GlobalTypeMapper.UseVector();
#pragma warning restore CS0618

var builder = WebApplication.CreateBuilder(args);

// ========== Services Configuration ==========

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Add PostgreSQL with pgvector support
builder.Services.AddDbContext<VisualSearchDbContext>(options =>
{
    options.UseNpgsql(
        connectionString,
        npgsqlOptions =>
        {
            npgsqlOptions.UseVector();
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null);
        });
});

// Add CLIP embedding service (singleton for model reuse)
builder.Services.AddSingleton<ClipEmbeddingService>();

// Add HTTP client factory for downloading images
builder.Services.AddHttpClient();

// Add seed data service (runs on startup)
builder.Services.AddHostedService<SeedDataService>();

// Add OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Visual Search API",
        Version = "v1",
        Description = "High-performance visual similarity search API using CLIP embeddings and pgvector."
    });
});

var app = builder.Build();

// ========== Middleware Pipeline ==========

// Enable Swagger in all environments for POC
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Visual Search API v1");
    options.RoutePrefix = "swagger";
});

// ========== Endpoints ==========

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithTags("System");

// Map search endpoints (binary protocol)
app.MapSearchEndpoints();

// Map image search endpoint (fast, server-side ML)
app.MapImageSearchEndpoints();

// Map admin endpoints (JSON REST)
app.MapAdminEndpoints();

// ========== Run Application ==========

app.Run();
