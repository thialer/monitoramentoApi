namespace Monitoramento.Shared.Models
{
    public class ApiMonitor
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Nome { get; set; }
        public string Url { get; set; }
        public string Tipo { get; set; }
        public int Intervalo { get; set; }
        public string StatusAtual { get; set; }
        public DateTime? LastCheckedAt { get; set; }
        public bool Ativo { get; set; }
        public string Metodo { get; set; } = "GET";
        public string? Headers { get; set; }
        public string? Body { get; set; }
        public User User { get; set; }
    }
}