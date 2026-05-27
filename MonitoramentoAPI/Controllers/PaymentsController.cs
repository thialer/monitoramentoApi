using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe.Checkout;
using System.Security.Claims;
using Monitoramento.Shared.Data;
namespace ApiMonitoramentoAPI.Controllers
{
    [ApiController]
    [Route("payments")]
    [Authorize]
    public class PaymentsController : ControllerBase
    {

        public PaymentsController(AppDbContext context)
        {
            _context = context;
        }

        private readonly AppDbContext _context;

        [HttpPost("create-checkout-session")]
        public IActionResult CreateCheckout()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                Mode = "subscription",

                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Price = "price_1TYZ3uLDHqv8ExiHA4rjYDSB",
                        Quantity = 1
                    }
                },

                SuccessUrl = "http://localhost:5173/success",
                CancelUrl = "http://localhost:5173/cancel",

                Metadata = new Dictionary<string, string>
                {
                    { "userId", userId }
                }
            };

            var service = new SessionService();
            Session session = service.Create(options);

            return Ok(new { url = session.Url });
        }


        [HttpPost("cancel-subscription")]
        public async Task<IActionResult> CancelSubscription()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userId, out int parsedUserId))
                return BadRequest();

            var usuario = _context.Users.FirstOrDefault(x => x.Id == parsedUserId);

            if (usuario == null)
                return NotFound();

            if (string.IsNullOrEmpty(usuario.StripeSubscriptionId))
                return BadRequest("Usuário não possui assinatura.");

            var service = new Stripe.SubscriptionService();

            await service.UpdateAsync(
                usuario.StripeSubscriptionId,
                new Stripe.SubscriptionUpdateOptions
                {
                    CancelAtPeriodEnd = true
                });


            return Ok(new
            {
                message = "Assinatura será cancelada ao final do período."
            });
        }
    }
}