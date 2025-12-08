using VisualSearch.Api.Contracts.DTOs;
using VisualSearch.Api.Contracts.Requests;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Domain.Interfaces;

namespace VisualSearch.Api.Application.Services;

/// <summary>
/// Application service for product-related business operations.
/// </summary>
public sealed class ProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IProviderRepository _providerRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IProductImageRepository _productImageRepository;

    public ProductService(
        IProductRepository productRepository,
        IProviderRepository providerRepository,
        ICategoryRepository categoryRepository,
        IProductImageRepository productImageRepository)
    {
        _productRepository = productRepository;
        _providerRepository = providerRepository;
        _categoryRepository = categoryRepository;
        _productImageRepository = productImageRepository;
    }

    public async Task<PagedResult<ProductSummaryDto>> GetProductsPagedAsync(
        int page,
        int pageSize,
        int? providerId = null,
        int? categoryId = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _productRepository.GetPagedAsync(
            page, pageSize, providerId, categoryId, search, cancellationToken);

        var dtos = items.Select(MapToSummaryDto);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResult<ProductSummaryDto>(dtos, totalCount, page, pageSize, totalPages);
    }

    /// <summary>
    /// Gets products with pagination for admin dashboard.
    /// </summary>
    public async Task<AdminPagedResult<AdminProductDto>> GetAdminProductsPagedAsync(
        int page,
        int pageSize,
        int? providerId = null,
        int? categoryId = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _productRepository.GetPagedAsync(
            page, pageSize, providerId, categoryId, search, cancellationToken);

        var dtos = items.Select(MapToAdminDto).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new AdminPagedResult<AdminProductDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<ProductDto?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetWithImagesAsync(id, cancellationToken);
        return product is null ? null : MapToDto(product);
    }

    /// <summary>
    /// Gets a product by ID for admin dashboard.
    /// </summary>
    public async Task<AdminProductDto?> GetAdminProductByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetWithImagesAsync(id, cancellationToken);
        return product is null ? null : MapToAdminDto(product);
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        // Validate provider exists
        var providerExists = await _providerRepository.ExistsAsync(request.ProviderId, cancellationToken);
        if (!providerExists)
        {
            throw new InvalidOperationException($"Provider with ID '{request.ProviderId}' does not exist.");
        }

        // Validate category exists if provided
        if (request.CategoryId.HasValue)
        {
            var categoryExists = await _categoryRepository.ExistsAsync(request.CategoryId.Value, cancellationToken);
            if (!categoryExists)
            {
                throw new InvalidOperationException($"Category with ID '{request.CategoryId.Value}' does not exist.");
            }
        }

        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price ?? 0,
            Currency = request.Currency ?? "EUR",
            ProductUrl = request.ProductUrl,
            ProviderId = request.ProviderId,
            CategoryId = request.CategoryId,
            CreatedAt = DateTime.UtcNow
        };

        await _productRepository.AddAsync(product, cancellationToken);

        // Reload with relations
        var createdProduct = await _productRepository.GetWithImagesAsync(product.Id, cancellationToken);
        return MapToDto(createdProduct!);
    }

    /// <summary>
    /// Creates a product with ExternalId support for admin.
    /// </summary>
    public async Task<int> CreateAdminProductAsync(
        int providerId,
        string name,
        string? externalId,
        string? description,
        decimal price,
        string? currency,
        int? categoryId,
        string? productUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name is required.", nameof(name));
        }

        var providerExists = await _providerRepository.ExistsAsync(providerId, cancellationToken);
        if (!providerExists)
        {
            throw new InvalidOperationException($"Provider with ID {providerId} not found.");
        }

        var product = new Product
        {
            ProviderId = providerId,
            ExternalId = externalId,
            Name = name,
            Description = description,
            Price = price,
            Currency = currency ?? "EUR",
            CategoryId = categoryId,
            ProductUrl = productUrl,
            CreatedAt = DateTime.UtcNow
        };

        await _productRepository.AddAsync(product, cancellationToken);
        return product.Id;
    }

    public async Task<ProductDto?> UpdateProductAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetWithImagesAsync(id, cancellationToken);
        if (product is null)
        {
            return null;
        }

        // Validate provider exists
        var providerExists = await _providerRepository.ExistsAsync(request.ProviderId, cancellationToken);
        if (!providerExists)
        {
            throw new InvalidOperationException($"Provider with ID '{request.ProviderId}' does not exist.");
        }

        // Validate category exists if provided
        if (request.CategoryId.HasValue)
        {
            var categoryExists = await _categoryRepository.ExistsAsync(request.CategoryId.Value, cancellationToken);
            if (!categoryExists)
            {
                throw new InvalidOperationException($"Category with ID '{request.CategoryId.Value}' does not exist.");
            }
        }

        product.Name = request.Name;
        product.Description = request.Description;
        product.Price = request.Price ?? 0;
        product.Currency = request.Currency ?? "EUR";
        product.ProductUrl = request.ProductUrl;
        product.ProviderId = request.ProviderId;
        product.CategoryId = request.CategoryId;

        await _productRepository.UpdateAsync(product, cancellationToken);

        // Reload with relations
        var updatedProduct = await _productRepository.GetWithImagesAsync(id, cancellationToken);
        return MapToDto(updatedProduct!);
    }

    /// <summary>
    /// Updates a product with partial data for admin.
    /// </summary>
    public async Task<int?> UpdateAdminProductAsync(
        int id,
        int? providerId,
        string? externalId,
        string? name,
        string? description,
        decimal? price,
        string? currency,
        int? categoryId,
        string? productUrl,
        CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return null;
        }

        if (providerId.HasValue)
        {
            var providerExists = await _providerRepository.ExistsAsync(providerId.Value, cancellationToken);
            if (!providerExists)
            {
                throw new InvalidOperationException($"Provider with ID {providerId.Value} not found.");
            }
            product.ProviderId = providerId.Value;
        }

        if (externalId is not null)
        {
            product.ExternalId = externalId;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            product.Name = name;
        }

        if (description is not null)
        {
            product.Description = description;
        }

        if (price.HasValue)
        {
            product.Price = price.Value;
        }

        if (currency is not null)
        {
            product.Currency = currency;
        }

        if (categoryId.HasValue)
        {
            if (categoryId.Value > 0)
            {
                var categoryExists = await _categoryRepository.ExistsAsync(categoryId.Value, cancellationToken);
                if (!categoryExists)
                {
                    throw new InvalidOperationException($"Category with ID {categoryId.Value} not found.");
                }
            }
            product.CategoryId = categoryId.Value > 0 ? categoryId.Value : null;
        }

        if (productUrl is not null)
        {
            product.ProductUrl = productUrl;
        }

        await _productRepository.UpdateAsync(product, cancellationToken);
        return product.Id;
    }

    public async Task<bool> DeleteProductAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return false;
        }

        await _productRepository.DeleteAsync(product, cancellationToken);
        return true;
    }

    /// <summary>
    /// Deletes a product and all its images (cascade delete).
    /// </summary>
    public async Task<bool> DeleteAdminProductAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetWithImagesAsync(id, cancellationToken);
        if (product is null)
        {
            return false;
        }

        // Delete all images first
        foreach (var image in product.Images)
        {
            await _productImageRepository.DeleteAsync(image, cancellationToken);
        }

        // Delete product
        await _productRepository.DeleteAsync(product, cancellationToken);
        return true;
    }

    public async Task<IEnumerable<ProductDto>> GetProductsByProviderAsync(int providerId, CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.GetByProviderAsync(providerId, cancellationToken);
        return products.Select(MapToDto);
    }

    public async Task<IEnumerable<ProductDto>> GetProductsByCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.GetByCategoryAsync(categoryId, cancellationToken);
        return products.Select(MapToDto);
    }

    private static ProductDto MapToDto(Product product)
    {
        return new ProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.Currency,
            product.ProductUrl,
            product.ProviderId,
            product.Provider?.Name ?? "Unknown",
            product.CategoryId,
            product.Category?.Name,
            product.CreatedAt,
            null, // UpdatedAt
            product.Images.Select(i => new ProductImageDto(
                i.Id,
                i.ImageUrl,
                null, // AltText
                i.IsPrimary,
                i.CreatedAt
            ))
        );
    }

    private static AdminProductDto MapToAdminDto(Product product)
    {
        return new AdminProductDto
        {
            Id = product.Id,
            ProviderId = product.ProviderId,
            ProviderName = product.Provider?.Name ?? "Unknown",
            ExternalId = product.ExternalId,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            Currency = product.Currency,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name,
            ProductUrl = product.ProductUrl,
            CreatedAt = product.CreatedAt,
            Images = product.Images.Select(i => new AdminProductImageDto
            {
                Id = i.Id,
                ImageUrl = i.ImageUrl,
                LocalPath = i.LocalPath,
                IsLocalFile = i.LocalPath is not null,
                IsPrimary = i.IsPrimary,
                HasEmbedding = i.Embedding is not null,
                CreatedAt = i.CreatedAt
            }).ToList()
        };
    }

    private static ProductSummaryDto MapToSummaryDto(Product product)
    {
        var primaryImage = product.Images.FirstOrDefault(i => i.IsPrimary)
            ?? product.Images.FirstOrDefault();

        return new ProductSummaryDto(
            product.Id,
            product.Name,
            product.Price,
            product.Currency,
            product.Provider?.Name ?? "Unknown",
            product.Category?.Name,
            primaryImage?.ImageUrl
        );
    }
}
