using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MonitoramentoAPI.Data;
using System.Security.Claims;
using MonitoramentoAPI.Models.DTOs;

namespace MonitoramentoAPI.Controllers
{
    [ApiController]
    [Route("user")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UserController(AppDbContext context)
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

        // GET /user
        // =========================
        [HttpGet]
        public IActionResult GetUser()
        {
            var userId = GetUserId();

            var user = _context.Users.FirstOrDefault(u => u.Id == userId);

            if (user == null)
                return NotFound();

            return Ok(new
            {
                user.Id,
                user.Nome,
                user.Email
                // depois aqui entra plano
            });
        }

        // PUT /user
        // =========================
        [HttpPut]
        public IActionResult UpdateUser([FromBody] UpdateUserRequest request)
        {
            var userId = GetUserId();

            var user = _context.Users.FirstOrDefault(u => u.Id == userId);

            if (user == null)
                return NotFound();

            user.Nome = request.Nome;
            user.Email = request.Email;

            _context.SaveChanges();

            return Ok(new { message = "Usuário atualizado com sucesso." });
        }

        // DELETE /user
        // =========================
        [HttpDelete]
        public IActionResult DeleteUser()
        {
            var userId = GetUserId();

            var user = _context.Users.FirstOrDefault(u => u.Id == userId);

            if (user == null)
                return NotFound();

            _context.Users.Remove(user);
            _context.SaveChanges();

            return Ok(new { message = "Conta excluída com sucesso." });
        }
    }
}