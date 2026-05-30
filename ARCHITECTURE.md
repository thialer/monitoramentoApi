# Arquitetura MonitoramentoAPI - Integração Worker

## Resumo da Mudança

**Antes:**
```
MonitoramentoAPI (API apenas)
MonitoramentoWorker (Projeto separado - BackgroundService)
Monitoramento.Shared (DbContext, Models)
```

**Depois (Atual):**
```
MonitoramentoAPI (API + BackgroundService integrado)
├── Controllers/
├── Services/
│   ├── MonitoringWorker.cs (BackgroundService)
│   └── WorkerHealthService.cs (Health monitoring)
└── [Shared code]

Monitoramento.Shared (DbContext, Models)
```

## Componentes Integrados

### 1. **MonitoringWorker** (BackgroundService)
**Arquivo:** `MonitoramentoAPI/Services/MonitoringWorker.cs`

**Responsabilidades:**
- Verificar continuamente APIs (HTTP GET/POST/PUT/DELETE)
- Medir response time e status code
- Detectar mudanças de status (UP → DOWN, DOWN → UP)
- Gerar logs apenas quando status muda
- Enviar alertas por email com retry automático
- Respeitar CancellationToken para shutdown gracioso

**Configuração:**
```json
"MonitoringWorker": {
	"CheckCycleIntervalSeconds": 30,  // Intervalo entre ciclos
	"HttpTimeoutSeconds": 10          // Timeout HTTP
}
```

**Ciclo de Execução:**
1. Lê todos os monitores ativos do BD
2. Para cada monitor, verifica se é hora de executar (baseado em `Intervalo`)
3. Faz requisição HTTP com timeout configurável
4. Registra status (UP/DOWN) em Log
5. Se status mudou, envia email para alertas configurados
6. Aguarda intervalo configurado antes de próximo ciclo

### 2. **WorkerHealthService** (Diagnostics)
**Arquivo:** `MonitoramentoAPI/Services/WorkerHealthService.cs`

**Responsabilidades:**
- Rastrear status do Worker (uptime, ciclos completados, erros)
- Fornecer métricas para health check
- Thread-safe com lock para acesso simultâneo

**Métricas Fornecidas:**
```
- isRunning: boolean
- uptime: {days, hours, minutes, seconds, totalSeconds}
- lastCycleTime: DateTime
- cyclesCompleted: int
- errorsEncountered: int
- monitorsProcessedLastCycle: int
- monitorsSkippedLastCycle: int
- lastError: string
```

### 3. **Program.cs** (Configuração)
**Mudanças Principais:**

```csharp
// 1. Registrar serviços
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IWorkerHealthService, WorkerHealthService>();
builder.Services.AddHostedService<MonitoringWorker>();

// 2. Endpoints health check
app.MapGet("/health", ...)
app.MapGet("/health/worker", ...)

// 3. Graceful shutdown
builder.WebHost.UseShutdownTimeout(TimeSpan.FromSeconds(30));
```

## Fluxo de Dados

```
┌─────────────────────────────────────────┐
│   Application Start (Program.cs)        │
│   - Build DI Container                  │
│   - Register Services                   │
│   - Migrate Database                    │
│   - Start HostedServices                │
└──────────────┬──────────────────────────┘
			   │
			   ▼
┌─────────────────────────────────────────┐
│   MonitoringWorker.ExecuteAsync         │
│   (rodando continuamente)               │
└──────────────┬──────────────────────────┘
			   │
			   ├─→ Delay 5s (startup)
			   │
			   └─→ Loop while !StoppingToken
				   │
				   ├─→ Fetch active monitors
				   │
				   ├─→ For each monitor
				   │   │
				   │   ├─→ Check if due (interval)
				   │   │
				   │   ├─→ HTTP Request (timeout)
				   │   │
				   │   ├─→ Compare with last status
				   │   │
				   │   ├─→ If changed
				   │   │   ├─→ Create Log
				   │   │   ├─→ Send Email (retry 3x)
				   │   │   └─→ Record in HealthService
				   │   │
				   │   └─→ Update LastCheckedAt
				   │
				   ├─→ Record cycle metrics
				   │
				   └─→ Wait (CheckCycleIntervalSeconds)
```

