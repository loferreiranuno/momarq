using System.Diagnostics;
using Pgvector;
using VisualSearch.Api.Contracts.DTOs;
using VisualSearch.Api.Contracts.Requests;
using VisualSearch.Api.Data.Entities;
using VisualSearch.Api.Domain.Interfaces;
using VisualSearch.Api.Services;

namespace VisualSearch.Api.Application.Services;

/// <summary>
/// Application service for product image operations.
/// Orchestrates repository, upload, and vectorization services.
/// </summary>
public sealed class ProductImageService : IProductImageService
{
    private readonly IProductImageRepository _imageRepository;
    private readonly IProductRepository _productRepository;
    private readonly ImageUploadService _uploadService;
    private readonly VectorizationService _vectorizationService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ProductImageService> _logger;

    public ProductImageService(
        IProductImageRepository imageRepository,
        IProductRepository productRepository,
        ImageUploadService uploadService,
        VectorizationService vectorizationService,
        IHttpClientFactory httpClientFactory,
        ILogger<ProductImageService> logger)
    {
        _imageRepository = imageRepository;
        _productRepository = productRepository;
        _uploadService = uploadService;
        _vectorizationService = vectorizationService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ProductImageDto?> GetByIdAsync(int imageId, CancellationToken cancellationToken = default)
    {
        var image = await _imageRepository.GetByIdAsync(imageId, cancellationToken);
        return image is null ? null : MapToDto(image);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ProductImageDto>> GetByProductIdAsync(int productId, CancellationToken cancellationToken = default)
    {
        var images = await _imageRepository.GetByProductAsync(productId, cancellationToken);
        return images.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<ProductImageDto> AddFromUrlAsync(
        int productId,
        string imageUrl,
        bool isPrimary = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageUrl);

        if (!await _productRepository.ExistsAsync(productId, cancellationToken))
        {
            throw new InvalidOperationException($"Product with ID {productId} not found.");
        }

        // If this is the primary image, clear other primaries
        if (isPrimary)
        {
            await ClearPrimaryImagesAsync(productId, cancellationToken);
        }

        var image = new ProductImage
        {
            ProductId = productId,
            ImageUrl = imageUrl,
            IsPrimary = isPrimary,
            CreatedAt = DateTime.UtcNow
        };

        await _imageRepository.AddAsync(image, cancellationToken);

        _logger.LogInformation("Added image from URL for product {ProductId}: {ImageUrl}", productId, imageUrl);

        return MapToDto(image);
    }

    /// <inheritdoc />
    public async Task<ProductImageDto> UploadAsync(
        int productId,
        Stream imageStream,
        string fileName,
        bool isPrimary = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        ArgumentNullException.ThrowIfNull(fileName);

        if (!await _productRepository.ExistsAsync(productId, cancellationToken))
        {
            throw new InvalidOperationException($"Product with ID {productId} not found.");
        }

        // If this is the primary image, clear other primaries
        if (isPrimary)
        {
            await ClearPrimaryImagesAsync(productId, cancellationToken);
        }

        // Save the image to local storage
        var (relativePath, _) = await _uploadService.SaveImageAsync(imageStream, fileName, cancellationToken);

        var image = new ProductImage
        {
            ProductId = productId,
            ImageUrl = $"/uploads/{relativePath}",
            LocalPath = relativePath,
            IsPrimary = isPrimary,
            CreatedAt = DateTime.UtcNow
        };

        await _imageRepository.AddAsync(image, cancellationToken);

        _logger.LogInformation("Uploaded image for product {ProductId}: {LocalPath}", productId, relativePath);

        return MapToDto(image);
    }

    /// <inheritdoc />
    public async Task<ProductImageDto> DownloadAndSaveAsync(
        int productId,
        string imageUrl,
        bool isPrimary = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageUrl);

        if (!await _productRepository.ExistsAsync(productId, cancellationToken))
        {
            throw new InvalidOperationException($"Product with ID {productId} not found.");
        }

        // Download image from URL
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        byte[] imageBytes;
        try
        {
            imageBytes = await httpClient.GetByteArrayAsync(imageUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download image from {ImageUrl}", imageUrl);
            throw new InvalidOperationException($"Failed to download image from URL: {imageUrl}", ex);
        }

        // If this is the primary image, clear other primaries
        if (isPrimary)
        {
            await ClearPrimaryImagesAsync(productId, cancellationToken);
        }

        // Save to local storage
        var (relativePath, _) = await _uploadService.SaveImageBytesAsync(imageBytes, "downloaded.jpg", cancellationToken);

        var image = new ProductImage
        {
            ProductId = productId,
            ImageUrl = $"/uploads/{relativePath}",
            LocalPath = relativePath,
            IsPrimary = isPrimary,
            CreatedAt = DateTime.UtcNow
        };

        await _imageRepository.AddAsync(image, cancellationToken);

        _logger.LogInformation("Downloaded and saved image for product {ProductId} from {OriginalUrl} to {LocalPath}",
            productId, imageUrl, relativePath);

        return MapToDto(image);
    }

    /// <inheritdoc />
    public async Task<ProductImageDto?> UpdateAsync(
        int imageId,
        UpdateProductImageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var image = await _imageRepository.GetByIdAsync(imageId, cancellationToken);
        if (image is null)
        {
            return null;
        }

        // If setting as primary, clear other primaries first
        if (request.IsPrimary && !image.IsPrimary)
        {
            await ClearPrimaryImagesAsync(image.ProductId, cancellationToken);
        }

        image.IsPrimary = request.IsPrimary;

        await _imageRepository.UpdateAsync(image, cancellationToken);

        _logger.LogInformation("Updated image {ImageId}", imageId);

        return MapToDto(image);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int imageId, CancellationToken cancellationToken = default)
    {
        var image = await _imageRepository.GetByIdAsync(imageId, cancellationToken);
        if (image is null)
        {
            return false;
        }

        // Delete local file if exists
        if (!string.IsNullOrWhiteSpace(image.LocalPath))
        {
            _uploadService.DeleteImage(image.LocalPath);
        }

        await _imageRepository.DeleteAsync(image, cancellationToken);

        _logger.LogInformation("Deleted image {ImageId}", imageId);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> VectorizeImageAsync(int imageId, CancellationToken cancellationToken = default)
    {
        var image = await _imageRepository.GetByIdAsync(imageId, cancellationToken);
        if (image is null)
        {
            _logger.LogWarning("Image {ImageId} not found for vectorization", imageId);
            return false;
        }

        float[]? embedding;

        // Prefer local file if available
        if (!string.IsNullOrWhiteSpace(image.LocalPath))
        {
            var imageBytes = await _uploadService.ReadImageAsync(image.LocalPath, cancellationToken);
            if (imageBytes is null)
            {
                _logger.LogWarning("Local file not found for image {ImageId}: {LocalPath}", imageId, image.LocalPath);
                return false;
            }

            embedding = await _vectorizationService.GenerateEmbeddingAsync(imageBytes, cancellationToken);
        }
        else
        {
            embedding = await _vectorizationService.GenerateEmbeddingFromUrlAsync(image.ImageUrl, cancellationToken);
        }

        if (embedding is null)
        {
            _logger.LogWarning("Failed to generate embedding for image {ImageId}", imageId);
            return false;
        }

        image.Embedding = new Vector(embedding);
        await _imageRepository.UpdateAsync(image, cancellationToken);

        _logger.LogInformation("Vectorized image {ImageId}", imageId);

        return true;
    }

    /// <inheritdoc />
    public async Task<int> VectorizeProductImagesAsync(int productId, CancellationToken cancellationToken = default)
    {
        var images = await _imageRepository.GetByProductAsync(productId, cancellationToken);
        var successCount = 0;

        foreach (var image in images)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (await VectorizeImageInternalAsync(image, cancellationToken))
            {
                successCount++;
            }
        }

        _logger.LogInformation("Vectorized {Count} images for product {ProductId}", successCount, productId);

        return successCount;
    }

    /// <inheritdoc />
    public async Task<VectorizationResultDto> VectorizeAllAsync(
        IProgress<VectorizationProgressDto>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Get all images without embeddings
        var allImages = new List<ProductImage>();

        // We need to load images in batches since the repository doesn't expose a query
        // This is a limitation - ideally we'd add a method to get images without embeddings
        var productIds = await GetAllProductIdsAsync(cancellationToken);
        foreach (var productId in productIds)
        {
            var productImages = await _imageRepository.GetByProductAsync(productId, cancellationToken);
            allImages.AddRange(productImages.Where(i => i.Embedding is null));
        }

        var total = allImages.Count;
        var successCount = 0;
        var failedCount = 0;

        _logger.LogInformation("Starting batch vectorization of {Count} images", total);

        for (var i = 0; i < allImages.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var image = allImages[i];

            progress?.Report(new VectorizationProgressDto(
                Current: i + 1,
                Total: total,
                CurrentImageUrl: image.ImageUrl,
                Status: "Processing"
            ));

            if (await VectorizeImageInternalAsync(image, cancellationToken))
            {
                successCount++;
            }
            else
            {
                failedCount++;
            }
        }

        stopwatch.Stop();

        progress?.Report(new VectorizationProgressDto(
            Current: total,
            Total: total,
            CurrentImageUrl: null,
            Status: "Completed"
        ));

        _logger.LogInformation(
            "Batch vectorization completed: {Success}/{Total} successful, {Failed} failed, {Duration}ms",
            successCount, total, failedCount, stopwatch.ElapsedMilliseconds);

        return new VectorizationResultDto(
            TotalProcessed: total,
            Successful: successCount,
            Failed: failedCount,
            Duration: stopwatch.Elapsed
        );
    }

    private async Task<bool> VectorizeImageInternalAsync(ProductImage image, CancellationToken cancellationToken)
    {
        float[]? embedding;

        // Prefer local file if available
        if (!string.IsNullOrWhiteSpace(image.LocalPath))
        {
            var imageBytes = await _uploadService.ReadImageAsync(image.LocalPath, cancellationToken);
            if (imageBytes is null)
            {
                _logger.LogWarning("Local file not found for image {ImageId}: {LocalPath}", image.Id, image.LocalPath);
                return false;
            }

            embedding = await _vectorizationService.GenerateEmbeddingAsync(imageBytes, cancellationToken);
        }
        else
        {
            embedding = await _vectorizationService.GenerateEmbeddingFromUrlAsync(image.ImageUrl, cancellationToken);
        }

        if (embedding is null)
        {
            _logger.LogWarning("Failed to generate embedding for image {ImageId}", image.Id);
            return false;
        }

        image.Embedding = new Vector(embedding);
        await _imageRepository.UpdateAsync(image, cancellationToken);

        return true;
    }

    private async Task ClearPrimaryImagesAsync(int productId, CancellationToken cancellationToken)
    {
        var images = await _imageRepository.GetByProductAsync(productId, cancellationToken);
        foreach (var image in images.Where(i => i.IsPrimary))
        {
            image.IsPrimary = false;
            await _imageRepository.UpdateAsync(image, cancellationToken);
        }
    }

    private async Task<IEnumerable<int>> GetAllProductIdsAsync(CancellationToken cancellationToken)
    {
        // This is a workaround - ideally the repository would expose this
        // For now, we'll use the product repository
        var products = await _productRepository.GetAllAsync(cancellationToken);
        return products.Select(p => p.Id);
    }

    private static ProductImageDto MapToDto(ProductImage image)
    {
        return new ProductImageDto(
            Id: image.Id,
            ImageUrl: image.ImageUrl,
            AltText: null, // ProductImage entity doesn't have AltText
            IsPrimary: image.IsPrimary,
            CreatedAt: image.CreatedAt
        );
    }
}
