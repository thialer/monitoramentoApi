namespace MonitoramentoAPI.Models.DTOs
{
    public class UpdateMonitorRequest
    {
        public string Nome { get; set; }
        public string Url { get; set; }
        public int Intervalo { get; set; }
    }
}