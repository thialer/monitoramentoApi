using Microsoft.AspNetCore.Mvc;
using Monitoramento.Shared.Data;
using System.Text.Json;

namespace ApiMonitoramentoAPI.Controllers
{
    [ApiController]
    [Route("billing")]
    public class BillingController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BillingController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            if (string.IsNullOrEmpty(body))
                return BadRequest("Body vazio");

            try
            {
                var json = JsonDocument.Parse(body);

                if (!json.RootElement.TryGetProperty("type", out var typeProperty))
                    return BadRequest("Evento sem tipo");

                var eventType = typeProperty.GetString();

                if (eventType != "payment.approved")
                    return Ok();

                var userId = json.RootElement
                    .GetProperty("data")
                    .GetProperty("metadata")
                    .GetProperty("userId")
                    .GetInt32();

                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                    return NotFound("Usuário não encontrado");

                user.Plano = "PRO"; 

                await _context.SaveChangesAsync();

                Console.WriteLine($"Usuário {userId} virou PRO");

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro: {ex.Message}");
                return BadRequest("Erro no webhook");
            }
        }
    }
}