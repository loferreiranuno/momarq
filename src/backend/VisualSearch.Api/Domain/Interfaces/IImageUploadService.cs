namespace VisualSearch.Api.Domain.Interfaces;

public interface IImageUploadService
{
    Task<string> UploadImageAsync(byte[] imageBytes, string fileName, CancellationToken cancellationToken = default);
    Task<string> UploadImageAsync(Stream imageStream, string fileName, CancellationToken cancellationToken = default);
    Task DeleteImageAsync(string imagePath, CancellationToken cancellationToken = default);
    Task<byte[]> GetImageBytesAsync(string imagePath, CancellationToken cancellationToken = default);
    string GetImageUrl(string imagePath);
}