## Dependency Injection (DI)

### Cycle de Vida

| Serviço | Scope | Registro |
|---------|-------|----------|
| `AppDbContext` | Scoped | AddDbContext |
| `IHttpClientFactory` | Singleton | AddHttpClient |
| `IWorkerHealthService` | Singleton | AddSingleton |
| `MonitoringWorker` | Singleton | AddHostedService |
| `ILogger<T>` | Singleton | Built-in |
| `IConfiguration` | Singleton | Built-in |

### Injeção no Worker

```csharp
public MonitoringWorker(
	IServiceProvider serviceProvider,      // Para criar scope de DB
	IHttpClientFactory httpClientFactory,  // Para requisições HTTP
	IConfiguration configuration,           // Para settings
	ILogger<MonitoringWorker> logger,      // Para logging
	IWorkerHealthService healthService)    // Para métricas
```

**Nota:** `AppDbContext` é `Scoped`, então precisa de `IServiceProvider.CreateScope()` dentro do loop.

## Segurança

### Validações

1. **Email Settings**
   - Valida se SmtpServer, SenderEmail, SenderPassword estão configurados
   - Lança `InvalidOperationException` se faltarem

2. **JWT Configuration**
   - Valida se `Jwt:Key` está configurado no startup
   - Falha rápido com mensagem clara

3. **Headers JSON**
   - Captura `JsonException` se headers inválido
   - Registra erro mas continua processamento

4. **CancellationToken**
   - Respeita token em todos os await
   - Shutdown gracioso sem perder dados

### Sensitivos

- Passwords não são logadas
- Auth headers truncados no log (primeiros 50 chars)
- Email passwords vêm de `appsettings` ou environment variables

## Performance e Escalabilidade

### Otimizações para Free Tier

1. **Connection Pooling**
   - MySQL auto-pool (padrão)
   - Scoped DbContext para evitar memory leak

2. **Delay Configurável**
   - Default 30s entre ciclos (economiza CPU)
   - Pode aumentar para 60s+ em free tier

3. **Timeout HTTP**
   - Default 10s para não bloquear thread por muito tempo
   - Pode reduzir para 5s em redes rápidas

4. **Logging Eficiente**
   - Apenas loga mudanças de status (menos IO)
   - Debug logs para ciclos (desabilitados em prod por padrão)

5. **Memory Management**
   - Scope criado e descartado a cada ciclo
   - Sem cache em memória (simplifica escalabilidade)

### Escalabilidade Horizontal

**Problema:** Multiple instances rodando o mesmo Worker

**Solução possível:** Implementar distributed lock (exemplo: Redis)
**Solução simples:** Rodar Worker apenas em 1 instância (usar env var para desabilitar)

```csharp
// Possível melhoria futura
if (!builder.Configuration.GetValue<bool>("MonitoringWorker:EnabledOnStartup"))
{
	// Skip registering HostedService
}
```

## Tratamento de Erros

### Estratégias por Tipo de Erro

| Tipo | Comportamento | Log |
|------|--------------|-----|
| **Timeout HTTP** | Status = 500, registra | Warning |
| **HTTP Error (non-2xx)** | Cria log, envia email | Debug/Info |
| **Email Falha** | Retry 3x com backoff | Warning/Error |
| **DB Connection Falha** | Log, continua próximo ciclo | Error |
| **JSON Headers Inválido** | Registra erro, pula | Warning |
| **Cancellation** | Break loop, shutdown | Info |

### Retry Logic (Email)

```
Tentativa 1: Send immediately
  ├─ Falha? → aguarda 1s
  │
Tentativa 2: Retry
  ├─ Falha? → aguarda 2s
  │
Tentativa 3: Final retry
  ├─ Falha? → throw exception
```

**Backoff:** exponencial (1s, 2s, 4s)

## Monitoramento em Produção

