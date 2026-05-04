using Microsoft.EntityFrameworkCore;
using Monitoramento.Shared.Data;
using Monitoramento.Shared.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Mail;
namespace MonitoramentoWorker;

public class Worker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    public Worker(
       IServiceProvider serviceProvider,
       IHttpClientFactory httpClientFactory,
       IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
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

        var isUp = statusCode >= 200 && statusCode < 300;

        var ultimoLog = db.Logs
            .Where(l => l.MonitorId == monitor.Id)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefault();

        var log = new Log
        {
            MonitorId = monitor.Id,
            StatusCode = statusCode,
            ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
            IsUp = isUp,
            Erro = isUp ? null : "Falha ao acessar endpoint",
            CreatedAt = DateTime.UtcNow
        };

        db.Logs.Add(log);

        monitor.LastCheckedAt = DateTime.UtcNow;

        var caiuAgora = !isUp &&
                       (ultimoLog == null || ultimoLog.IsUp == true);
        var voltouAgora = isUp &&
                 ultimoLog != null &&
                 ultimoLog.IsUp == false;

        var alerts = db.Alerts
    .Where(a => a.UserId == monitor.UserId && a.Tipo == "email")
    .ToList();

        if (caiuAgora)
        {
            foreach (var alert in alerts)
            {
                try
                {
                    await EnviarEmail(
                        alert.Destino,
                        "Monitor caiu",
                        $"O monitor '{monitor.Nome}' apresentou falha. Status code: {statusCode}"
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao enviar email: {ex.Message}");
                }
            }
        }

        if (voltouAgora)
        {
            foreach (var alert in alerts)
            {
                try
                {
                    await EnviarEmail(
                        alert.Destino,
                        "Monitor normalizado",
                        $"O monitor '{monitor.Nome}' voltou ao normal. Status code: {statusCode}"
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao enviar email: {ex.Message}");
                }
            }
        }

        await db.SaveChangesAsync();
    }
    private async Task EnviarEmail(string destino, string assunto, string mensagem)
    {
        var smtpServer = _configuration["EmailSettings:SmtpServer"];
        var port = int.Parse(_configuration["EmailSettings:Port"]!);
        var senderEmail = _configuration["EmailSettings:SenderEmail"];
        var senderPassword = _configuration["EmailSettings:SenderPassword"];

        using var client = new SmtpClient(smtpServer, port)
        {
            Credentials = new NetworkCredential(senderEmail, senderPassword),
            EnableSsl = true
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(senderEmail!),
            Subject = assunto,
            Body = mensagem,
            IsBodyHtml = false
        };

        mailMessage.To.Add(destino);

        await client.SendMailAsync(mailMessage);
    }
}