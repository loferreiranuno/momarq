using Pgvector;
using VisualSearch.Api.Domain.Interfaces;
using VisualSearch.Api.Services;

namespace VisualSearch.Api.Extensions;

/// <summary>
/// Adapter to make ClipEmbeddingService implement IClipEmbeddingService interface.
/// </summary>
public sealed class ClipEmbeddingServiceAdapter : IClipEmbeddingService
{
    private readonly ClipEmbeddingService _innerService;

    public ClipEmbeddingServiceAdapter(ClipEmbeddingService innerService)
    {
        _innerService = innerService;
    }

    public bool IsModelLoaded => _innerService.IsModelLoaded;

    public async Task<Vector> GetImageEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        var embedding = await _innerService.GenerateEmbeddingAsync(imageBytes, cancellationToken);
        if (embedding is null)
        {
            throw new InvalidOperationException("Failed to generate image embedding. Model may not be loaded.");
        }
        return new Vector(embedding);
    }

    public async Task<Vector> GetImageEmbeddingAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream, cancellationToken);
        return await GetImageEmbeddingAsync(memoryStream.ToArray(), cancellationToken);
    }

    public Task<Vector> GetTextEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        // CLIP text encoding not yet implemented in the base service
        throw new NotImplementedException("Text embedding generation is not yet implemented.");
    }
}
