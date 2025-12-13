using System.Text.Json;
using VisualSearch.Api.Contracts.DTOs;
using VisualSearch.Api.Contracts.Requests;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Domain.Interfaces;
using VisualSearch.Contracts.Crawling;

namespace VisualSearch.Api.Application.Services;

/// <summary>
/// Application service for provider-related business operations.
/// </summary>
public sealed class ProviderService
{
    private readonly IProviderRepository _providerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IProductImageRepository _productImageRepository;

    public ProviderService(
        IProviderRepository providerRepository,
        IProductRepository productRepository,
        IProductImageRepository productImageRepository)
    {
        _providerRepository = providerRepository;
        _productRepository = productRepository;
        _productImageRepository = productImageRepository;
    }

    public async Task<IEnumerable<ProviderDto>> GetAllProvidersAsync(CancellationToken cancellationToken = default)
    {
        var providers = await _providerRepository.GetAllAsync(cancellationToken);
        var providerDtos = new List<ProviderDto>();

        foreach (var provider in providers)
        {
            var productCount = await _productRepository.GetCountByProviderAsync(provider.Id, cancellationToken);
            providerDtos.Add(MapToDto(provider, productCount));
        }

        return providerDtos;
    }

    /// <summary>
    /// Gets all providers with product count for admin dashboard.
    /// </summary>
    public async Task<IEnumerable<AdminProviderDto>> GetAllAdminProvidersAsync(CancellationToken cancellationToken = default)
    {
        var providers = await _providerRepository.GetAllAsync(cancellationToken);
        var providerDtos = new List<AdminProviderDto>();

        foreach (var provider in providers)
        {
            var productCount = await _productRepository.GetCountByProviderAsync(provider.Id, cancellationToken);
            providerDtos.Add(new AdminProviderDto
            {
                Id = provider.Id,
                Name = provider.Name,
                LogoUrl = provider.LogoUrl,
                WebsiteUrl = provider.WebsiteUrl,
                ProductCount = productCount,
                CrawlerType = provider.CrawlerType,
                CrawlerConfigJson = provider.CrawlerConfigJson
            });
        }

        return providerDtos;
    }

    public async Task<ProviderDto?> GetProviderByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var provider = await _providerRepository.GetByIdAsync(id, cancellationToken);
        if (provider is null)
        {
            return null;
        }

