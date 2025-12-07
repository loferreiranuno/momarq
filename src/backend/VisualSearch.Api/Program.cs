using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using Pgvector.Npgsql;
using VisualSearch.Api;
using VisualSearch.Api.Data;
using VisualSearch.Api.Endpoints;
using VisualSearch.Api.Extensions;
using VisualSearch.Api.Services;

// Configure Npgsql global type mapper for pgvector BEFORE any connections are made
#pragma warning disable CS0618 // GlobalTypeMapper is obsolete but needed for EF Core integration
NpgsqlConnection.GlobalTypeMapper.UseVector();
#pragma warning restore CS0618

var builder = WebApplication.CreateBuilder(args);

// Configure multipart body limits for image uploads (up to 50MB)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52_428_800; // 50 MB
    options.ValueLengthLimit = 52_428_800; // 50 MB
    options.MultipartHeadersLengthLimit = 52_428_800; // 50 MB
});

// Configure request body limits for Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 52_428_800; // 50 MB
});

var jwtOptions = JwtOptions.Create(builder.Configuration);
builder.Services.AddSingleton(jwtOptions);

// ========== Services Configuration (using organized DI extensions) ==========

// Add database services (DbContext with pgvector)
builder.Services.AddDatabaseServices(builder.Configuration);

// Add repositories (data access layer)
builder.Services.AddRepositories();

// Add application services (business logic layer)
builder.Services.AddApplicationServices();

// Add AI services (CLIP, YOLO, vectorization)
builder.Services.AddAIServices();

// Add infrastructure services (file upload, settings, seed data)
builder.Services.AddInfrastructureServices();

// Add HTTP services (HTTP clients for external APIs)
builder.Services.AddHttpServices();

// Add memory cache for settings
builder.Services.AddMemoryCache();

// Add named HTTP client for image download with SSL bypass (for problematic external URLs)
builder.Services.AddHttpClient("ImageDownload")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

// Add hosted service for seed data
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

// Map public categories endpoint
app.MapCategoriesEndpoints();

// Map admin endpoints (JSON REST)
app.MapAdminEndpoints();

// ========== Run Application ==========

app.Run();
