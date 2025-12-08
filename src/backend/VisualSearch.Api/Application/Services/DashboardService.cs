using VisualSearch.Api.Contracts.DTOs;
using VisualSearch.Api.Domain.Interfaces;
using VisualSearch.Api.Services;

namespace VisualSearch.Api.Application.Services;

/// <summary>
/// Application service for dashboard statistics and system status.
/// </summary>
public sealed class DashboardService
{
    private readonly IProviderRepository _providerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IProductImageRepository _productImageRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IClipEmbeddingService _clipEmbeddingService;
    private readonly IObjectDetectionService _objectDetectionService;
    private readonly VectorizationService _vectorizationService;

    public DashboardService(
        IProviderRepository providerRepository,
        IProductRepository productRepository,
        IProductImageRepository productImageRepository,
        ICategoryRepository categoryRepository,
        IClipEmbeddingService clipEmbeddingService,
        IObjectDetectionService objectDetectionService,
        VectorizationService vectorizationService)
    {
        _providerRepository = providerRepository;
        _productRepository = productRepository;
        _productImageRepository = productImageRepository;
        _categoryRepository = categoryRepository;
        _clipEmbeddingService = clipEmbeddingService;
        _objectDetectionService = objectDetectionService;
        _vectorizationService = vectorizationService;
    }

    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var providers = await _providerRepository.GetAllAsync(cancellationToken);
        var products = await _productRepository.GetAllAsync(cancellationToken);
        var totalImages = await _productImageRepository.GetTotalCountAsync(cancellationToken);
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);
        var enabledCategories = await _categoryRepository.GetEnabledForDetectionAsync(cancellationToken);

        return new DashboardStatsDto(
            TotalProviders: providers.Count(),
            ActiveProviders: providers.Count(), // All providers are active currently
            TotalProducts: products.Count(),
            TotalImages: totalImages,
            TotalCategories: categories.Count(),
            EnabledDetectionCategories: enabledCategories.Count()
        );
    }

    /// <summary>
    /// Gets admin dashboard stats including vectorization progress.
    /// </summary>
    public async Task<AdminDashboardStatsDto> GetAdminStatsAsync(CancellationToken cancellationToken = default)
    {
        var providers = await _providerRepository.GetAllAsync(cancellationToken);
        var products = await _productRepository.GetAllAsync(cancellationToken);
        var totalImages = await _productImageRepository.GetTotalCountAsync(cancellationToken);
        var vectorizedCount = await _productImageRepository.GetVectorizedCountAsync(cancellationToken);

        var progress = totalImages > 0 ? (double)vectorizedCount / totalImages * 100 : 100;

        return new AdminDashboardStatsDto(
            Products: products.Count(),
            Providers: providers.Count(),
            Images: totalImages,
            VectorizedImages: vectorizedCount,
            VectorizationProgress: progress
        );
    }

    public Task<SystemStatusDto> GetSystemStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = new SystemStatusDto(
            ClipModelLoaded: _clipEmbeddingService.IsModelLoaded,
            YoloModelLoaded: _objectDetectionService.IsModelLoaded,
            DatabaseConnected: true, // If we got here, database is connected
            ApiVersion: "1.0.0"
        );

        return Task.FromResult(status);
    }

    /// <summary>
    /// Gets admin system status including service availability.
    /// </summary>
    public AdminSystemStatusDto GetAdminSystemStatus()
    {
        return new AdminSystemStatusDto(
            ClipModelLoaded: _clipEmbeddingService.IsModelLoaded,
            YoloModelLoaded: _objectDetectionService.IsModelLoaded,
            VectorizationAvailable: _vectorizationService.IsAvailable,
            ObjectDetectionAvailable: _vectorizationService.IsDetectionAvailable
        );
    }
}
