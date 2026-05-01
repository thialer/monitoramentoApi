namespace Monitoramento.Shared.Models
{
    public class Log
    {
        public int Id { get; set; }
        public int ApiMonitorId { get; set; }
        public int? StatusCode { get; set; }
        public int ResponseTimeMs { get; set; }
        public bool IsUp { get; set; }
        public string? Erro { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ApiMonitor ApiMonitor { get; set; }
    }
}
