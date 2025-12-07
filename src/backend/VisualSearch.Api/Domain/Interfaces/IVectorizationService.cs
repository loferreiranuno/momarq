using Pgvector;

namespace VisualSearch.Api.Domain.Interfaces;

public interface IVectorizationService
{
    Task<Vector> VectorizeImageAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
    Task<Vector> VectorizeImageAsync(Stream imageStream, CancellationToken cancellationToken = default);
    Task<Vector> VectorizeImageFromUrlAsync(string imageUrl, CancellationToken cancellationToken = default);
}
