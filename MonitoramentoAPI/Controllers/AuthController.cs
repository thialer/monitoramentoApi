using ApiMonitoramentoAPI.DTOs;
using ApiMonitoramentoAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoramento.Shared.Data;
using Monitoramento.Shared.Models;
using Monitoramento.Shared.Models.DTOs;
using System.Net.Mail;
using System.Security.Claims;
namespace ApiMonitoramentoAPI.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly TokenService _tokenService;

        public AuthController(AppDbContext context, TokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
        }

        // REGISTER
        // =========================
        [HttpPost("register")]
        public IActionResult Register(RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "E-mail é obrigatório." });

            if (!MailAddress.TryCreate(request.Email, out _))
                return BadRequest(new { message = "E-mail inválido." });

            if (_context.Users.Any(u => u.Email == request.Email))
                return BadRequest(new { message = "E-mail já cadastrado." });

            var user = new User
            {
                Nome = request.Nome,
                Email = request.Email,
                SenhaHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Plano = "FREE"
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            return Ok(new { message = "Usuário registrado com sucesso!" });
        }

        // LOGIN
        // =========================
        [HttpPost("login")]
        public IActionResult Login(LoginRequest request)
        {
            var user = _context.Users.FirstOrDefault(x => x.Email == request.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.SenhaHash))
                return Unauthorized(new { message = "E-mail ou senha inválidos." });

            var token = _tokenService.GenerateToken(user);

            return Ok(new
            {
                token,
                nome = user.Nome,
                email = user.Email,
                plano = user.Plano
            });
        }


        //Plano CHANGE
        // =========================
        [Authorize]
        [HttpPatch("change-plan")]
        public IActionResult ChangePlan([FromBody] string plano)
        {
            var userId = GetUserId();

            if (userId == 0)
                return Unauthorized();

            var user = _context.Users.FirstOrDefault(u => u.Id == userId);

            if (user == null)
                return NotFound();

            if (plano != "FREE" && plano != "PRO")
                return BadRequest("Plano inválido.");

            user.Plano = plano;
            _context.SaveChanges();

            return Ok(new
            {
                message = $"Plano alterado para {plano} com sucesso."
            });
        }

        // FORGOT PASSWORD
        // =========================
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public IActionResult ForgotPassword(ForgotPasswordRequest request)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == request.Email);

            if (user == null)
                return Ok(new { message = "Se o email existir, você receberá instruções." });

            var oldTokens = _context.PasswordResetTokens
                .Where(t => t.UserId == user.Id && !t.Used);

            _context.PasswordResetTokens.RemoveRange(oldTokens);

            var token = Guid.NewGuid().ToString();

            var resetToken = new PasswordResetToken
            {
                UserId = user.Id,
                Token = token,
                Expiration = DateTime.UtcNow.AddHours(1),
                CreatedAt = DateTime.UtcNow
            };

            _context.PasswordResetTokens.Add(resetToken);
            _context.SaveChanges();

            // simulação de envio de email
            Console.WriteLine($"Token de reset: {token}");

            return Ok(new { message = "Se o email existir, você receberá instruções." });
        }

        // =========================
        // RESET PASSWORD
        // =========================
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public IActionResult ResetPassword(ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            {
                return BadRequest(new { message = "Senha deve ter no mínimo 6 caracteres." });
            }

            var token = _context.PasswordResetTokens
                .FirstOrDefault(t => t.Token == request.Token);

            if (token == null || token.Used || token.Expiration < DateTime.UtcNow)
                return BadRequest(new { message = "Token inválido ou expirado." });

            var user = _context.Users.FirstOrDefault(u => u.Id == token.UserId);

            if (user == null)
                return BadRequest();

            user.SenhaHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

            token.Used = true;
            _context.PasswordResetTokens.Remove(token);

            _context.SaveChanges();

            return Ok(new { message = "Senha redefinida com sucesso." });
        }

        // Adicionado método auxiliar para obter o Id do usuário a partir dos claims
        private int GetUserId()
        {
            if (User?.Identity?.IsAuthenticated != true)
                return 0;

            // Tenta várias chaves comuns de claim para o identificador do usuário
            var idValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("id")?.Value
                          ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrWhiteSpace(idValue))
                return 0;

            if (int.TryParse(idValue, out var id))
                return id;

            // Se o Id não for inteiro, retorna 0 (tratamento existente no controller lida com NotFound)
            return 0;
        }
    }
}