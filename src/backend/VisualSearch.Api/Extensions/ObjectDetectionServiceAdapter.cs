using DomainInterfaces = VisualSearch.Api.Domain.Interfaces;
using InternalServices = VisualSearch.Api.Services;

namespace VisualSearch.Api.Extensions;

/// <summary>
/// Adapter to make ObjectDetectionService implement IObjectDetectionService interface.
/// </summary>
public sealed class ObjectDetectionServiceAdapter : DomainInterfaces.IObjectDetectionService
{
    private readonly InternalServices.ObjectDetectionService _innerService;

    public ObjectDetectionServiceAdapter(InternalServices.ObjectDetectionService innerService)
    {
        _innerService = innerService;
    }

    public bool IsModelLoaded => _innerService.IsModelLoaded;

    public async Task<IEnumerable<DomainInterfaces.DetectedObject>> DetectObjectsAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        var detections = await _innerService.DetectObjectsAsync(imageBytes, cancellationToken);
        return detections.Select(d => new DomainInterfaces.DetectedObject(
            d.ClassName,
            d.ClassId,
            d.Confidence,
            new DomainInterfaces.BoundingBox(d.X1, d.Y1, d.X2 - d.X1, d.Y2 - d.Y1)
        ));
    }

    public async Task<IEnumerable<DomainInterfaces.DetectedObject>> DetectObjectsAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream, cancellationToken);
        return await DetectObjectsAsync(memoryStream.ToArray(), cancellationToken);
    }

    public Task<byte[]> CropDetectedObjectAsync(byte[] imageBytes, DomainInterfaces.DetectedObject detection, CancellationToken cancellationToken = default)
    {
        // Convert back to internal format
        var internalDetection = new InternalServices.DetectedObject
        {
            ClassName = detection.Label,
            ClassId = detection.ClassId,
            Confidence = detection.Confidence,
            X1 = (int)detection.BoundingBox.X,
            Y1 = (int)detection.BoundingBox.Y,
            X2 = (int)(detection.BoundingBox.X + detection.BoundingBox.Width),
            Y2 = (int)(detection.BoundingBox.Y + detection.BoundingBox.Height)
        };

        // Use the existing CropDetections method for a single detection
        var crops = _innerService.CropDetections(imageBytes, [internalDetection]);
        return Task.FromResult(crops.FirstOrDefault() ?? []);
    }
}
