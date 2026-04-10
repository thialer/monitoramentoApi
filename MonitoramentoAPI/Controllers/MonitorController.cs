using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MonitoramentoAPI.Data;
using MonitoramentoAPI.Models;
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
        public IActionResult Create(Monitor monitor)
        {
            var claimId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(claimId))
            {
                return Unauthorized(new { message = "Usuário não identificado no token." });
            }

            monitor.UserId = int.Parse(claimId);

            _context.Monitors.Add(monitor);
            _context.SaveChanges();

            return Ok(monitor);
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
    }
}