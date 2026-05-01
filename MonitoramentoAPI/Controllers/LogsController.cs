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

        // GET /logs/{ApiMonitorId}
        // =========================
        [HttpGet("{ApiMonitorId}")]
        public IActionResult GetLogs(int ApiMonitorId)
        {
            var userId = GetUserId();

            var ApiMonitor = _context.ApiMonitors
                .FirstOrDefault(m => m.Id == ApiMonitorId && m.UserId == userId);

            if (ApiMonitor == null)
                return NotFound(new { message = "ApiMonitor não encontrado." });

            var logs = _context.Logs
                .Where(l => l.ApiMonitorId == ApiMonitorId)
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
    }
}