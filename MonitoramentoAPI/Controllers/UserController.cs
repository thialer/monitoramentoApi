using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MonitoramentoAPI.Controllers
{
    [ApiController]
    [Route("user")]
    public class UserController : ControllerBase
    {
        [HttpGet]
        [Authorize]
        public IActionResult GetUser()
        {
            return Ok("autenticado!");
        }
    }
}