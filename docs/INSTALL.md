# Visual Search POC - Guia de Instalação

## Requisitos

- **Servidor**: Contabo Cloud VPS 20 (ou equivalente) - Ubuntu 24.04 LTS
- **RAM**: Mínimo 4GB (recomendado 8GB)
- **Disco**: Mínimo 20GB
- **CPU**: 4+ vCPUs (sem GPU necessária)

## Instalação Rápida (Docker)

### 1. Instalar Docker e Docker Compose

```bash
# Atualizar sistema
sudo apt update && sudo apt upgrade -y

# Instalar dependências
sudo apt install -y apt-transport-https ca-certificates curl software-properties-common

# Adicionar chave GPG do Docker
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /usr/share/keyrings/docker-archive-keyring.gpg

# Adicionar repositório Docker
echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/docker-archive-keyring.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

# Instalar Docker
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Adicionar usuário ao grupo docker
sudo usermod -aG docker $USER
newgrp docker

# Verificar instalação
docker --version
docker compose version
```

### 2. Clonar Repositório e Iniciar

```bash
# Clonar repositório
cd /opt
sudo git clone https://github.com/seu-usuario/visual-search.git
sudo chown -R $USER:$USER visual-search
cd visual-search

# Iniciar todos os serviços
docker compose up -d --build

# Verificar status
docker compose ps

# Ver logs
docker compose logs -f
```

### 3. Verificar Instalação

```bash
# Health check da API
curl http://localhost/health

# Swagger UI
# Abrir no browser: http://SEU_IP/swagger

# Testar API de providers
curl http://localhost/api/admin/providers
```

## Instalação Manual (Sem Docker)

### 1. Instalar PostgreSQL 16 + pgvector

```bash
# Adicionar repositório PostgreSQL
sudo sh -c 'echo "deb https://apt.postgresql.org/pub/repos/apt $(lsb_release -cs)-pgdg main" > /etc/apt/sources.list.d/pgdg.list'
wget --quiet -O - https://www.postgresql.org/media/keys/ACCC4CF8.asc | sudo apt-key add -

# Instalar PostgreSQL 16
sudo apt update
sudo apt install -y postgresql-16 postgresql-contrib-16

# Instalar pgvector
sudo apt install -y postgresql-16-pgvector

# Iniciar PostgreSQL
sudo systemctl start postgresql
sudo systemctl enable postgresql

# Criar database e usuário
sudo -u postgres psql << EOF
CREATE USER vsuser WITH PASSWORD 'vspassword123';
CREATE DATABASE visualsearch OWNER vsuser;
GRANT ALL PRIVILEGES ON DATABASE visualsearch TO vsuser;
\c visualsearch
CREATE EXTENSION IF NOT EXISTS vector;
EOF

# Verificar pgvector
sudo -u postgres psql -d visualsearch -c "SELECT * FROM pg_extension WHERE extname = 'vector';"
```

### 2. Instalar .NET 8 SDK

```bash
# Adicionar repositório Microsoft
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Instalar .NET 8 SDK
sudo apt update
sudo apt install -y dotnet-sdk-8.0

# Verificar
dotnet --version
```

### 3. Compilar e Executar API

```bash
# Navegar para diretório da API
cd /opt/visual-search/src/backend/VisualSearch.Api

# Restaurar dependências
dotnet restore

# Compilar
dotnet build -c Release

# Executar migrações (o SeedDataService faz isto automaticamente no startup)
# dotnet ef database update

# Publicar
dotnet publish -c Release -o /opt/visual-search/publish

# Testar execução
cd /opt/visual-search/publish
dotnet VisualSearch.Api.dll
```

### 4. Configurar Systemd Service

```bash
# Criar arquivo de serviço
sudo tee /etc/systemd/system/visual-search.service << EOF
[Unit]
Description=Visual Search API
After=network.target postgresql.service

[Service]
Type=notify
User=www-data
WorkingDirectory=/opt/visual-search/publish
ExecStart=/usr/bin/dotnet /opt/visual-search/publish/VisualSearch.Api.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=visual-search
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Environment=ConnectionStrings__DefaultConnection=Host=localhost;Database=visualsearch;Username=vsuser;Password=vspassword123

[Install]
WantedBy=multi-user.target
EOF

# Ajustar permissões
sudo chown -R www-data:www-data /opt/visual-search/publish

# Recarregar systemd e iniciar
sudo systemctl daemon-reload
sudo systemctl enable visual-search
sudo systemctl start visual-search

# Verificar status
sudo systemctl status visual-search
sudo journalctl -u visual-search -f
```

### 5. Instalar e Configurar Nginx