### Endpoints

```bash
# Health básico (sempre retorna 200)
GET /health
→ { "status": "ok" }

# Health detalhado do Worker
GET /health/worker
→ Métricas: uptime, ciclos, erros, last error

# Swagger (documentação)
GET /swagger
```

### Logs a Monitorar

**Startup:**
```
✓ Application started successfully
🔄 MonitoringWorker should be running in background
```

**Ciclos Normais:**
```
❤️ Heartbeat: application alive at {Time}
```

**Worker Ciclos (Debug):**
```
Processing {MonitorCount} active monitors
Monitoring cycle {CycleCount} completed - Processed: X, Skipped: Y
```

**Problemas:**
```
ERROR: Timeout checking monitor {MonitorId}
WARNING: Email send failed for monitor down alert
ERROR: Error verifying monitor {MonitorId}
```

## Migração de Código

### De Projeto Separado Para Integrado

**Antes:**
```csharp
// MonitoramentoWorker/Program.cs
var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureServices((context, services) =>
{
	services.AddDbContext<AppDbContext>(...);
	services.AddHostedService<MonitoringWorker>();
});
var host = builder.Build();
host.Run();
```

**Depois:**
```csharp
// MonitoramentoAPI/Program.cs
builder.Services.AddDbContext<AppDbContext>(...);
builder.Services.AddHostedService<MonitoringWorker>();
// ... rest of API config
app.Run();
```

### Mudanças no Código do Worker

1. ✅ Mesmo `MonitoringWorker.cs`
2. ✅ Mesmas dependências injetadas
3. ✅ Melhorado: logging detalhado
4. ✅ Melhorado: retry logic para email
5. ✅ Melhorado: health service integration
6. ✅ Melhorado: graceful shutdown

## Próximos Passos / Melhorias Possíveis

### Phase 1 (Atual)
- ✅ Worker integrado
- ✅ Health check
- ✅ Graceful shutdown
- ✅ Logging melhorado
- ✅ Retry logic

### Phase 2 (Futuro)
- [ ] Distributed lock para múltiplas instâncias
- [ ] Configuração de Worker via env var (enable/disable)
- [ ] Metrics exportadas para Prometheus
- [ ] Admin panel para gerenciar worker
- [ ] Alertas para Slack/Discord
- [ ] Request profiling e slow query detection

## Testing

### Unit Tests (Recomendado)

```csharp
[Test]
public async Task MonitoringWorker_ShouldProcessActiveMonitors()
{
	// Arrange
	var mockDb = new Mock<AppDbContext>();
	var mockHttp = new Mock<IHttpClientFactory>();
	var mockHealth = new Mock<IWorkerHealthService>();

	// Act
	var worker = new MonitoringWorker(
		serviceProvider, mockHttp.Object, config, logger, mockHealth.Object);

	// Assert
	Assert.IsTrue(true);
}
```

### Integration Tests

```csharp
[TestFixture]
public class MonitoringWorkerIntegrationTests
{
	[Test]
	public async Task Worker_ShouldSendEmailOnStatusChange()
	{
		// Create test database
		// Create monitor in DB
		// Run worker
		// Verify email was sent
	}
}
```

## Checklist de Deploy

- [ ] Todas as environment variables configuradas
- [ ] Banco de dados migrado (`db.Database.Migrate()`)
- [ ] Email settings testadas
- [ ] JWT key configurada
- [ ] Monitores criados no BD com `Ativo = true`
- [ ] Compilação bem-sucedida
- [ ] Health check retornando 200
- [ ] Worker health check mostrando `cyclesCompleted > 0`
- [ ] Logs aparecendo em tempo real
- [ ] Emails sendo recebidos após mudança de status

## Conclusão

Arquitetura simplificada, otimizada para free tier, com foco em:
- **Confiabilidade:** Graceful shutdown, retry logic, error handling
- **Observabilidade:** Logs detalhados, health check, métricas
- **Manutenibilidade:** Código consolidado, sem projetos separados
- **Performance:** Configurável, light-weight, async/await
