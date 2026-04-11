using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MonitoramentoAPI.Data;
using MonitoramentoAPI.Models;
using MonitoramentoAPI.Models.DTOs;
using System.Security.Claims;

namespace MonitoramentoAPI.Controllers
{
    [ApiController]
    [Route("monitors")]
    [Authorize]
    public class MonitorsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MonitorsController(AppDbContext context)
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

        
        // POST /monitors
        // =========================
        [HttpPost]
        public IActionResult Create(CreateMonitorRequest request)
        {
            var userId = GetUserId();

            if (!Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
                return BadRequest(new { message = "URL inválida." });

            if (request.Intervalo < 1)
                return BadRequest(new { message = "Intervalo mínimo é 1 minuto." });

            var totalMonitors = _context.Monitors.Count(m => m.UserId == userId);

            if (totalMonitors >= 5)
                return BadRequest(new { message = "Limite de monitores atingido." });

            var monitor = new Monitor
            {
                Nome = request.Nome,
                Url = request.Url,
                Tipo = request.Tipo,
                Intervalo = request.Intervalo,
                UserId = userId,
                StatusAtual = "unknown",
                Ativo = true
            };

            _context.Monitors.Add(monitor);
            _context.SaveChanges();

            return CreatedAtAction(nameof(GetById), new { id = monitor.Id }, monitor);
        }

        // GET /monitors
        // =========================
        [HttpGet]
        public IActionResult GetAll()
        {
            var userId = GetUserId();

            var monitors = _context.Monitors
                .Where(m => m.UserId == userId)
                .ToList();

            return Ok(monitors);
        }

        // GET /monitors/{id}
        // =========================
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            var userId = GetUserId();

            var monitor = _context.Monitors
                .FirstOrDefault(m => m.Id == id && m.UserId == userId);

            if (monitor == null)
                return NotFound();

            return Ok(monitor);
        }

    
        // PUT /monitors/{id}
        // =========================
        [HttpPut("{id}")]
        public IActionResult Update(int id, UpdateMonitorRequest request)
        {
            var userId = GetUserId();

            var monitor = _context.Monitors
                .FirstOrDefault(m => m.Id == id && m.UserId == userId);

            if (monitor == null)
                return NotFound();

            if (!Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
                return BadRequest(new { message = "URL inválida." });

            if (request.Intervalo < 1)
                return BadRequest(new { message = "Intervalo mínimo é 1 minuto." });

            monitor.Nome = request.Nome;
            monitor.Url = request.Url;
            monitor.Intervalo = request.Intervalo;

            _context.SaveChanges();

            return Ok(new { message = "Monitor atualizado com sucesso." });
        }

        // DELETE /monitors/{id}
        // =========================
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var userId = GetUserId();

            var monitor = _context.Monitors
                .FirstOrDefault(m => m.Id == id && m.UserId == userId);

            if (monitor == null)
                return NotFound();

            _context.Monitors.Remove(monitor);
            _context.SaveChanges();

            return Ok(new { message = "Monitor removido com sucesso." });
        }

        // PATCH /monitors/{id}/toggle
        // =========================
        [HttpPatch("{id}/toggle")]
        public IActionResult Toggle(int id)
        {
            var userId = GetUserId();

            var monitor = _context.Monitors
                .FirstOrDefault(m => m.Id == id && m.UserId == userId);

            if (monitor == null)
                return NotFound();

            monitor.Ativo = !monitor.Ativo;

            _context.SaveChanges();

            return Ok(new
            {
                message = "Status alterado com sucesso.",
                ativo = monitor.Ativo
            });
        }

        // POST /monitors/{id}/check-now
        // =========================
        [HttpPost("{id}/check-now")]
        public IActionResult CheckNow(int id)
        {
            var userId = GetUserId();

            var monitor = _context.Monitors
                .FirstOrDefault(m => m.Id == id && m.UserId == userId);

            if (monitor == null)
                return NotFound();

            return Ok(new
            {
                message = "Verificação iniciada com sucesso.",
                monitor = monitor.Nome
            });
        }
    }
}