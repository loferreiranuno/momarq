using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using Pgvector.Npgsql;
using VisualSearch.Api;
using VisualSearch.Api.Data;
using VisualSearch.Api.Endpoints;
using VisualSearch.Api.Services;

// Configure Npgsql global type mapper for pgvector BEFORE any connections are made
#pragma warning disable CS0618 // GlobalTypeMapper is obsolete but needed for EF Core integration
NpgsqlConnection.GlobalTypeMapper.UseVector();
#pragma warning restore CS0618

var builder = WebApplication.CreateBuilder(args);

var jwtOptions = JwtOptions.Create(builder.Configuration);
builder.Services.AddSingleton(jwtOptions);

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

// Add memory cache for settings
builder.Services.AddMemoryCache();

// Add CLIP embedding service (singleton for model reuse)
builder.Services.AddSingleton<ClipEmbeddingService>();

// Add object detection service (YOLO for furniture detection)
builder.Services.AddSingleton<ObjectDetectionService>();

// Add image upload service (handles file storage with resize/compression)
builder.Services.AddSingleton<ImageUploadService>();

// Add vectorization service (facade combining CLIP + YOLO + local file reading)
builder.Services.AddSingleton<VectorizationService>();

// Add settings service (singleton for SSE connections)
builder.Services.AddSingleton<SettingsService>();

// Add HTTP client factory for downloading images
builder.Services.AddHttpClient();

// Add named HTTP client for image download with SSL bypass (for problematic external URLs)
builder.Services.AddHttpClient("ImageDownload")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

// Add seed data service (runs on startup)
builder.Services.AddHostedService<SeedDataService>();

// Configure JWT authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", policy => policy.RequireRole("Admin"));

// Add OpenAPI/Swagger with JWT support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Visual Search API",
        Version = "v1",
        Description = "High-performance visual similarity search API using CLIP embeddings and pgvector."
    });

    // Add JWT authentication to Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
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

// Enable authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// ========== Endpoints ==========

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithTags("System");

// Map authentication endpoints
app.MapAuthEndpoints();

// Map settings endpoints
app.MapSettingsEndpoints();

// Map image search endpoint (server-side ML)
app.MapImageSearchEndpoints();

// Map admin endpoints (JSON REST)
app.MapAdminEndpoints();

// ========== Run Application ==========

app.Run();
