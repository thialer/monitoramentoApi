using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoramento.Shared.Data;
using Monitoramento.Shared.Data;
using Monitoramento.Shared.Models;
using Monitoramento.Shared.Models.DTOs;
using System.Net.Mail;
using System.Security.Claims;

namespace ApiMonitoramentoAPI.Controllers
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

            if (string.IsNullOrWhiteSpace(request.Destino))
                return BadRequest(new { message = "Destino é obrigatório." });

            if (!MailAddress.TryCreate(request.Destino, out _))
                return BadRequest(new { message = "Email inválido." });

            var jaExiste = _context.Alerts.Any(a =>
                a.UserId == userId &&
                a.Destino == request.Destino
            );

            if (jaExiste)
                return BadRequest(new { message = "Este email já está cadastrado." });

            var alert = new Alert
            {
                UserId = userId,
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

        [HttpPut("{id}")]
        public IActionResult Update(int id, CreateAlertRequest request)
        {
            var userId = GetUserId();

            var alert = _context.Alerts
                .FirstOrDefault(a => a.Id == id && a.UserId == userId);

            if (alert == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(request.Destino))
                return BadRequest(new { message = "Destino é obrigatório." });

            if (!MailAddress.TryCreate(request.Destino, out _))
                return BadRequest(new { message = "Email inválido." });

            var emailDuplicado = _context.Alerts.Any(a =>
                a.UserId == userId &&
                a.Destino == request.Destino &&
                a.Id != id
            );

            if (emailDuplicado)
                return BadRequest(new { message = "Este email já está cadastrado." });

            alert.Destino = request.Destino;

            _context.SaveChanges();

            return Ok(alert);
        }
    }
}