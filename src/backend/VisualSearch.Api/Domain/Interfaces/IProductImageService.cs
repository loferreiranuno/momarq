using VisualSearch.Api.Contracts.DTOs;
using VisualSearch.Api.Contracts.Requests;

namespace VisualSearch.Api.Domain.Interfaces;

/// <summary>
/// Application service interface for product image operations.
/// </summary>
public interface IProductImageService
{
    /// <summary>
    /// Gets an image by ID.
    /// </summary>
    Task<ProductImageDto?> GetByIdAsync(int imageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all images for a product.
    /// </summary>
    Task<IEnumerable<ProductImageDto>> GetByProductIdAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new image to a product from a URL.
    /// </summary>
    Task<ProductImageDto> AddFromUrlAsync(int productId, string imageUrl, bool isPrimary = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new image to a product from a URL with auto-vectorization.
    /// Returns the admin-specific DTO with embedding info.
    /// </summary>
    Task<AdminProductImageDto> AddFromUrlWithVectorizationAsync(int productId, string imageUrl, bool isPrimary = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads an image file to a product.
    /// </summary>
    Task<ProductImageDto> UploadAsync(int productId, Stream imageStream, string fileName, bool isPrimary = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads an image file to a product with auto-vectorization.
    /// Returns the admin-specific DTO with embedding info.
    /// </summary>
    Task<AdminProductImageDto> UploadWithVectorizationAsync(int productId, Stream imageStream, string fileName, bool isPrimary = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads an image from URL and saves it locally.
    /// </summary>
    Task<ProductImageDto> DownloadAndSaveAsync(int productId, string imageUrl, bool isPrimary = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads an image from URL and saves it locally with auto-vectorization.
    /// Returns the admin-specific DTO with embedding info.
    /// </summary>
    Task<AdminProductImageDto> DownloadAndSaveWithVectorizationAsync(int productId, string imageUrl, bool isPrimary = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an image.
    /// </summary>
    Task<ProductImageDto?> UpdateAsync(int imageId, UpdateProductImageRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an image with optional URL change and re-vectorization.
    /// </summary>
    Task<AdminProductImageDto?> UpdateWithUrlChangeAsync(int productId, int imageId, string? imageUrl, bool? isPrimary, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an image.
    /// </summary>
    Task<bool> DeleteAsync(int imageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an image belonging to a specific product.
    /// </summary>
    Task<bool> DeleteByProductAsync(int productId, int imageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embedding for a single image.
    /// </summary>
    Task<bool> VectorizeImageAsync(int imageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embeddings for all images of a product.
    /// </summary>
    Task<int> VectorizeProductImagesAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embeddings for all images without embeddings.
    /// </summary>
    Task<VectorizationResultDto> VectorizeAllAsync(IProgress<VectorizationProgressDto>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embeddings for all images (optionally including those with existing embeddings).
    /// Returns admin-specific result DTO.
    /// </summary>
    Task<AllVectorizationResultDto> VectorizeAllAdminAsync(bool force = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if vectorization service is available.
    /// </summary>
    bool IsVectorizationAvailable { get; }
}
