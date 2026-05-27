using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using Monitoramento.Shared.Data;

namespace ApiMonitoramentoAPI.Controllers
{
    [ApiController]
    [Route("webhook")]
    public class WebhookController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WebhookController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Handle()
        {
            var json = await new StreamReader(Request.Body).ReadToEndAsync();

            var stripeEvent = EventUtility.ParseEvent(json);

            Console.WriteLine($"EVENTO: {stripeEvent.Type}");

            if (stripeEvent.Type == "checkout.session.completed")
            {
                Console.WriteLine("Entrou no checkout.session.completed");


                var session = stripeEvent.Data.Object as Stripe.Checkout.Session;

                if (session == null)
                {
                    Console.WriteLine("SESSION NULL");
                    return Ok();
                }

                Console.WriteLine($"Session ID: {session.Id}");

                if (session.Metadata == null)
                {
                    Console.WriteLine("Metadata NULL");
                    return Ok();
                }

                if (!session.Metadata.ContainsKey("userId"))
                {
                    Console.WriteLine("userId não encontrado");
                    return Ok();
                }

                var userId = session.Metadata["userId"];
                Console.WriteLine($"userId recebido: {userId}");

                if (!int.TryParse(userId, out int parsedUserId))
                {
                    Console.WriteLine("ID inválido");
                    return Ok();
                }

                var user = await _context.Users.FindAsync(parsedUserId);

                if (user == null)
                {
                    Console.WriteLine("Usuário não encontrado");
                    return Ok();
                }

                Console.WriteLine($"Usuário encontrado: {user.Email}");

                Console.WriteLine($"Usuário encontrado: {user.Email}");

                user.Plano = "PRO";

                user.StripeSubscriptionId = session.SubscriptionId;

                user.StripeCustomerId = session.CustomerId;

                await _context.SaveChangesAsync();

                Console.WriteLine("PLANO ATUALIZADO PARA PRO");

                Console.WriteLine("PLANO ATUALIZADO PARA PRO");
            }

            if (stripeEvent.Type == "customer.subscription.deleted")
            {
                Console.WriteLine("Assinatura cancelada");

                var subscription = stripeEvent.Data.Object as Subscription;

                if (subscription == null)
                {
                    Console.WriteLine("Subscription NULL");
                    return Ok();
                }

                var subscriptionId = subscription.Id;

                var user = _context.Users
                    .FirstOrDefault(x => x.StripeSubscriptionId == subscriptionId);

                if (user == null)
                {
                    Console.WriteLine("Usuário não encontrado");
                    return Ok();
                }

                user.Plano = "FREE";

                await _context.SaveChangesAsync();

                Console.WriteLine("PLANO ALTERADO PARA FREE");
            }

            return Ok();
        }
    }
}