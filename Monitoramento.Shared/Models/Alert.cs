namespace Monitoramento.Shared.Models
{
    public class Alert
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        public string Tipo { get; set; } = "email";
        public string Destino { get; set; } 

        public bool Ativo { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; }
    }
}