# AMOMAR Visual Search Project - Architecture Guidelines

This document defines the architecture rules and patterns for the AMOMAR Visual Search API project.

## Project Structure

The backend follows a **Clean Architecture** pattern with the following layers:

```
VisualSearch.Api/
├── Domain/                     # Domain layer - core business abstractions
│   └── Interfaces/             # Repository and service interfaces
├── Infrastructure/             # Infrastructure layer - data access
│   └── Repositories/           # Repository implementations (EF Core)
├── Application/                # Application layer - business logic
│   └── Services/               # Application services
├── Contracts/                  # API contracts
│   ├── DTOs/                   # Data Transfer Objects
│   └── Requests/               # Request models
├── Endpoints/                  # Minimal API endpoints (thin layer)
├── Data/                       # EF Core DbContext and entities
│   └── Entities/               # Database entities
├── Services/                   # Infrastructure services (CLIP, YOLO, etc.)
├── Extensions/                 # DI and configuration extensions
└── Migrations/                 # EF Core migrations
```

## Dependency Flow

```
Endpoints → Application Services → Repositories → DbContext
              ↓
        Infrastructure Services (CLIP, YOLO)
```

**CRITICAL RULES:**
1. **Endpoints MUST NOT access DbContext directly** - Always go through Application Services
2. **Application Services MUST NOT expose database entities** - Return DTOs only
3. **Repositories are the ONLY layer that accesses DbContext directly**

## Layer Responsibilities

### Endpoints (Minimal APIs)
- Thin layer for HTTP request/response handling
- Input validation at API boundary
- Maps between API contracts and service calls
- **NEVER** contains business logic
- **NEVER** accesses DbContext directly

```csharp
// ✅ CORRECT - Use service
app.MapGet("/api/products", async (ProductService service) =>
    Results.Ok(await service.GetAllAsync()));

// ❌ WRONG - Direct DbContext access
app.MapGet("/api/products", async (VisualSearchDbContext db) =>
    Results.Ok(await db.Products.ToListAsync()));
```

### Application Services
- Contain business logic and orchestration
- Coordinate between repositories and infrastructure services
- Return DTOs, never entities
- Handle cross-cutting concerns (logging, validation)

```csharp
public class ProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IProductImageRepository _imageRepository;
    
    public async Task<ProductDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        var product = await _productRepository.GetByIdWithImagesAsync(id, ct);
        return product?.ToDto();
    }
}
```

### Repositories
- Data access abstraction over DbContext
- CRUD operations and queries
- Return entities or projection results
- **ONLY** layer that uses DbContext

```csharp
public class ProductRepository : RepositoryBase<Product>, IProductRepository
{
    public async Task<Product?> GetByIdWithImagesAsync(int id, CancellationToken ct)
    {
        return await _dbContext.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }
}
```

### Domain Interfaces
- Define contracts for repositories and services
- Located in `Domain/Interfaces/`
- No implementation details

## Search Deduplication Rule

**CRITICAL:** When searching products by image similarity, ALWAYS deduplicate by ProductId.

Products can have multiple images, and each image has its own embedding. When searching by vector similarity, the same product may appear multiple times (once per matching image).

```csharp
// ✅ CORRECT - Deduplicate by ProductId
var results = imageResults
    .GroupBy(r => r.Image.ProductId)
    .Select(g => g.OrderBy(r => r.Distance).First())  // Keep best match per product
    .OrderBy(r => r.Distance)
    .Take(limit)
    .ToList();

// ❌ WRONG - Returns duplicates
var results = imageResults
    .OrderBy(r => r.Distance)
    .Take(limit)
    .ToList();
```

## DTOs and Contracts

- **DTOs** (`Contracts/DTOs/`): Read-only data structures returned by services
- **Requests** (`Contracts/Requests/`): Input models for create/update operations
- Use C# records for immutability

```csharp
// DTO
public record ProductDto(int Id, string Name, decimal Price, ...);

// Request
public record CreateProductRequest(string Name, decimal Price, int ProviderId, ...);
```

## Dependency Injection

All DI registration is centralized in `Extensions/ServiceCollectionExtensions.cs`:

```csharp
services.AddDatabaseServices(configuration);  // DbContext
services.AddRepositories();                   // ICategoryRepository, IProductRepository, etc.
services.AddApplicationServices();            // CategoryService, ProductService, etc.
services.AddAIServices();                     // CLIP, YOLO adapters
services.AddInfrastructureServices();         // ImageUpload, Settings
```

## AI/ML Services

### CLIP Embedding Service
- Generates 512-dimensional embeddings for images
- Interface: `IClipEmbeddingService`
- Implementation: `ClipEmbeddingService` + `ClipEmbeddingServiceAdapter`

### Object Detection Service  
- YOLO-based object detection for furniture items
- Interface: `IObjectDetectionService`
- Implementation: `ObjectDetectionService` + `ObjectDetectionServiceAdapter`

### Vectorization Service
- Orchestrates CLIP and YOLO for image processing
- Handles embedding generation from bytes or URL

## Database

- PostgreSQL with pgvector extension for vector similarity search
- Entity Framework Core for ORM
- Vector embeddings stored as `Pgvector.Vector` type

### Vector Search Pattern
```csharp
var results = await _dbContext.ProductImages
    .Where(pi => pi.Embedding != null)
    .Select(pi => new { Image = pi, Distance = pi.Embedding!.CosineDistance(queryVector) })
    .OrderBy(x => x.Distance)
    .Take(limit)
    .ToListAsync(ct);
```

## Testing Guidelines

- Test file naming: `{ClassName}Tests.cs`
- Test method naming: `MethodName_Condition_ExpectedResult`
- Use in-memory database for repository tests
- Mock interfaces for service tests

## Error Handling

- Use `Results.BadRequest()`, `Results.NotFound()`, `Results.Problem()` in endpoints
- Throw exceptions in services for exceptional cases
- Log errors with structured logging

## Naming Conventions

- **Repositories**: `I{Entity}Repository` / `{Entity}Repository`
- **Services**: `{Domain}Service`
- **DTOs**: `{Entity}Dto`
- **Requests**: `Create{Entity}Request`, `Update{Entity}Request`

## Migrations
- Use EF Core migrations for schema changes
- Naming convention: `YYYYMMDDHHMMSS_Description`
- DO NOT modify existing migrations after they are applied in production
- Use `dotnet ef migrations add` and `dotnet ef database update` commands
