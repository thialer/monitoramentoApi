using Microsoft.EntityFrameworkCore;
using Monitoramento.Shared.Data;
using Monitoramento.Shared.Models;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

namespace ApiMonitoramentoAPI.Services;

public class MonitoringWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MonitoringWorker> _logger;
    private readonly IWorkerHealthService _healthService;

    // Configurable intervals (in seconds) - default 30s check cycle, 10s http timeout
    private int _checkCycleIntervalSeconds = 30;
    private int _httpTimeoutSeconds = 10;
    private const int MAX_EMAIL_RETRIES = 3;

    public MonitoringWorker(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<MonitoringWorker> logger,
        IWorkerHealthService healthService)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _healthService = healthService;

        // Load from config if available (for free tier optimization)
        if (int.TryParse(configuration["MonitoringWorker:CheckCycleIntervalSeconds"], out var cycleInterval))
        {
            _checkCycleIntervalSeconds = cycleInterval;
        }

        if (int.TryParse(configuration["MonitoringWorker:HttpTimeoutSeconds"], out var httpTimeout))
        {
            _httpTimeoutSeconds = httpTimeout;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "MonitoringWorker started - CheckCycleInterval: {CycleInterval}s, HttpTimeout: {HttpTimeout}s",
            _checkCycleIntervalSeconds,
            _httpTimeoutSeconds
        );

        // Add initial delay to prevent connection pool exhaustion on startup
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        int cycleCount = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            cycleCount++;
            try
            {
                _logger.LogDebug("Starting monitoring cycle {CycleCount}", cycleCount);

                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var monitores = await db.ApiMonitors
                    .Where(x => x.Ativo)
                    .ToListAsync(stoppingToken);

                if (monitores.Count == 0)
                {
                    _logger.LogDebug("No active monitors to check");
                }
                else
                {
                    _logger.LogDebug("Processing {MonitorCount} active monitors", monitores.Count);
                }

                int processedCount = 0;
                int skippedCount = 0;

                foreach (var monitor in monitores)
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Cancellation requested during monitor loop");
                        break;
                    }

                    var deveExecutar =
                        monitor.LastCheckedAt == null ||
                        DateTime.UtcNow >= monitor.LastCheckedAt.Value.AddMinutes(monitor.Intervalo);

                    if (!deveExecutar)
                    {
                        skippedCount++;
                        continue;
                    }

                    try
                    {
                        await VerificarMonitor(monitor, db, stoppingToken);
                        processedCount++;
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Monitor check for {MonitorId} ({MonitorName}) was cancelled", monitor.Id, monitor.Nome);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error verifying monitor {MonitorId} ({MonitorName}) - URL: {Url}", 
                            monitor.Id, monitor.Nome, monitor.Url);
                    }
                }

                if (processedCount > 0 || skippedCount > 0)
                {
                    _logger.LogDebug("Monitoring cycle {CycleCount} completed - Processed: {ProcessedCount}, Skipped: {SkippedCount}", 
                        cycleCount, processedCount, skippedCount);

                    // Record cycle metrics for health check
                    _healthService.RecordCycle(processedCount, skippedCount);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("MonitoringWorker cycle {CycleCount} was cancelled", cycleCount);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MonitoringWorker cycle {CycleCount} - continuing...", cycleCount);
                _healthService.RecordError(ex);
                // Continue on error rather than stopping
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_checkCycleIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("MonitoringWorker delay was cancelled - shutting down gracefully");
                break;
            }
        }

        _logger.LogInformation("MonitoringWorker stopped gracefully after {CycleCount} cycles", cycleCount);
    }

    private async Task VerificarMonitor(ApiMonitor monitor, AppDbContext db, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(_httpTimeoutSeconds);

        var stopwatch = Stopwatch.StartNew();
        int statusCode = 0;
        string? errorDetail = null;

        try
        {
            var request = new HttpRequestMessage(
                new HttpMethod(monitor.Metodo ?? "GET"),
                monitor.Url
            );

            if (!string.IsNullOrEmpty(monitor.Body))
            {
                request.Content = new StringContent(
                    monitor.Body,
                    Encoding.UTF8,
                    "application/json"
                );
            }

            if (!string.IsNullOrEmpty(monitor.Headers))
            {
                try
                {
                    var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(monitor.Headers);
                    if (headers != null)
                    {
                        foreach (var header in headers)
                        {
                            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Invalid JSON in monitor headers for {MonitorId}", monitor.Id);
                    errorDetail = $"Invalid headers JSON: {ex.Message}";
                }
            }

            var response = await client.SendAsync(request, cancellationToken);
            stopwatch.Stop();
            statusCode = (int)response.StatusCode;
            _logger.LogDebug("Monitor {MonitorId} ({MonitorName}) returned status {StatusCode} in {ElapsedMs}ms", 
                monitor.Id, monitor.Nome, statusCode, stopwatch.ElapsedMilliseconds);
        }
        catch (TaskCanceledException ex)
        {
            stopwatch.Stop();
            statusCode = 500;
            errorDetail = $"Request timeout after {_httpTimeoutSeconds}s";
            _logger.LogWarning(ex, "Timeout checking monitor {MonitorId} ({MonitorName})", monitor.Id, monitor.Nome);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            statusCode = 500;
            errorDetail = $"HTTP error: {ex.Message}";
            _logger.LogWarning(ex, "HTTP error checking monitor {MonitorId} ({MonitorName})", monitor.Id, monitor.Nome);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            statusCode = 500;
            errorDetail = $"Unexpected error: {ex.Message}";
            _logger.LogError(ex, "Unexpected error checking monitor {MonitorId} ({MonitorName})", monitor.Id, monitor.Nome);
        }

        var isUp = statusCode >= 200 && statusCode < 300;

        var ultimoLog = db.Logs
            .Where(l => l.MonitorId == monitor.Id)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefault();

        monitor.LastCheckedAt = DateTime.UtcNow;

        var caiuAgora = !isUp &&
                        (ultimoLog == null || ultimoLog.IsUp == true);

        var voltouAgora = isUp &&
                          ultimoLog != null &&
                          ultimoLog.IsUp == false;

        // Save log only when status changes
        if (caiuAgora || voltouAgora)
        {
            var log = new Log
            {
                MonitorId = monitor.Id,
                StatusCode = statusCode,
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                IsUp = isUp,
                Erro = !isUp ? (errorDetail ?? "Falha ao acessar endpoint") : null,
                CreatedAt = DateTime.UtcNow
            };

            db.Logs.Add(log);
            _logger.LogInformation("Log created for monitor {MonitorId}: {Status}", 
                monitor.Id, isUp ? "UP" : "DOWN");
        }

        var alerts = db.Alerts
            .Where(a => a.UserId == monitor.UserId && a.Tipo == "email")
            .ToList();

        if (!alerts.Any())
        {
            _logger.LogDebug("No email alerts configured for monitor {MonitorId}", monitor.Id);
        }

        if (caiuAgora)
        {
            _logger.LogWarning("Monitor {MonitorId} ({MonitorName}) just went DOWN", monitor.Id, monitor.Nome);

            foreach (var alert in alerts)
            {
                try
                {
                    await EnviarEmailComRetry(
                        alert.Destino,
                        "Monitor caiu",
                        $"O monitor '{monitor.Nome}' apresentou falha. Status code: {statusCode}. Erro: {errorDetail ?? "desconhecido"}",
                        monitor.Id
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send email alert for monitor down (MonitorId: {MonitorId}, Email: {Email})", 
                        monitor.Id, alert.Destino);
                }
            }
        }

        if (voltouAgora)
        {
            _logger.LogInformation("Monitor {MonitorId} ({MonitorName}) just went UP", monitor.Id, monitor.Nome);

            foreach (var alert in alerts)
            {
                try
                {
                    await EnviarEmailComRetry(
                        alert.Destino,
                        "Monitor normalizado",
                        $"O monitor '{monitor.Nome}' voltou ao normal. Status code: {statusCode}",
                        monitor.Id
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send email alert for monitor up (MonitorId: {MonitorId}, Email: {Email})", 
                        monitor.Id, alert.Destino);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnviarEmailComRetry(string destino, string assunto, string mensagem, int monitorId)
    {
        int tentativa = 0;
        Exception? ultimaExcecao = null;

        while (tentativa < MAX_EMAIL_RETRIES)
        {
            tentativa++;
            try
            {
                await EnviarEmail(destino, assunto, mensagem);
                _logger.LogInformation("Email sent successfully for monitor {MonitorId} to {Destino} (attempt {Attempt}/{MaxRetries})", 
                    monitorId, destino, tentativa, MAX_EMAIL_RETRIES);
                return;
            }
            catch (Exception ex)
            {
                ultimaExcecao = ex;
                _logger.LogWarning(ex, "Email send failed for monitor {MonitorId} to {Destino} (attempt {Attempt}/{MaxRetries})", 
                    monitorId, destino, tentativa, MAX_EMAIL_RETRIES);

                if (tentativa < MAX_EMAIL_RETRIES)
                {
                    // Exponential backoff: 1s, 2s, 4s
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, tentativa - 1)));
                }
            }
        }

        throw new InvalidOperationException(
            $"Failed to send email to {destino} after {MAX_EMAIL_RETRIES} attempts",
            ultimaExcecao
        );
    }

    private async Task EnviarEmail(string destino, string assunto, string mensagem)
    {
        var smtpServer = _configuration["EmailSettings:SmtpServer"];
        var port = int.Parse(_configuration["EmailSettings:Port"]!);
        var senderEmail = _configuration["EmailSettings:SenderEmail"];
        var senderPassword = _configuration["EmailSettings:SenderPassword"];

        if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(senderPassword))
        {
            throw new InvalidOperationException("Email settings are not properly configured. Check EmailSettings in configuration.");
        }

        try
        {
            using var client = new SmtpClient(smtpServer, port)
            {
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail),
                Subject = assunto,
                Body = mensagem,
                IsBodyHtml = false
            };

            mailMessage.To.Add(destino);

            await client.SendMailAsync(mailMessage);
        }
        catch (SmtpException ex)
        {
            throw new InvalidOperationException($"SMTP error sending to {destino}: {ex.StatusCode}", ex);
        }
    }
}
