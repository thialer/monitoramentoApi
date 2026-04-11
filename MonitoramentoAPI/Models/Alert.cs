namespace MonitoramentoAPI.Models
{
    public class Alert
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Tipo { get; set; } // email / whatsapp
        public string Destino { get; set; }
        public bool Ativo { get; set; }

        public User User { get; set; }
    }
}
