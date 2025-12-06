using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace VisualSearch.Api.Services;

/// <summary>
/// Service for handling image uploads with resize and compression.
/// Organizes files in subfolders by date for better filesystem performance.
/// </summary>
public sealed class ImageUploadService
{
    private readonly ILogger<ImageUploadService> _logger;
    private readonly string _uploadsPath;
    private const int MaxImageWidth = 800;
    private const int MaxImageHeight = 800;
    private const int JpegQuality = 85;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageUploadService"/> class.
    /// </summary>
    public ImageUploadService(
        ILogger<ImageUploadService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _uploadsPath = configuration.GetValue<string>("Uploads:Path") ?? "/app/uploads";
    }

    /// <summary>
    /// Gets the base uploads path.
    /// </summary>
    public string UploadsPath => _uploadsPath;

    /// <summary>
    /// Saves an uploaded image with automatic resize and compression.
    /// Files are organized in subfolders: /uploads/products/{year}/{month}/{guid}.jpg
    /// </summary>
    /// <param name="fileStream">The uploaded file stream.</param>
    /// <param name="originalFileName">The original file name (for extension detection).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A tuple containing the relative path and the processed image bytes.</returns>
    public async Task<(string RelativePath, byte[] ImageBytes)> SaveImageAsync(
        Stream fileStream,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        // Generate organized path: products/{year}/{month}/{guid}.jpg
        var now = DateTime.UtcNow;
        var relativePath = Path.Combine(
            "products",
            now.Year.ToString(),
            now.Month.ToString("D2"),
            $"{Guid.NewGuid()}.jpg");

        var fullPath = Path.Combine(_uploadsPath, relativePath);
        var directory = Path.GetDirectoryName(fullPath)!;

        // Ensure directory exists
        Directory.CreateDirectory(directory);

        // Process image
        byte[] processedBytes;
        using (var image = await Image.LoadAsync(fileStream, cancellationToken))
        {
            // Resize if needed (maintain aspect ratio)
            if (image.Width > MaxImageWidth || image.Height > MaxImageHeight)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(MaxImageWidth, MaxImageHeight),
                    Mode = ResizeMode.Max
                }));

                _logger.LogDebug("Resized image from {OriginalWidth}x{OriginalHeight} to {NewWidth}x{NewHeight}",
                    image.Width, image.Height, image.Width, image.Height);
            }

            // Encode as JPEG with compression
            using var outputStream = new MemoryStream();
            await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = JpegQuality }, cancellationToken);
            processedBytes = outputStream.ToArray();
        }

        // Save to disk
        await File.WriteAllBytesAsync(fullPath, processedBytes, cancellationToken);

        _logger.LogInformation("Saved uploaded image to {Path} ({Size} bytes)", 
            relativePath, processedBytes.Length);

        // Return path with forward slashes for URLs
        return (relativePath.Replace('\\', '/'), processedBytes);
    }

    /// <summary>
    /// Saves image bytes directly (for programmatic uploads).
    /// </summary>
    /// <param name="imageBytes">The image bytes.</param>
    /// <param name="originalFileName">Optional original filename for extension detection.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A tuple containing the relative path and the processed image bytes.</returns>
    public async Task<(string RelativePath, byte[] ImageBytes)> SaveImageBytesAsync(
        byte[] imageBytes,
        string originalFileName = "image.jpg",
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(imageBytes);
        return await SaveImageAsync(stream, originalFileName, cancellationToken);
    }

    /// <summary>
    /// Reads an image from the local storage.
    /// </summary>
    /// <param name="relativePath">The relative path to the image.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The image bytes, or null if the file doesn't exist.</returns>
    public async Task<byte[]?> ReadImageAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_uploadsPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("Image file not found: {Path}", relativePath);
            return null;
        }

        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    /// <summary>
    /// Deletes an image from local storage.
    /// </summary>
    /// <param name="relativePath">The relative path to the image.</param>
    /// <returns>True if deleted, false if file didn't exist.</returns>
    public bool DeleteImage(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var fullPath = Path.Combine(_uploadsPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            File.Delete(fullPath);
            _logger.LogInformation("Deleted image: {Path}", relativePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete image: {Path}", relativePath);
            return false;
        }
    }

    /// <summary>
    /// Gets the full filesystem path for a relative path.
    /// </summary>
    /// <param name="relativePath">The relative path.</param>
    /// <returns>The full filesystem path.</returns>
    public string GetFullPath(string relativePath)
    {
        return Path.Combine(_uploadsPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// Checks if an image exists in local storage.
    /// </summary>
    /// <param name="relativePath">The relative path to check.</param>
    /// <returns>True if exists, false otherwise.</returns>
    public bool ImageExists(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var fullPath = Path.Combine(_uploadsPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(fullPath);
    }
}
