using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VisualSearch.Api.Application.Services;
using VisualSearch.Api.Data;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Domain.Interfaces;
using VisualSearch.Api.Infrastructure.Repositories;
using VisualSearch.Api.Services;

namespace VisualSearch.Api.Extensions;

/// <summary>
/// Extension methods for configuring dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds database services including DbContext and connection configuration.
    /// </summary>
    public static IServiceCollection AddDatabaseServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<VisualSearchDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.UseVector();
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
            });
        });

        return services;
    }

    /// <summary>
    /// Adds all repository implementations.
    /// </summary>
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProviderRepository, ProviderRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductImageRepository, ProductImageRepository>();
        services.AddScoped<ISettingRepository, SettingRepository>();
        services.AddScoped<IAdminUserRepository, AdminUserRepository>();

        return services;
    }

    /// <summary>
    /// Adds application layer services (business logic).
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();
        services.AddScoped<CategoryService>();
        services.AddScoped<ProviderService>();
        services.AddScoped<ProductService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<AuthService>();
        services.AddScoped<VisualSearchService>();
        services.AddScoped<IProductImageService, ProductImageService>();

        return services;
    }

    /// <summary>
    /// Adds AI/ML services (CLIP, YOLO, vectorization).
    /// </summary>
    public static IServiceCollection AddAIServices(this IServiceCollection services)
    {
        // CLIP embedding service (singleton - holds loaded model)
        services.AddSingleton<ClipEmbeddingService>();
        services.AddSingleton<IClipEmbeddingService>(sp => 
            new ClipEmbeddingServiceAdapter(sp.GetRequiredService<ClipEmbeddingService>()));

        // Object detection service (singleton - holds loaded model)
        services.AddSingleton<ObjectDetectionService>();
        services.AddSingleton<IObjectDetectionService>(sp =>
            new ObjectDetectionServiceAdapter(sp.GetRequiredService<ObjectDetectionService>()));

        // Vectorization service (singleton - uses other singleton services)
        services.AddSingleton<VectorizationService>();

        return services;
    }

    /// <summary>
    /// Adds infrastructure services (file upload, settings, etc.).
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Image upload service (singleton - stateless)
        services.AddSingleton<ImageUploadService>();
        
        // Settings service (singleton - for SSE connections and caching)
        services.AddSingleton<SettingsService>();

        // SSE ticket service (singleton - short-lived one-time tokens for EventSource auth)
        services.AddSingleton<SseTicketService>();

        return services;
    }

    /// <summary>
    /// Adds HTTP clients for external services.
    /// </summary>
    public static IServiceCollection AddHttpServices(this IServiceCollection services)
    {
        services.AddHttpClient("ImageDownloader", client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "VisualSearchApi/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient();

        return services;
    }
}
