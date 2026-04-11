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

        [HttpPost]
        public IActionResult Create(CreateMonitorRequest request)
        {
            var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(claimId))
                return Unauthorized();
            //validação da URL
            if (!Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
            {
                return BadRequest(new { message = "URL inválida." });
            }


            if (!Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
                return BadRequest(new { message = "URL inválida." });

            if (request.Intervalo < 1)
                return BadRequest(new { message = "Intervalo mínimo é 1 minuto." });

            var monitor = new Monitor
            {
                Nome = request.Nome,
                Url = request.Url,
                Tipo = request.Tipo,
                Intervalo = request.Intervalo,
                UserId = int.Parse(claimId),
                StatusAtual = "unknown",
                Ativo = true
            };

            _context.Monitors.Add(monitor);
            _context.SaveChanges();

            return CreatedAtAction(nameof(GetById), new { id = monitor.Id }, monitor);
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value; 
            if (string.IsNullOrEmpty(claimId))
                return Unauthorized();

            var userId = int.Parse(claimId);

            var monitors = _context.Monitors
                .Where(m => m.UserId == userId)
                .ToList();

            return Ok(monitors);
        }

        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(claimId))
                return Unauthorized();

            var userId = int.Parse(claimId);

            var monitor = _context.Monitors
                .FirstOrDefault(m => m.Id == id && m.UserId == userId);

            if (monitor == null)
                return NotFound();

            return Ok(monitor);

        }
    }
}