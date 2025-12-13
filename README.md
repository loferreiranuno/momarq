# Visual Search

A visual similarity search application using CLIP embeddings and pgvector for fast image-based product discovery.

**Live Site:** [https://monmarq.es](https://monmarq.es)

## Tech Stack

### Backend (.NET 9)
- **ASP.NET Core** - Minimal APIs with OpenAPI/Swagger
- **Entity Framework Core 9** - PostgreSQL with pgvector
- **JWT Bearer Authentication** - Admin panel security
- **Server-Sent Events (SSE)** - Real-time settings + jobs updates
- **CLIP Embeddings** - Image similarity via SixLabors.ImageSharp
- **Worker Service** - Background job processing for web crawling

### Frontend (Vue 3 + TypeScript)
- **Vue 3** - Composition API with `<script setup>`
- **Vite** - Fast development and optimized builds
- **Pinia** - Type-safe state management
- **Vue Router** - SPA navigation with auth guards
- **TanStack Query** - Server state management (via Kubb)
- **Dexie.js** - IndexedDB for client-side storage (history, favorites)

### Infrastructure
- **PostgreSQL 16** - With pgvector extension for similarity search
- **Traefik** - Reverse proxy with automatic SSL (Let's Encrypt)
- **Nginx** - Static file serving for frontend
- **Docker Compose** - Multi-service orchestration
- **GitHub Actions** - CI/CD pipeline with GHCR

## Architecture

This project follows **Clean Architecture** principles. See [`.github/copilot.instructions.md`](.github/copilot.instructions.md) for detailed architecture guidelines.

```
Endpoints → Application Services → Repositories → DbContext
              ↓
        Infrastructure Services (CLIP, YOLO)
```

**Key Rules:**
- Endpoints MUST NOT access DbContext directly
- Application Services return DTOs only, never entities
- Repositories are the ONLY layer that accesses DbContext

## Features

- 🔍 **Visual Search** - Upload an image to find similar products
- ❤️ **Favorites** - Save products you like (stored locally)
- 📜 **Search History** - Review past searches with thumbnails
- 🕷️ **Web Crawling** - Background worker for product data extraction
- 🛠️ **Admin Panel** - Configure application settings
- 🔐 **JWT Authentication** - Secure admin access with forced password change
- ⚡ **Real-time Updates** - SSE-based settings + jobs synchronization

## SSE (Server-Sent Events)

The frontend uses SSE for always-on updates.

- Settings SSE: `GET /api/settings/sse` (public)
- Jobs SSE: `GET /api/jobs/sse?ticket=...` (requires one-time ticket)
- SSE ticket minting: `POST /api/auth/sse-ticket` (Admin JWT via `Authorization` header)

Why tickets? `EventSource` cannot send `Authorization` headers, so the UI mints a short-lived, single-use ticket over an authenticated call and then uses it in the SSE URL.

## Crawl Job Worker

The `VisualSearch.Worker` project is a .NET Worker Service that processes crawl jobs in the background.

### Architecture

```
                         ┌─────────────────────┐
                         │    API (Job CRUD)   │
                         └──────────┬──────────┘
                                    │ Creates jobs with Status=Queued
                                    ▼
┌─────────────────────────────────────────────────────────────────┐
│                        PostgreSQL                                │
│  ┌─────────────┐  ┌─────────────┐  ┌──────────────────────────┐ │
│  │  crawl_jobs │  │ crawl_pages │  │ crawl_extracted_products │ │
│  │ lease_owner │  │   status    │  │       raw_json           │ │
│  │lease_expires│  │   content   │  │       image_urls         │ │
│  └─────────────┘  └─────────────┘  └──────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                                    ▲
                                    │ Claims jobs atomically (lease pattern)
                                    │
         ┌──────────────────────────┴───────────────────────────┐
         │                  Worker Service                       │
         │  ┌────────────────────────────────────────────────┐  │
         │  │         CrawlJobWorkerService (Background)     │  │
         │  │  - Polls for queued/expired jobs               │  │
         │  │  - Claims via atomic SQL CTE                   │  │
         │  │  - Renews lease during processing              │  │
         │  └────────────────────────────────────────────────┘  │
         │                          │                            │
         │                          ▼                            │
         │  ┌────────────────────────────────────────────────┐  │
         │  │           CrawlerStrategyFactory               │  │
         │  │  - Resolves strategy by provider.crawler_type  │  │
         │  └────────────────────────────────────────────────┘  │
         │           │                        │                  │
         │           ▼                        ▼                  │
         │  ┌─────────────────┐   ┌─────────────────────────┐   │
         │  │GenericCrawler   │   │ Custom strategies       │   │
         │  │- Sitemap parsing│   │ (ZaraHome, IKEA, etc.)  │   │
         │  │- HTML crawling  │   │                         │   │
         │  └─────────────────┘   └─────────────────────────┘   │
         │                          │                            │
         │                          ▼                            │
         │  ┌────────────────────────────────────────────────┐  │
         │  │         DefaultProductExtractor                │  │
         │  │  - JSON-LD structured data                     │  │
         │  │  - OpenGraph meta tags                         │  │
         │  │  - HTML microdata                              │  │
         │  │  - Custom CSS selectors                        │  │
         │  └────────────────────────────────────────────────┘  │
         └───────────────────────────────────────────────────────┘
```

### Strategy Pattern

Each provider can have a custom crawling strategy configured via:

| Column | Description |
|--------|-------------|
| `providers.crawler_type` | Strategy identifier (e.g., "generic", "sitemap", "api") |
| `providers.crawler_config_json` | JSON configuration for the strategy |

**Configuration example (JSON):**
```json
{
  "crawlerType": "generic",
  "userAgent": "VisualSearchBot/1.0",
  "requestDelayMs": 1000,
  "maxConcurrentRequests": 2,
  "includePatterns": ["/products/", "/catalog/"],
  "excludePatterns": ["/cart", "/checkout", "/login"],
  "productContainerSelector": ".product-card",
  "productNameSelector": ".product-title",
  "productPriceSelector": ".product-price",
  "productImageSelector": ".product-image img",
  "paginationSelector": ".pagination a"
}
```

### Lease-Based Job Claiming

The worker uses atomic lease-based job claiming for distributed safety:

1. **Claim**: Worker atomically claims a job by setting `lease_owner` and `lease_expires_at`
2. **Process**: Worker processes pages, renewing the lease every 2 minutes
3. **Complete**: On success/failure, worker clears the lease and sets final status

This allows multiple worker instances to run safely without duplicate processing.

### Adding Custom Strategies

1. Create a new class implementing `ICrawlerStrategy`:
```csharp
public sealed class ZaraHomeCrawlerStrategy : ICrawlerStrategy
{
    public string CrawlerType => "zarahome";
    
    public Task<IReadOnlyList<string>> DiscoverUrlsAsync(...) { ... }
    public Task<CrawlPageResult> CrawlPageAsync(...) { ... }
}
```

2. Register in `Program.cs`:
```csharp
builder.Services.AddSingleton<ICrawlerStrategy, ZaraHomeCrawlerStrategy>();
```

3. Configure the provider:
```sql
UPDATE providers 
SET crawler_type = 'zarahome', 
    crawler_config_json = '{"apiKey": "...", ...}'
WHERE name = 'Zara Home';
```

### Running the Worker

**Standalone:**
```bash
cd src/backend/VisualSearch.Worker
dotnet run
```

**Docker Compose (development):**
```bash
docker compose up worker
```

**Production:**
The worker is included in `docker-compose.prod.yml` and starts automatically.

**Scaling:**
```yaml
# docker-compose.prod.yml
worker:
  deploy:
    replicas: 3  # Run 3 worker instances
```

## Quick Start

### Prerequisites
- Docker & Docker Compose v2+
- Node.js 22+ (for local frontend development)
- .NET 9 SDK (for local backend development)

### Local Development (Docker)

```bash
# Clone the repository
git clone https://github.com/loferreiranuno/momarq.git
cd momarq

# Start all services (development mode)
docker compose up -d --build

# Check status
docker compose ps

# View logs
docker compose logs -f
```

Access the application:
- **Frontend**: http://localhost:3000
- **API**: http://localhost:5000
- **Swagger API**: http://localhost:5000/swagger
- **Admin Panel**: http://localhost:3000/admin

### Development (Without Docker)

#### Backend
```bash
cd src/backend/VisualSearch.Api

# Restore dependencies
dotnet restore

# Run with hot reload
dotnet watch run
```

#### Frontend
```bash
cd src/frontend

# Install dependencies
npm install

# Generate API client from OpenAPI spec (optional)
npm run generate-api

# Start dev server
npm run dev
```

## CI/CD Pipeline

This project uses GitHub Actions for continuous integration and deployment.

### Workflows

| Workflow | Trigger | Description |
|----------|---------|-------------|
| `ci.yml` | PR & push to `master` | Build & test backend/frontend |
| `deploy.yml` | Push to `master` | Build Docker images, push to GHCR, deploy to VPS |

### GitHub Secrets Required

Configure these secrets in your GitHub repository settings (`Settings > Secrets and variables > Actions`):

| Secret | Description | Example |
|--------|-------------|---------|
| `VPS_HOST` | Contabo VPS IP or hostname | `123.45.67.89` |
| `VPS_USER` | SSH username | `root` or `deploy` |
| `VPS_SSH_KEY` | Private SSH key (ed25519 or RSA) | `-----BEGIN OPENSSH...` |
| `DB_PASSWORD` | PostgreSQL password | Strong random password |
| `JWT_SECRET_KEY` | JWT signing key (min 32 chars) | Strong random string |

### Docker Images

Images are published to GitHub Container Registry:
- `ghcr.io/loferreiranuno/momarq/api:latest`
- `ghcr.io/loferreiranuno/momarq/frontend:latest`
- `ghcr.io/loferreiranuno/momarq/worker:latest`

## Production Deployment

### VPS Prerequisites (Contabo)

1. **Install Docker & Docker Compose:**
```bash
# Update system
apt update && apt upgrade -y

# Install Docker
curl -fsSL https://get.docker.com | sh

# Add your user to docker group (optional, for non-root)
usermod -aG docker $USER

# Verify installation
docker --version
docker compose version
```

2. **Configure Firewall:**
```bash
# Allow SSH, HTTP, HTTPS
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw enable
```

3. **Setup SSH Key Authentication:**
```bash
# On your local machine, generate key if needed
ssh-keygen -t ed25519 -C "deploy@monmarq.es"

# Copy public key to VPS
ssh-copy-id -i ~/.ssh/id_ed25519.pub root@YOUR_VPS_IP
```

### DNS Configuration

Create an **A record** pointing your domain to the VPS IP:

| Type | Name | Value | TTL |
|------|------|-------|-----|
| A | `@` | `YOUR_VPS_IP` | 300 |
| A | `www` | `YOUR_VPS_IP` | 300 |

### First-Time Deployment

1. **SSH into your VPS:**
```bash
ssh root@YOUR_VPS_IP
```

2. **Create deployment directory:**
```bash
mkdir -p /opt/monmarq
cd /opt/monmarq
```

3. **Clone the repository (or let GitHub Actions do it):**
```bash
git clone https://github.com/loferreiranuno/momarq.git .
```

4. **Create environment file:**
```bash
cat > .env << 'EOF'
DB_PASSWORD=your_secure_database_password
JWT_SECRET_KEY=your_32_char_minimum_jwt_secret_key
EOF
chmod 600 .env
```

5. **Create Docker network:**
```bash
docker network create traefik-public
```

6. **Start the application:**
```bash
docker compose -f docker-compose.prod.yml up -d
```

7. **Verify SSL certificate:**
```bash
# Wait 1-2 minutes for Let's Encrypt
curl -I https://monmarq.es
```

### Maintenance Commands

```bash
# View all container status
docker compose -f docker-compose.prod.yml ps

# View logs (all services)
docker compose -f docker-compose.prod.yml logs -f

# View logs (specific service)
docker compose -f docker-compose.prod.yml logs -f api

# Restart a service
docker compose -f docker-compose.prod.yml restart api

# Pull latest images and redeploy
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d

# Stop all services
docker compose -f docker-compose.prod.yml down

# Clean up unused images
docker image prune -f
```

## Default Admin Credentials

| Username | Password | Note |
|----------|----------|------|
| `admin` | `admin123!` | Must change on first login |

## Environment Variables

### API (appsettings.json / environment)
| Variable | Description | Default |
|----------|-------------|---------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | See docker-compose |
| `Jwt__Key` | JWT signing key (min 32 chars) | `VisualSearch-Default-JWT-Key-Change-In-Production-2024!` |
| `Jwt__Issuer` | JWT issuer | `VisualSearch.Api` |
| `Jwt__Audience` | JWT audience | `VisualSearch.Frontend` |

### Frontend (via Settings API)
| Key | Description | Default |
|-----|-------------|---------|
| `ui.siteName` | Site name in header | `Visual Search` |
| `search.maxImageSize` | Max image dimension | `800` |
| `search.jpegQuality` | JPEG compression quality | `85` |
| `search.maxResults` | Max search results | `20` |

## Testing

### Running Tests

The project includes integration tests using **xUnit**, **Testcontainers** (PostgreSQL with pgvector), and **FluentAssertions**.

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test file
dotnet test --filter "FullyQualifiedName~AuthFlowTests"
```

### Test Structure

```
tests/
└── VisualSearch.Api.Tests/
    ├── Fixtures/
    │   ├── PostgresContainerFixture.cs  # Testcontainers PostgreSQL
    │   └── WebApplicationFixture.cs     # WebApplicationFactory
    └── Integration/
        ├── IntegrationTestBase.cs       # Base class with auth helpers
        ├── AuthFlowTests.cs             # Login, change password tests
        └── AdminCrudTests.cs            # Provider, product, category CRUD
```

## Architecture Compliance

### Current Status

| Component | Status | Notes |
|-----------|--------|-------|
| `AuthEndpoints` | ✅ Compliant | Uses `AuthService` |
| `CategoriesEndpoints` | ✅ Compliant | Uses `CategoryService` |
| `ImageSearchEndpoints` | ✅ Compliant | Uses `VisualSearchService` |
| `SettingsEndpoints` | ✅ Compliant | Uses `SettingsService` |
| `AdminEndpoints` | ✅ Compliant | Uses Application Services (`ProviderService`, `ProductService`, etc.) |

## Project Structure

```
momarq/
├── docker-compose.yml          # Development orchestration
├── docker-compose.prod.yml     # Production with Traefik
├── .github/
│   ├── copilot.instructions.md # Architecture guidelines
│   └── workflows/
│       ├── ci.yml              # Build & test workflow
│       └── deploy.yml          # Deploy to production
├── deploy/
│   ├── nginx/
│   │   ├── Dockerfile          # Multi-stage build (Node + Nginx)
│   │   └── nginx.conf          # Static file serving
│   └── traefik/
│       ├── traefik.yml         # Traefik static config
│       └── dynamic.yml         # Middleware definitions
├── src/
│   ├── backend/
│   │   ├── VisualSearch.Api/
│   │   │   ├── Application/    # Business logic services
│   │   │   ├── Contracts/      # DTOs and request models
│   │   │   ├── Data/           # EF Core DbContext & entities
│   │   │   ├── Domain/         # Interfaces
│   │   │   ├── Endpoints/      # Minimal API endpoints
│   │   │   ├── Infrastructure/ # Repository implementations
│   │   │   ├── Services/       # Infrastructure services
│   │   │   └── Migrations/     # Database migrations
│   │   ├── VisualSearch.Worker/
│   │   │   ├── Crawling/       # Crawler strategy interfaces
│   │   │   │   └── Strategies/ # Strategy implementations
│   │   │   ├── Data/           # Worker DbContext
│   │   │   └── Services/       # CrawlJobWorkerService
│   │   └── VisualSearch.Contracts/
│   │       └── Crawling/       # Shared DTOs and enums
│   └── frontend/
│       └── src/
│           ├── api/            # Generated API client
│           ├── components/     # Vue components
│           ├── db/             # Dexie IndexedDB
│           ├── stores/         # Pinia stores
│           ├── styles/         # SCSS design system
│           └── views/          # Route views
└── docs/
    └── INSTALL.md              # Detailed installation guide
```

## API Endpoints

### Public
| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/search/image` | Upload image for similarity search |
| `GET` | `/api/settings/public` | Get public settings |

### Admin (requires JWT)
| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/auth/login` | Authenticate admin |
| `POST` | `/api/auth/change-password` | Change admin password |
| `GET` | `/api/auth/me` | Get current user |
| `GET` | `/api/settings` | Get all settings |
| `PUT` | `/api/settings/{key}` | Update setting |
| `GET` | `/api/settings/sse` | SSE settings stream |
| `GET` | `/api/admin/stats` | Get dashboard stats |
| `GET` | `/api/admin/providers` | List providers |

## Design System

The frontend uses a Zara Home-inspired design with:
- **Background**: `#F5F5F0` (warm off-white)
- **Text**: `#2C2C2C` (soft black)
- **Accent**: `#8B7355` (warm brown)
- **Typography**: Playfair Display (headings), Inter (body)

## License

MIT License - See [LICENSE](LICENSE) for details.
