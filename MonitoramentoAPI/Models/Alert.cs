namespace MonitoramentoAPI.Models
{
    public class Alert
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        public string Tipo { get; set; } // "email" ou "whatsapp"
        public string Destino { get; set; } // email ou número

        public bool Ativo { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; }
    }
}