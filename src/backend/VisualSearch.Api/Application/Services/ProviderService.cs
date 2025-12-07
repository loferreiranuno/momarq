using VisualSearch.Api.Contracts.DTOs;
using VisualSearch.Api.Contracts.Requests;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Domain.Interfaces;

namespace VisualSearch.Api.Application.Services;

/// <summary>
/// Application service for provider-related business operations.
/// </summary>
public sealed class ProviderService
{
    private readonly IProviderRepository _providerRepository;
    private readonly IProductRepository _productRepository;

    public ProviderService(
        IProviderRepository providerRepository,
        IProductRepository productRepository)
    {
        _providerRepository = providerRepository;
        _productRepository = productRepository;
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

    public async Task<ProviderDto> CreateProviderAsync(CreateProviderRequest request, CancellationToken cancellationToken = default)
    {
        // Check if provider with same name exists
        var existing = await _providerRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Provider with name '{request.Name}' already exists.");
        }

        var provider = new Provider
        {
            Name = request.Name,
            LogoUrl = request.LogoUrl,
            WebsiteUrl = request.WebsiteUrl,
            CreatedAt = DateTime.UtcNow
        };

        await _providerRepository.AddAsync(provider, cancellationToken);
        return MapToDto(provider, 0);
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
}
