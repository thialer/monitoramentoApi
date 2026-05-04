using Microsoft.EntityFrameworkCore;
using Monitoramento.Shared.Data;
using Monitoramento.Shared.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace MonitoramentoWorker;

public class Worker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;

    public Worker(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var monitores = await db.ApiMonitors
                .Where(x => x.Ativo)
                .ToListAsync(stoppingToken);

            foreach (var monitor in monitores)
            {
                var deveExecutar =
                    monitor.LastCheckedAt == null ||
                    DateTime.UtcNow >= monitor.LastCheckedAt.Value.AddMinutes(monitor.Intervalo);

                if (!deveExecutar)
                    continue;

                await VerificarMonitor(monitor, db);
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task VerificarMonitor(ApiMonitor monitor, AppDbContext db)
    {
        var client = _httpClientFactory.CreateClient();

        client.Timeout = TimeSpan.FromSeconds(30);

        var stopwatch = Stopwatch.StartNew();

        int statusCode = 0;

        try
        {
            // REQUEST DINÂMICO (GET/POST/PUT/DELETE)
            var request = new HttpRequestMessage(
                new HttpMethod(monitor.Metodo ?? "GET"),
                monitor.Url
            );

            // BODY
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
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(monitor.Headers);

                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
            }

            var response = await client.SendAsync(request);

            stopwatch.Stop();

            statusCode = (int)response.StatusCode;
        }
        catch
        {
            stopwatch.Stop();
            statusCode = 500;
        }

        var log = new Log
        {
            MonitorId = monitor.Id,
            StatusCode = statusCode,
            ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
            IsUp = statusCode >= 200 && statusCode < 300,
            Erro = statusCode == 500 ? "Falha ao acessar endpoint" : null,
            CreatedAt = DateTime.UtcNow
        };

        db.Logs.Add(log);

        monitor.LastCheckedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }
}