namespace VisualSearch.Api.Domain.Interfaces;

public interface IObjectDetectionService
{
    Task<IEnumerable<DetectedObject>> DetectObjectsAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
    Task<IEnumerable<DetectedObject>> DetectObjectsAsync(Stream imageStream, CancellationToken cancellationToken = default);
    Task<byte[]> CropDetectedObjectAsync(byte[] imageBytes, DetectedObject detection, CancellationToken cancellationToken = default);
    bool IsModelLoaded { get; }
}

public record DetectedObject(
    string Label,
    int ClassId,
    float Confidence,
    BoundingBox BoundingBox
);

public record BoundingBox(
    float X,
    float Y,
    float Width,
    float Height
);
