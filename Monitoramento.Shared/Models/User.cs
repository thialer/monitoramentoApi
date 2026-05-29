namespace Monitoramento.Shared.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Nome { get; set; }
        public string Email { get; set; }
        public string SenhaHash { get; set; }
        public string Plano { get; set; } // free / pro
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string? StripeSubscriptionId { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string? StripeCustomerId { get; set; }
    }
}