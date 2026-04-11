using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MonitoramentoAPI.Data;
using MonitoramentoAPI.Models;
using MonitoramentoAPI.Models.DTOs;
using System.Security.Claims;

namespace MonitoramentoAPI.Controllers
{
    [ApiController]
    [Route("alerts")]
    [Authorize]
    public class AlertsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AlertsController(AppDbContext context)
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

        // POST /alerts
        // =========================
        [HttpPost]
        public IActionResult Create(CreateAlertRequest request)
        {
            var userId = GetUserId();

            if (string.IsNullOrEmpty(request.Tipo) || string.IsNullOrEmpty(request.Destino))
                return BadRequest(new { message = "Tipo e destino são obrigatórios." });

            if (request.Tipo.ToLower() != "email" && request.Tipo.ToLower() != "whatsapp")
                return BadRequest(new { message = "Tipo inválido. Use 'email' ou 'whatsapp'." });

            var alert = new Alert
            {
                UserId = userId,
                Tipo = request.Tipo.ToLower(),
                Destino = request.Destino
            };

            _context.Alerts.Add(alert);
            _context.SaveChanges();

            return Ok(alert);
        }

        // GET /alerts
        // =========================
        [HttpGet]
        public IActionResult GetAll()
        {
            var userId = GetUserId();

            var alerts = _context.Alerts
                .Where(a => a.UserId == userId)
                .ToList();

            return Ok(alerts);
        }

        // DELETE /alerts/{id}
        // =========================
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var userId = GetUserId();

            var alert = _context.Alerts
                .FirstOrDefault(a => a.Id == id && a.UserId == userId);

            if (alert == null)
                return NotFound();

            _context.Alerts.Remove(alert);
            _context.SaveChanges();

            return Ok(new { message = "Alerta removido com sucesso." });
        }
    }
}