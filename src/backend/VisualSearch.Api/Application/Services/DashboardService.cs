using VisualSearch.Api.Contracts.DTOs;
using VisualSearch.Api.Domain.Interfaces;

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

    public DashboardService(
        IProviderRepository providerRepository,
        IProductRepository productRepository,
        IProductImageRepository productImageRepository,
        ICategoryRepository categoryRepository,
        IClipEmbeddingService clipEmbeddingService,
        IObjectDetectionService objectDetectionService)
    {
        _providerRepository = providerRepository;
        _productRepository = productRepository;
        _productImageRepository = productImageRepository;
        _categoryRepository = categoryRepository;
        _clipEmbeddingService = clipEmbeddingService;
        _objectDetectionService = objectDetectionService;
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
}
