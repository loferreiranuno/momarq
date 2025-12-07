using Pgvector;

namespace VisualSearch.Api.Domain.Interfaces;

public interface IClipEmbeddingService
{
    Task<Vector> GetImageEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
    Task<Vector> GetImageEmbeddingAsync(Stream imageStream, CancellationToken cancellationToken = default);
    Task<Vector> GetTextEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    bool IsModelLoaded { get; }
}
