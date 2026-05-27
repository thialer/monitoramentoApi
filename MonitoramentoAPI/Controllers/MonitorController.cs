using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoramento.Shared.Data;
using Monitoramento.Shared.Models;
using Monitoramento.Shared.Models.DTOs;
using System.Security.Claims;

namespace ApiMonitoramentoAPI.Controllers
{
    [ApiController]
    [Route("ApiMonitors")]
    [Authorize]
    public class ApiMonitorsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ApiMonitorsController(AppDbContext context)
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


        // POST /ApiMonitors
        // =========================
        [HttpPost]
        public IActionResult Create(CreateApiMonitorRequest request)
        {
            var userId = GetUserId();

            if (!Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
                return BadRequest(new { message = "URL inválida." });

            if (request.Intervalo < 1)
                return BadRequest(new { message = "Intervalo mínimo é 1 minuto." });

            var user = _context.Users.Find(userId);

            if (user == null)
                return Unauthorized();

            if (user.Plano == "FREE")
            {
                request.Intervalo = 15;
            }

            var totalApiMonitors = _context.ApiMonitors.Count(m => m.UserId == userId);

            if (user.Plano == "FREE" && totalApiMonitors >= 1)
            {
                return BadRequest(new { message = "Plano FREE permite apenas 1 ApiMonitor. Faça upgrade para PRO." });
            }

            if (user.Plano == "PRO" && totalApiMonitors >= 5)
            {
                return BadRequest(new { message = "Limite máximo de ApiMonitores atingido." });
            }

            var ApiMonitor = new ApiMonitor
            {
                Nome = request.Nome,
                Url = request.Url,
                Metodo = request.Metodo,
                Headers = request.Headers,
                Body = request.Body,
                Tipo = request.Tipo,
                Intervalo = request.Intervalo,
                UserId = userId,
                StatusAtual = "unknown",
                Ativo = true
            };

            _context.ApiMonitors.Add(ApiMonitor);
            _context.SaveChanges();

            return CreatedAtAction(nameof(GetById), new { id = ApiMonitor.Id }, ApiMonitor);
        }

        // GET /ApiMonitors
        // =========================
        [HttpGet]
        public IActionResult GetAll()
        {
            var userId = GetUserId();

            var ApiMonitors = _context.ApiMonitors
                .Where(m => m.UserId == userId)
                .ToList();

            return Ok(ApiMonitors);
        }

        // GET /ApiMonitors/{id}
        // =========================
        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            var userId = GetUserId();

            var ApiMonitor = _context.ApiMonitors
                .FirstOrDefault(m => m.Id == id && m.UserId == userId);

            if (ApiMonitor == null)
                return NotFound();

            return Ok(ApiMonitor);
        }

    
        // PUT /ApiMonitors/{id}
        // =========================
        [HttpPut("{id}")]
        public IActionResult Update(int id, UpdateApiMonitorRequest request)
        {
            var userId = GetUserId();

            var ApiMonitor = _context.ApiMonitors
                .FirstOrDefault(m => m.Id == id && m.UserId == userId);

            if (ApiMonitor == null)
                return NotFound();

            if (!Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
                return BadRequest(new { message = "URL inválida." });

            if (request.Intervalo < 1)
                return BadRequest(new { message = "Intervalo mínimo é 1 minuto." });

            ApiMonitor.Nome = request.Nome;
            ApiMonitor.Url = request.Url;
            ApiMonitor.Intervalo = request.Intervalo;

            _context.SaveChanges();

            return Ok(new { message = "ApiMonitor atualizado com sucesso." });
        }

        // DELETE /ApiMonitors/{id}
        // =========================
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var userId = GetUserId();

            var ApiMonitor = _context.ApiMonitors
                .FirstOrDefault(m => m.Id == id && m.UserId == userId);

            if (ApiMonitor == null)
                return NotFound();

            _context.ApiMonitors.Remove(ApiMonitor);
            _context.SaveChanges();

            return Ok(new { message = "ApiMonitor removido com sucesso." });
        }

        // PATCH /ApiMonitors/{id}/toggle
        // =========================
        [HttpPatch("{id}/toggle")]
        public IActionResult Toggle(int id)
        {
            var userId = GetUserId();

            var ApiMonitor = _context.ApiMonitors
                .FirstOrDefault(m => m.Id == id && m.UserId == userId);

            if (ApiMonitor == null)
                return NotFound();

            ApiMonitor.Ativo = !ApiMonitor.Ativo;

            _context.SaveChanges();

            return Ok(new
            {
                message = "Status alterado com sucesso.",
                ativo = ApiMonitor.Ativo
            });
        }

    }
}