        var productCount = await _productRepository.GetCountByProviderAsync(id, cancellationToken);
        return MapToDto(provider, productCount);
    }

    /// <summary>
    /// Gets a provider by ID for admin dashboard.
    /// </summary>
    public async Task<AdminProviderDto?> GetAdminProviderByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var provider = await _providerRepository.GetByIdAsync(id, cancellationToken);
        if (provider is null)
        {
            return null;
        }

        var productCount = await _productRepository.GetCountByProviderAsync(id, cancellationToken);
        return new AdminProviderDto
        {
            Id = provider.Id,
            Name = provider.Name,
            LogoUrl = provider.LogoUrl,
            WebsiteUrl = provider.WebsiteUrl,
            ProductCount = productCount,
            CrawlerType = provider.CrawlerType,
            CrawlerConfigJson = provider.CrawlerConfigJson
        };
    }

    public async Task<ProviderDto> CreateProviderAsync(CreateProviderRequest request, CancellationToken cancellationToken = default)
    {
        // Check if provider with same name exists
        var existing = await _providerRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Provider with name '{request.Name}' already exists.");
        }

        // Validate crawler type
        ValidateCrawlerType(request.CrawlerType);
        
        // Validate crawler config JSON
        ValidateCrawlerConfigJson(request.CrawlerConfigJson);

        var provider = new Provider
        {
            Name = request.Name,
            LogoUrl = request.LogoUrl,
            WebsiteUrl = request.WebsiteUrl,
            CrawlerType = request.CrawlerType ?? CrawlerTypes.Generic,
            CrawlerConfigJson = request.CrawlerConfigJson,
            CreatedAt = DateTime.UtcNow
        };

        await _providerRepository.AddAsync(provider, cancellationToken);
        return MapToDto(provider, 0);
    }

    /// <summary>
    /// Creates a provider and returns admin DTO.
    /// </summary>
    public async Task<AdminProviderDto> CreateAdminProviderAsync(CreateProviderRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Provider name is required.", nameof(request));
        }

        // Validate crawler type
        ValidateCrawlerType(request.CrawlerType);
        
        // Validate crawler config JSON
        ValidateCrawlerConfigJson(request.CrawlerConfigJson);

        var provider = new Provider
        {
            Name = request.Name,
            LogoUrl = request.LogoUrl,
            WebsiteUrl = request.WebsiteUrl,
            CrawlerType = request.CrawlerType ?? CrawlerTypes.Generic,
            CrawlerConfigJson = request.CrawlerConfigJson,
            CreatedAt = DateTime.UtcNow
        };

        await _providerRepository.AddAsync(provider, cancellationToken);
        return new AdminProviderDto
        {
            Id = provider.Id,
            Name = provider.Name,
            LogoUrl = provider.LogoUrl,
            WebsiteUrl = provider.WebsiteUrl,
            ProductCount = 0,
            CrawlerType = provider.CrawlerType,
            CrawlerConfigJson = provider.CrawlerConfigJson
        };
    }

    public async Task<ProviderDto?> UpdateProviderAsync(int id, UpdateProviderRequest request, CancellationToken cancellationToken = default)
    {
        var provider = await _providerRepository.GetByIdAsync(id, cancellationToken);
        if (provider is null)
        {
            return null;
        }

        // Check if another provider with same name exists
        var existingName = await _providerRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existingName is not null && existingName.Id != id)
        {
            throw new InvalidOperationException($"Provider with name '{request.Name}' already exists.");
        }

        provider.Name = request.Name;
        provider.LogoUrl = request.LogoUrl;
        provider.WebsiteUrl = request.WebsiteUrl;

        await _providerRepository.UpdateAsync(provider, cancellationToken);
        
        var productCount = await _productRepository.GetCountByProviderAsync(id, cancellationToken);
        return MapToDto(provider, productCount);
    }

    /// <summary>
    /// Updates a provider with partial data and returns admin DTO.
    /// </summary>
    public async Task<AdminProviderDto?> UpdateAdminProviderAsync(
        int id, 
        string? name, 
        string? logoUrl, 
        string? websiteUrl, 
        string? crawlerType,
        string? crawlerConfigJson,
        CancellationToken cancellationToken = default)
    {
        var provider = await _providerRepository.GetByIdAsync(id, cancellationToken);
        if (provider is null)
        {
            return null;
        }

        // Validate crawler type
        ValidateCrawlerType(crawlerType);
        
        // Validate crawler config JSON
        ValidateCrawlerConfigJson(crawlerConfigJson);

        if (!string.IsNullOrWhiteSpace(name))
        {
            provider.Name = name;
        }

        if (logoUrl is not null)
        {
            provider.LogoUrl = logoUrl;
        }

        if (websiteUrl is not null)
        {
            provider.WebsiteUrl = websiteUrl;
        }

        if (crawlerType is not null)
        {
            provider.CrawlerType = crawlerType;
        }

        if (crawlerConfigJson is not null)
        {
            provider.CrawlerConfigJson = string.IsNullOrWhiteSpace(crawlerConfigJson) ? null : crawlerConfigJson;
        }

        await _providerRepository.UpdateAsync(provider, cancellationToken);

        var productCount = await _productRepository.GetCountByProviderAsync(id, cancellationToken);
        return new AdminProviderDto
        {
            Id = provider.Id,
            Name = provider.Name,
            LogoUrl = provider.LogoUrl,
            WebsiteUrl = provider.WebsiteUrl,
            ProductCount = productCount,
            CrawlerType = provider.CrawlerType,
            CrawlerConfigJson = provider.CrawlerConfigJson
        };
    }

    public async Task<bool> DeleteProviderAsync(int id, CancellationToken cancellationToken = default)
    {
        var provider = await _providerRepository.GetByIdAsync(id, cancellationToken);
        if (provider is null)
        {
            return false;
        }

        // Check if provider has products
        var productCount = await _productRepository.GetCountByProviderAsync(id, cancellationToken);
        if (productCount > 0)
        {
            throw new InvalidOperationException($"Cannot delete provider with {productCount} products. Delete the products first.");
        }

        await _providerRepository.DeleteAsync(provider, cancellationToken);
        return true;
    }

    /// <summary>
    /// Deletes a provider and all associated products and images (cascade delete).
    /// </summary>
    public async Task<bool> DeleteProviderCascadeAsync(int id, CancellationToken cancellationToken = default)
    {
        var provider = await _providerRepository.GetWithProductsAndImagesAsync(id, cancellationToken);
        if (provider is null)
        {
            return false;
        }

        // Delete all images first
        foreach (var product in provider.Products)
        {
            foreach (var image in product.Images)
            {
                await _productImageRepository.DeleteAsync(image, cancellationToken);
            }
        }

        // Delete all products
        foreach (var product in provider.Products)
        {
            await _productRepository.DeleteAsync(product, cancellationToken);
        }

        // Delete provider
        await _providerRepository.DeleteAsync(provider, cancellationToken);
        return true;
    }

    public async Task<IEnumerable<ProviderSummaryDto>> GetProviderSummariesAsync(CancellationToken cancellationToken = default)
    {
        var providers = await _providerRepository.GetAllAsync(cancellationToken);
        return providers.Select(p => new ProviderSummaryDto(p.Id, p.Name, true));
    }

    private static ProviderDto MapToDto(Provider provider, int productCount)
    {
        return new ProviderDto(
            provider.Id,
            provider.Name,
            null, // Description field not in current entity
            provider.WebsiteUrl,
            provider.LogoUrl,
            true, // IsActive - not in current entity
            provider.CreatedAt,
            null, // UpdatedAt field not in current entity
            productCount
        );
    }

    /// <summary>
    /// Validates that the crawler type is a known type.
    /// </summary>
    private static void ValidateCrawlerType(string? crawlerType)
    {
        if (!AllowedCrawlerTypes.IsValid(crawlerType))
        {
            throw new ArgumentException(
                $"Invalid crawler type '{crawlerType}'. Allowed values: {string.Join(", ", AllowedCrawlerTypes.Values)}",
                nameof(crawlerType));
        }
    }

    /// <summary>
    /// Validates that the crawler config JSON is valid JSON and can be deserialized to CrawlerConfig.
    /// </summary>
    private static void ValidateCrawlerConfigJson(string? crawlerConfigJson)
    {
        if (string.IsNullOrWhiteSpace(crawlerConfigJson))
        {
            return; // Empty is allowed
        }

        try
        {
            var config = JsonSerializer.Deserialize<CrawlerConfig>(crawlerConfigJson);
            if (config is null)
            {
                throw new ArgumentException("Crawler config JSON cannot be null.", nameof(crawlerConfigJson));
            }
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid crawler config JSON: {ex.Message}", nameof(crawlerConfigJson));
        }
    }
}
