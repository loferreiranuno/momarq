# Amomar Project Split - Migration Guide

This guide explains how to migrate from the monorepo `_amomar` to three independent repositories.

## Repository Overview

| Repository | Contents | GitHub URL |
|------------|----------|------------|
| **Amomar.API** | Backend API + Worker + Tests | `github.com/loferreiranuno/Amomar.API` |
| **Amomar.WEB** | Vue.js Frontend | `github.com/loferreiranuno/Amomar.WEB` |
| **Amomar.Infra** | Infrastructure + Deployment | `github.com/loferreiranuno/Amomar.Infra` |

## Migration Steps

### Step 1: Create GitHub Repositories

Create three new empty repositories on GitHub:
- `Amomar.API`
- `Amomar.WEB`
- `Amomar.Infra`

### Step 2: Migrate Amomar.API

```powershell
# Create new local repo
mkdir C:\CodePersonal\Amomar.API
cd C:\CodePersonal\Amomar.API
git init

# Copy files from _split template
Copy-Item -Recurse C:\CodePersonal\_amomar\_split\Amomar.API\* .

# Copy backend source code
Copy-Item -Recurse C:\CodePersonal\_amomar\src\backend\VisualSearch.Api .\src\
Copy-Item -Recurse C:\CodePersonal\_amomar\src\backend\VisualSearch.Worker .\src\
Copy-Item -Recurse C:\CodePersonal\_amomar\src\backend\VisualSearch.Contracts .\src\
Copy-Item -Recurse C:\CodePersonal\_amomar\src\backend\.dockerignore .\src\

# Copy tests
mkdir tests
Copy-Item -Recurse C:\CodePersonal\_amomar\tests\VisualSearch.Api.Tests .\tests\

# Initial commit
git add .
git commit -m "Initial commit: Amomar.API from monorepo split"

# Push to GitHub
git remote add origin https://github.com/loferreiranuno/Amomar.API.git
git branch -M main
git push -u origin main
```

### Step 3: Migrate Amomar.WEB

```powershell
# Create new local repo
mkdir C:\CodePersonal\Amomar.WEB
cd C:\CodePersonal\Amomar.WEB
git init

# Copy files from _split template
Copy-Item -Recurse C:\CodePersonal\_amomar\_split\Amomar.WEB\* .

# Copy frontend source code
Copy-Item -Recurse C:\CodePersonal\_amomar\src\frontend\src .\
Copy-Item -Recurse C:\CodePersonal\_amomar\src\frontend\public .\
Copy-Item C:\CodePersonal\_amomar\src\frontend\package.json .
Copy-Item C:\CodePersonal\_amomar\src\frontend\package-lock.json .
Copy-Item C:\CodePersonal\_amomar\src\frontend\tsconfig.json .
Copy-Item C:\CodePersonal\_amomar\src\frontend\tsconfig.node.json .
Copy-Item C:\CodePersonal\_amomar\src\frontend\vite.config.ts .
Copy-Item C:\CodePersonal\_amomar\src\frontend\kubb.config.ts .
Copy-Item C:\CodePersonal\_amomar\src\frontend\postcss.config.js .
Copy-Item C:\CodePersonal\_amomar\src\frontend\index.html .
Copy-Item C:\CodePersonal\_amomar\src\frontend\env.d.ts .

# Sync swagger.json from running API (or use placeholder)
# curl -o swagger.json http://localhost:5000/swagger/v1/swagger.json

# Initial commit
git add .
git commit -m "Initial commit: Amomar.WEB from monorepo split"

# Push to GitHub
git remote add origin https://github.com/loferreiranuno/Amomar.WEB.git
git branch -M main
git push -u origin main
```

### Step 4: Migrate Amomar.Infra

```powershell
# Create new local repo
mkdir C:\CodePersonal\Amomar.Infra
cd C:\CodePersonal\Amomar.Infra
git init

# Copy files from _split template
Copy-Item -Recurse C:\CodePersonal\_amomar\_split\Amomar.Infra\* .

# Create backups folder with gitkeep
mkdir backups
New-Item -ItemType File -Path .\backups\.gitkeep

# Initial commit
git add .
git commit -m "Initial commit: Amomar.Infra from monorepo split"

# Push to GitHub
git remote add origin https://github.com/loferreiranuno/Amomar.Infra.git
git branch -M main
git push -u origin main
```

### Step 5: Configure Jenkins

1. **Add Jenkins credentials:**
   - `ghcr-credentials` - GitHub Container Registry (username + PAT)
   - `vps-ssh-key` - SSH private key for VPS access

2. **Create Multibranch Pipeline jobs:**
   - **Amomar-API** → Source: `https://github.com/loferreiranuno/Amomar.API`
   - **Amomar-WEB** → Source: `https://github.com/loferreiranuno/Amomar.WEB`
   - **Amomar-Infra** → Source: `https://github.com/loferreiranuno/Amomar.Infra`

3. **Configure GitHub webhooks** for each repo pointing to Jenkins.

### Step 6: Initial VPS Deployment

```bash
# SSH to VPS
ssh deploy@monmarq.es

# Clone Infra repo
cd /opt
git clone https://github.com/loferreiranuno/Amomar.Infra.git amomar
cd amomar

# Configure environment
cp .env.example .env
nano .env  # Set DB_PASSWORD and JWT_SECRET_KEY

# Initialize infrastructure
chmod +x scripts/*.sh
./scripts/init.sh

# Deploy all services
./scripts/deploy.sh --pull
```

## Post-Migration Checklist

- [ ] All three repos created and pushed to GitHub
- [ ] Jenkins jobs configured for each repo
- [ ] GitHub webhooks configured
- [ ] VPS initialized with Amomar.Infra
- [ ] All services running and healthy
- [ ] Frontend can communicate with API
- [ ] SSL certificates obtained via Let's Encrypt

## Development Workflow

### Backend Developer (Amomar.API)
```bash
cd Amomar.API
docker compose up           # Start postgres + api + worker
# Make changes
git push origin main        # Jenkins builds, tests, deploys
```

### Frontend Developer (Amomar.WEB)
```bash
# Start API first (in Amomar.API folder)
cd ../Amomar.API && docker compose up -d

# Then develop frontend
cd ../Amomar.WEB
npm install
npm run api:sync            # Update API types
npm run dev                 # Start dev server
git push origin main        # Jenkins builds, deploys
```

### DevOps (Amomar.Infra)
```bash
cd Amomar.Infra
# Update configs
git push origin main        # Manual Jenkins deploy
```

## Rollback Procedure

If a deployment fails:

1. **Single service rollback:**
   ```bash
   ssh deploy@monmarq.es
   cd /opt/amomar
   ./scripts/rollback.sh
   ```

2. **Or via Jenkins:**
   - Run `Amomar-Infra` pipeline
   - Select `rollback` action

## Troubleshooting

### API not reachable from Frontend
- Check `amomar-network` exists: `docker network ls`
- Verify API container is on network: `docker inspect visualsearch-api`

### SSL not working
- Check Traefik logs: `docker logs traefik`
- Verify domain DNS points to VPS IP
- Check `/letsencrypt/acme.json` permissions

### Database connection issues
- Verify postgres is healthy: `docker compose ps`
- Check `.env` has correct `DB_PASSWORD`
- Test connection: `docker exec -it visualsearch-postgres psql -U vsuser -d visualsearch`
