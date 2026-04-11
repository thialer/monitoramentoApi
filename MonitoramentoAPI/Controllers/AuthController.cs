using Microsoft.AspNetCore.Mvc;
using MonitoramentoAPI.Data;
using MonitoramentoAPI.Models;
using MonitoramentoAPI.Services;
using MonitoramentoAPI.DTOs;
namespace MonitoramentoAPI.Controllers

    //Rotas de autenticação para registro e login de usuários finalizadas

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

        [HttpPost("register")]
        public IActionResult Register(RegisterRequest request) 
        {
            if (_context.Users.Any(u => u.Email == request.Email))
                return BadRequest(new { message = "E-mail já cadastrado." });

            var user = new User
            {
                Nome = request.Nome,
                Email = request.Email,
                SenhaHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            return Ok(new { message = "Usuário registrado com sucesso!" });
        }

        [HttpPost("login")]
        public IActionResult Login(LoginRequest request) 
        {
            var user = _context.Users.FirstOrDefault(x => x.Email == request.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.SenhaHash))
                return Unauthorized(new { message = "E-mail ou senha inválidos." });

            var token = _tokenService.GenerateToken(user);

            return Ok(new { token });
        } 
    }
}