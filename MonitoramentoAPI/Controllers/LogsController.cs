using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoramento.Shared.Data;
using System.Security.Claims;

namespace ApiMonitoramentoAPI.Controllers
{
    [ApiController]
    [Route("logs")]
    [Authorize]
    public class LogsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public LogsController(AppDbContext context)
        {
            _context = context;
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (claim == null)
                throw new Exception("Token inválido");

            return int.Parse(claim.Value);
        }

        // GET /logs/{MonitorId}
        // =========================
        [HttpGet("{MonitorId}")]
        public IActionResult GetLogs(int MonitorId)
        {
            var userId = GetUserId();

            var ApiMonitor = _context.ApiMonitors
                .FirstOrDefault(m => m.Id == MonitorId && m.UserId == userId);

            if (ApiMonitor == null)
                return NotFound(new { message = "ApiMonitor não encontrado." });

            var logs = _context.Logs
                .Where(l => l.MonitorId == MonitorId)
                .OrderByDescending(l => l.CreatedAt)
                .Take(50)
                .Select(l => new
                {
                    l.Id,
                    l.IsUp,
                    l.StatusCode,
                    l.ResponseTimeMs,
                    l.Erro,
                    l.CreatedAt
                })
                .ToList();

            return Ok(logs);
        }
        [HttpGet]
        public IActionResult GetAllLogs()
        {
            var userId = GetUserId();

            var logs = (
                from log in _context.Logs
                join monitor in _context.ApiMonitors
                    on log.MonitorId equals monitor.Id
                where monitor.UserId == userId
                orderby log.CreatedAt descending
                select new
                {
                    log.Id,
                    log.MonitorId,
                    MonitorName = monitor.Nome,
                    Status = log.IsUp ? "ok" : "error",
                    StatusCode = log.StatusCode,
                    ResponseTime = log.ResponseTimeMs,
                    Error = log.Erro,
                    Timestamp = log.CreatedAt
                }
            )
            .Take(100)
            .ToList();

            return Ok(logs);
        }

    }
}