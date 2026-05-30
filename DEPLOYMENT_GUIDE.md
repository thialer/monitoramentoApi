# MonitoramentoAPI - Guia de Deployment e Configuração

## Arquitetura Integrada

Este projeto agora executa **API ASP.NET Core + BackgroundService (MonitoringWorker)** no mesmo host. Não há mais projetos separados.

### Estrutura Consolidada
```
MonitoramentoAPI/
├── Controllers/           # API endpoints
├── Services/
│   ├── MonitoringWorker.cs         # BackgroundService para monitoramento
│   ├── WorkerHealthService.cs      # Health check service
│   └── TokenService.cs             # JWT token service
├── Data/ (Monitoramento.Shared)    # DbContext e Models
├── Program.cs                      # Startup configuration
├── appsettings.json               # Configurações padrão
└── appsettings.Development.json   # Configurações de desenvolvimento
```

## Variáveis de Ambiente Necessárias

### Conexão com Banco de Dados
```
ConnectionStrings__DefaultConnection=server=HOST;port=PORT;database=DB;user=USER;password=PASSWORD;SslMode=Required;
```

### JWT Configuration
```
Jwt__Key=SUA_CHAVE_JWT_SECRETA_AQUI
Jwt__Issuer=ApiMonitoramento-api
Jwt__Audience=ApiMonitoramento-api
```

### Email Settings (Gmail recomendado)
```
EmailSettings__SmtpServer=smtp.gmail.com
EmailSettings__Port=587
EmailSettings__SenderEmail=seu_email@gmail.com
EmailSettings__SenderPassword=sua_senha_aplicacao_google
```

### Stripe (Opcional)
```
Stripe__SecretKey=seu_stripe_secret_key
Stripe__PublishableKey=seu_stripe_publishable_key
Stripe__WebhookSecret=seu_stripe_webhook_secret
```

### PORT (Para Render/Railway)
```
PORT=8080
```

## Configuração do Worker

Arquivo: `appsettings.json`

```json
"MonitoringWorker": {
	"CheckCycleIntervalSeconds": 30,    // Intervalo entre ciclos de verificação
	"HttpTimeoutSeconds": 10,           // Timeout para requisições HTTP
	"EnabledOnStartup": true            // Ativar ao iniciar
}
```

### Otimizações para Free Tier

- **CheckCycleIntervalSeconds**: 30s (padrão)
  - Aumentar para 60s para economizar recursos
  - Mínimo: 10s (não recomendado)

- **HttpTimeoutSeconds**: 10s (padrão)
  - Reduzir para 5s em redes lentas
  - Máximo: 30s

## Endpoints Disponíveis

### Health Checks
```
GET /health              # Basic health check (sempre retorna 200)
GET /health/worker       # Worker detailed status (métricas do background service)
GET /                    # Root endpoint (redireciona para Swagger em dev)
```

### Swagger
```
GET /swagger             # API documentation (habilitado em todas as configurações)
```

### Worker Health Response
```json
{
  "status": "ok",
  "worker": {
	"isRunning": true,
	"uptime": {
	  "days": 0,
	  "hours": 1,
	  "minutes": 30,
	  "seconds": 45,
	  "totalSeconds": 5445
	},
	"lastCycleTime": "2024-01-15T10:30:00Z",
	"cyclesCompleted": 123,
	"errorsEncountered": 2,
	"monitorsProcessedLastCycle": 5,
	"monitorsSkippedLastCycle": 2,
	"lastError": null
  }
}
```

## Deploy em Render

### 1. Criar novo Web Service em Render
- Github repository: `https://github.com/thialer/monitoramentoApi`
- Branch: `main`
- Build Command: `dotnet publish -c Release -o out`
- Start Command: `dotnet ./out/MonitoramentoAPI.dll`

### 2. Environment Variables no Render
Adicionar todas as variáveis listadas acima em "Environment" no painel do Render.

### 3. PostgreSQL/MySQL
- Render fornece banco de dados
- Copiar connection string para `ConnectionStrings__DefaultConnection`

## Deploy em Railway

### 1. Conectar Github
- Railway conecta automaticamente ao seu repo

### 2. Criar variáveis de ambiente
- Mesmas variáveis do Render

### 3. Build Detection
- Railway detecta automaticamente .NET 8

## Monitoramento em Produção

### Logs
- Verificar em Render Dashboard ou Railway Logs
- Worker loga cada ciclo com métricas
- Logs incluem detalhes de erros de HTTP, timeouts, problemas de email

### Health Checks
```bash
# Basic check
curl https://sua-api.render.com/health

# Detailed worker check
curl https://sua-api.render.com/health/worker
```

### Configurar Health Check no Render
- Health Check Path: `/health`
- Interval: 30s
- Timeout: 10s

## Features do Worker

✅ Monitoramento contínuo de APIs (HTTP GET/POST/etc)
✅ Verificação de Status Code (200-299 = UP)
✅ Timeout configurável
✅ Logs apenas quando status muda (otimização)
✅ Alertas por email com retry (até 3 tentativas)
✅ Graceful shutdown (30s timeout)
✅ Detailed logging para diagnóstico
✅ Health check endpoint com métricas

## Possíveis Problemas

### Worker não está enviando emails
1. Verificar `EmailSettings` em appsettings
2. Verificar logs: `/health/worker` mostra `lastError`
3. Gmail: ativar "Less secure app access" ou usar "App Passwords"
4. Validar configuração SMTP

### Worker não está processando monitores
1. Verificar se há monitores com `Ativo = true` no BD
2. Verificar em `/health/worker` se `cyclesCompleted > 0`
3. Verificar logs para erros de DB

### Timeout de requisições HTTP
1. Aumentar `HttpTimeoutSeconds` se APIs são lentas
2. Ou reduzir se há muitos timeouts (problema de conectividade)

### Alto uso de memória
1. Reduzir `CheckCycleIntervalSeconds` para menos ciclos
2. Monitorar tamanho do log table no BD
3. Implementar retention policy para logs antigos

## Migração de Projeto Worker Separado

Se você tinha um projeto Worker separado:

1. ✅ Code integrado em `MonitoramentoAPI/Services/MonitoringWorker.cs`
2. ✅ Registrado como HostedService em `Program.cs`
3. ✅ Dependency Injection funcionando
4. ✅ Banco de dados compartilhado com API

**Você pode remover o projeto `MonitoramentoWorker` do seu repositório.**

Para remover:
```bash
git rm -r MonitoramentoWorker/
git commit -m "Remove separate Worker project - integrated into API"
git push
```

## Tecnologias

- **.NET 8** (LTS)
- **ASP.NET Core** 8
- **Entity Framework Core** 8
- **MySQL** (via Aiven Cloud ou Render)
- **Stripe** (opcional)

## Suporte e Documentação

- [ASP.NET Core Hosted Services](https://learn.microsoft.com/en-us/dotnet/core/extensions/hosted-services)
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- [Render Deployment](https://render.com/docs)
- [Railway Deployment](https://docs.railway.app/)
