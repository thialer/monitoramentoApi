using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MonitoramentoAPI.Data;
using System.Security.Claims;

namespace MonitoramentoAPI.Controllers
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

        // GET /logs/{monitorId}
        // =========================
        [HttpGet("{monitorId}")]
        public IActionResult GetLogs(int monitorId)
        {
            var userId = GetUserId();

            var monitor = _context.Monitors
                .FirstOrDefault(m => m.Id == monitorId && m.UserId == userId);

            if (monitor == null)
                return NotFound(new { message = "Monitor não encontrado." });

            var logs = _context.Logs
                .Where(l => l.MonitorId == monitorId)
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