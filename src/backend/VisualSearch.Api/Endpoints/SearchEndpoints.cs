using System.Buffers.Binary;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using VisualSearch.Api.Data;

namespace VisualSearch.Api.Endpoints;

/// <summary>
/// Search endpoints for visual similarity search using binary protocol.
/// </summary>
public static class SearchEndpoints
{
    private const int EmbeddingDimension = 512;
    private const int MaxResultsPerObject = 8;

    /// <summary>
    /// Maps the search endpoints to the application.
    /// </summary>
    /// <param name="app">The web application.</param>
    public static void MapSearchEndpoints(this WebApplication app)
    {
        app.MapPost("/api/busca", HandleBinarySearchAsync)
            .Accepts<byte[]>("application/octet-stream")
            .Produces<byte[]>(200, "application/octet-stream")
            .WithName("BinarySearch")
            .WithDescription("Performs visual similarity search using binary protocol for maximum performance.")
            .WithTags("Search");
    }

    /// <summary>
    /// Handles binary search request.
    /// Binary protocol format (request):
    ///   int32: number of objects
    ///   for each object:
    ///     int32: label length in bytes
    ///     byte[]: UTF-8 encoded label
    ///     float32[512]: normalized embedding vector
    /// 
    /// Binary protocol format (response):
    ///   int32: number of objects
    ///   for each object:
    ///     int32: label length in bytes
    ///     byte[]: UTF-8 encoded label
    ///     int32: number of results (max 8)
    ///     for each result:
    ///       int32: product id
    ///       float32: similarity score
    ///       int32: name length
    ///       byte[]: UTF-8 encoded name
    ///       float32: price (as float for simplicity)
    ///       int32: image URL length
    ///       byte[]: UTF-8 encoded image URL
    ///       int32: provider name length
    ///       byte[]: UTF-8 encoded provider name
    /// </summary>
    private static async Task<IResult> HandleBinarySearchAsync(
        HttpRequest request,
        VisualSearchDbContext dbContext,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Read request body into memory
            using var memoryStream = new MemoryStream();
            await request.Body.CopyToAsync(memoryStream, cancellationToken);
            var requestBytes = memoryStream.ToArray();

            if (requestBytes.Length < 4)
            {
                logger.LogWarning("Invalid request: too short ({Length} bytes)", requestBytes.Length);
                return Results.BadRequest();
            }

            // Parse binary request
            var (searchRequests, parseError) = ParseBinaryRequest(requestBytes, logger);

            if (parseError != null)
            {
                logger.LogWarning("Failed to parse binary request: {Error}", parseError);
                return Results.BadRequest(new { error = parseError });
            }

            if (searchRequests.Count == 0)
            {
                logger.LogWarning("No valid search objects in request");
                return Results.BadRequest(new { error = "No valid search objects in request" });
            }

            logger.LogInformation("Received search request with {Count} objects", searchRequests.Count);

            // Perform similarity search for each object
            var searchResults = new List<ObjectSearchResult>();

            foreach (var searchRequest in searchRequests)
            {
                var queryVector = new Vector(searchRequest.Embedding);

                var results = await dbContext.ProductImages
                    .Include(pi => pi.Product)
                        .ThenInclude(p => p!.Provider)
                    .Where(pi => pi.Embedding != null)
                    .OrderBy(pi => pi.Embedding!.CosineDistance(queryVector))
                    .Take(MaxResultsPerObject)
                    .Select(pi => new ProductSearchResult
                    {
                        ProductId = pi.Product!.Id,
                        Score = (float)(1 - pi.Embedding!.CosineDistance(queryVector)),
                        Name = pi.Product.Name,
                        Price = (float)pi.Product.Price,
                        ImageUrl = pi.ImageUrl,
                        ProviderName = pi.Product.Provider!.Name
                    })
                    .ToListAsync(cancellationToken);

                searchResults.Add(new ObjectSearchResult
                {
                    Label = searchRequest.Label,
                    Results = results
                });
            }

            // Serialize binary response
            var responseBytes = SerializeBinaryResponse(searchResults);

            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            logger.LogInformation("Search completed in {Elapsed:F2}ms for {Count} objects, returning {ResultCount} total results",
                elapsed, searchRequests.Count, searchResults.Sum(r => r.Results.Count));

            return Results.Bytes(responseBytes, "application/octet-stream");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing binary search request");
            return Results.StatusCode(500);
        }
    }

    /// <summary>
    /// Parses the binary request format into search request objects.
    /// </summary>
    private static (List<SearchRequest> Requests, string? Error) ParseBinaryRequest(byte[] data, ILogger logger)
    {
        var requests = new List<SearchRequest>();
        var offset = 0;

        logger.LogDebug("Parsing binary request of {Length} bytes", data.Length);

        // Read number of objects
        if (data.Length < 4)
        {
            return (requests, $"Request too short: {data.Length} bytes, need at least 4");
        }

        var objectCount = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
        offset += 4;

        logger.LogDebug("Object count: {Count}", objectCount);

        if (objectCount <= 0 || objectCount > 100)
        {
            return (requests, $"Invalid object count: {objectCount}");
        }

        for (int i = 0; i < objectCount && offset < data.Length; i++)
        {
            // Read label length
            if (offset + 4 > data.Length)
            {
                return (requests, $"Unexpected end of data reading label length for object {i}");
            }

            var labelLength = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset));
            offset += 4;

            if (labelLength < 0 || labelLength > 1000)
            {
                return (requests, $"Invalid label length {labelLength} for object {i}");
            }

            // Read label
            if (offset + labelLength > data.Length)
            {
                return (requests, $"Unexpected end of data reading label for object {i}");
            }

            var label = Encoding.UTF8.GetString(data, offset, labelLength);
            offset += labelLength;

            // Read embedding (512 floats = 2048 bytes)
            var embeddingBytes = EmbeddingDimension * sizeof(float);
            if (offset + embeddingBytes > data.Length)
            {
                var remainingBytes = data.Length - offset;
                var remainingFloats = remainingBytes / sizeof(float);
                return (requests, $"Not enough data for embedding of object {i} ('{label}'). Expected {embeddingBytes} bytes ({EmbeddingDimension} floats), got {remainingBytes} bytes ({remainingFloats} floats)");
            }

            var embedding = new float[EmbeddingDimension];
            for (int j = 0; j < EmbeddingDimension; j++)
            {
                embedding[j] = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset));
                offset += sizeof(float);
            }

            logger.LogDebug("Parsed object {Index}: '{Label}' with embedding magnitude {Magnitude:F4}", 
                i, label, Math.Sqrt(embedding.Sum(x => x * x)));

            requests.Add(new SearchRequest
            {
                Label = label,
                Embedding = embedding
            });
        }

        return (requests, null);
    }

    /// <summary>
    /// Serializes search results into binary response format.
    /// </summary>
    private static byte[] SerializeBinaryResponse(List<ObjectSearchResult> results)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        // Write number of objects
        writer.Write(results.Count);

        foreach (var objectResult in results)
        {
            // Write label
            var labelBytes = Encoding.UTF8.GetBytes(objectResult.Label);
            writer.Write(labelBytes.Length);
            writer.Write(labelBytes);

            // Write number of results
            writer.Write(objectResult.Results.Count);

            foreach (var result in objectResult.Results)
            {
                // Write product ID
                writer.Write(result.ProductId);

                // Write score
                writer.Write(result.Score);

                // Write name
                var nameBytes = Encoding.UTF8.GetBytes(result.Name);
                writer.Write(nameBytes.Length);
                writer.Write(nameBytes);

                // Write price
                writer.Write(result.Price);

                // Write image URL
                var imageUrlBytes = Encoding.UTF8.GetBytes(result.ImageUrl);
                writer.Write(imageUrlBytes.Length);
                writer.Write(imageUrlBytes);

                // Write provider name
                var providerNameBytes = Encoding.UTF8.GetBytes(result.ProviderName);
                writer.Write(providerNameBytes.Length);
                writer.Write(providerNameBytes);
            }
        }

        return stream.ToArray();
    }

    private sealed class SearchRequest
    {
        public required string Label { get; set; }
        public required float[] Embedding { get; set; }
    }

    private sealed class ObjectSearchResult
    {
        public required string Label { get; set; }
        public required List<ProductSearchResult> Results { get; set; }
    }

    private sealed class ProductSearchResult
    {
        public int ProductId { get; set; }
        public float Score { get; set; }
        public required string Name { get; set; }
        public float Price { get; set; }
        public required string ImageUrl { get; set; }
        public required string ProviderName { get; set; }
    }
}