```bash
# Instalar Nginx
sudo apt install -y nginx

# Copiar frontend
sudo mkdir -p /var/www/visual-search
sudo cp -r /opt/visual-search/src/frontend/* /var/www/visual-search/
sudo chown -R www-data:www-data /var/www/visual-search

# Criar configuração Nginx
sudo tee /etc/nginx/sites-available/visual-search << 'EOF'
upstream api {
    server 127.0.0.1:8080;
    keepalive 32;
}

server {
    listen 80;
    server_name _;

    root /var/www/visual-search;
    index index.html;

    # Gzip
    gzip on;
    gzip_types text/plain text/css application/json application/javascript application/octet-stream application/wasm;

    # API proxy
    location /api/ {
        proxy_pass http://api;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Connection "";
        proxy_connect_timeout 10s;
        proxy_send_timeout 30s;
        proxy_read_timeout 30s;
    }

    # Swagger
    location /swagger {
        proxy_pass http://api;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
    }

    # Service Worker
    location /sw.js {
        add_header Cache-Control "no-cache";
        add_header Service-Worker-Allowed "/";
    }

    # COOP/COEP headers for SharedArrayBuffer
    location / {
        try_files $uri $uri/ /index.html;
        add_header Cross-Origin-Embedder-Policy "require-corp";
        add_header Cross-Origin-Opener-Policy "same-origin";
    }
}
EOF

# Ativar site
sudo ln -sf /etc/nginx/sites-available/visual-search /etc/nginx/sites-enabled/
sudo rm -f /etc/nginx/sites-enabled/default

# Testar e reiniciar
sudo nginx -t
sudo systemctl restart nginx
sudo systemctl enable nginx
```

### 6. Configurar Firewall

```bash
# Instalar UFW se não existir
sudo apt install -y ufw

# Configurar regras
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow ssh
sudo ufw allow http
sudo ufw allow https

# Ativar firewall
sudo ufw enable

# Verificar
sudo ufw status
```

## Configuração de Produção

### SSL com Certbot (Let's Encrypt)

```bash
# Instalar Certbot
sudo apt install -y certbot python3-certbot-nginx

# Obter certificado (substituir pelo seu domínio)
sudo certbot --nginx -d seu-dominio.com

# Auto-renovação (já configurado automaticamente)
sudo certbot renew --dry-run
```

### Otimizações PostgreSQL

```bash
# Editar configuração
sudo nano /etc/postgresql/16/main/postgresql.conf
```

Adicionar/modificar:

```ini
# Memória (ajustar baseado no VPS)
shared_buffers = 1GB
effective_cache_size = 3GB
work_mem = 64MB
maintenance_work_mem = 256MB

# pgvector específico
hnsw.ef_search = 100

# Conexões
max_connections = 100
```

```bash
# Reiniciar PostgreSQL
sudo systemctl restart postgresql
```

## Uso

### Adicionar Produtos via API

```bash
# Criar provider
curl -X POST http://localhost/api/admin/providers \
  -H "Content-Type: application/json" \
  -d '{"name": "Zara Home", "websiteUrl": "https://zarahome.com"}'

# Criar produto
curl -X POST http://localhost/api/admin/products \
  -H "Content-Type: application/json" \
  -d '{
    "providerId": 1,
    "name": "Sofá Moderno Cinza",
    "price": 599.99,
    "category": "sofa",
    "productUrl": "https://zarahome.com/sofa-1"
  }'

# Adicionar imagem ao produto (embedding gerado server-side se modelo ONNX disponível)
curl -X POST http://localhost/api/admin/products/1/images \
  -H "Content-Type: application/json" \
  -d '{
    "imageUrl": "https://example.com/sofa.jpg",
    "isPrimary": true
  }'

# Adicionar imagem com embedding pré-computado
curl -X POST http://localhost/api/admin/products/1/images/embedding \
  -H "Content-Type: application/json" \
  -d '{
    "imageUrl": "https://example.com/sofa.jpg",
    "isPrimary": true,
    "embedding": [0.1, 0.2, ...] // 512 floats
  }'
```

### Listar Produtos

```bash
curl "http://localhost/api/admin/products?page=1&pageSize=20"
```

## Troubleshooting

### API não inicia

```bash
# Ver logs
docker compose logs api
# ou
sudo journalctl -u visual-search -f

# Verificar conexão PostgreSQL
docker compose exec api dotnet ef database update
```

### Erro de conexão PostgreSQL

```bash
# Verificar se PostgreSQL está rodando
docker compose ps postgres
# ou
sudo systemctl status postgresql

# Testar conexão
psql -h localhost -U vsuser -d visualsearch -c "SELECT 1;"
```

### CORS issues no browser

```bash
# Verificar headers nginx
curl -I http://localhost/api/health
```

### Modelos não carregam no browser

- Verificar console do browser (F12)
- Confirmar headers COOP/COEP no nginx
- Limpar cache do browser
- Verificar se Service Worker está registado

## Performance Esperada

| Métrica | Target | Notas |
|---------|--------|-------|
| Tempo de carregamento inicial | ~10-20s | Primeiro acesso (download modelos ~150MB) |
| Tempo de carregamento repeat | <1s | Modelos em cache (Service Worker) |
| Detecção YOLO | ~150ms | Client-side, depende do CPU |
| Embedding CLIP | ~200ms | Por objeto detectado, client-side |
| Busca pgvector | <15ms | Server-side, HNSW index |
| **Latência total** | **<900ms** | Após modelos carregados |

## Arquitetura

```
┌─────────────────────────────────────────────────────────┐
│                      Browser                             │
│  ┌─────────────────────────────────────────────────┐    │
│  │  YOLO-World (WASM) → CLIP (WASM) → Binary Send  │    │
│  └─────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼ POST /api/busca (binary)
┌─────────────────────────────────────────────────────────┐
│                    Nginx (Port 80)                       │
│  Static Files + Reverse Proxy + COOP/COEP Headers       │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│              .NET 8 Minimal API (Port 8080)              │
│  Binary Protocol Parser → pgvector Query → Binary Resp  │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│         PostgreSQL 16 + pgvector (Port 5432)            │
│  HNSW Index (m=16, ef_construction=200) → Cosine Dist   │
└─────────────────────────────────────────────────────────┘
```
