# Visual Search

A visual similarity search application using CLIP embeddings and pgvector for fast image-based product discovery.

## Tech Stack

### Backend (.NET 9)
- **ASP.NET Core** - Minimal APIs with OpenAPI/Swagger
- **Entity Framework Core 9** - PostgreSQL with pgvector
- **JWT Bearer Authentication** - Admin panel security
- **Server-Sent Events (SSE)** - Real-time settings invalidation
- **CLIP Embeddings** - Image similarity via SixLabors.ImageSharp

### Frontend (Vue 3 + TypeScript)
- **Vue 3** - Composition API with `<script setup>`
- **Vite** - Fast development and optimized builds
- **Pinia** - Type-safe state management
- **Vue Router** - SPA navigation with auth guards
- **TanStack Query** - Server state management (via Kubb)
- **Dexie.js** - IndexedDB for client-side storage (history, favorites)

### Infrastructure
- **PostgreSQL 16** - With pgvector extension for similarity search
- **Nginx** - Reverse proxy with SSE support
- **Docker Compose** - Multi-service orchestration

## Features

- 🔍 **Visual Search** - Upload an image to find similar products
- ❤️ **Favorites** - Save products you like (stored locally)
- 📜 **Search History** - Review past searches with thumbnails
- 🛠️ **Admin Panel** - Configure application settings
- 🔐 **JWT Authentication** - Secure admin access with forced password change
- ⚡ **Real-time Updates** - SSE-based settings synchronization

## Quick Start

### Prerequisites
- Docker & Docker Compose
- Node.js 22+ (for local frontend development)
- .NET 9 SDK (for local backend development)

### Production (Docker)

```bash
# Clone the repository
git clone https://github.com/your-username/visual-search.git
cd visual-search

# Start all services
docker compose up -d --build

# Check status
docker compose ps

# View logs
docker compose logs -f
```

Access the application:
- **Frontend**: http://localhost
- **Swagger API**: http://localhost/swagger
- **Admin Panel**: http://localhost/admin

### Development

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

## Default Admin Credentials

| Username | Password | Note |
|----------|----------|------|
| `admin` | `admin123!` | Must change on first login |

## Environment Variables

### API (appsettings.json / environment)
| Variable | Description | Default |
|----------|-------------|---------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | See docker-compose |
| `Jwt__SecretKey` | JWT signing key (min 32 chars) | Auto-generated |
| `Jwt__Issuer` | JWT issuer | `VisualSearchApi` |
| `Jwt__Audience` | JWT audience | `VisualSearchApp` |

### Frontend (via Settings API)
| Key | Description | Default |
|-----|-------------|---------|
| `ui.siteName` | Site name in header | `Visual Search` |
| `search.maxImageSize` | Max image dimension | `800` |
| `search.jpegQuality` | JPEG compression quality | `85` |
| `search.maxResults` | Max search results | `20` |

## Project Structure

```
_amomar/
├── docker-compose.yml          # Service orchestration
├── deploy/
│   └── nginx/
│       ├── Dockerfile          # Multi-stage build (Node + Nginx)
│       └── nginx.conf          # Reverse proxy config
├── src/
│   ├── backend/
│   │   └── VisualSearch.Api/
│   │       ├── Data/           # EF Core entities & DbContext
│   │       ├── Endpoints/      # Minimal API endpoints
│   │       ├── Services/       # Business logic
│   │       └── Migrations/     # Database migrations
│   └── frontend/
│       ├── src/
│       │   ├── api/            # Generated API client
│       │   ├── components/     # Vue components
│       │   ├── db/             # Dexie IndexedDB
│       │   ├── stores/         # Pinia stores
│       │   ├── styles/         # SCSS design system
│       │   └── views/          # Route views
│       └── vite.config.ts
└── docs/
    └── INSTALL.md              # Detailed installation guide
```

## API Endpoints

### Public
| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/search/image` | Upload image for similarity search |
| `GET` | `/api/settings` | Get public settings |

### Admin (requires JWT)
| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/auth/login` | Authenticate admin |
| `POST` | `/api/auth/change-password` | Change admin password |
| `GET` | `/api/auth/events` | SSE settings stream |
| `GET` | `/api/settings/all` | Get all settings |
| `PUT` | `/api/settings/{key}` | Update setting |
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
