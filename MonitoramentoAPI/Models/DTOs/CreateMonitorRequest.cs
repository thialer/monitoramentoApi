namespace MonitoramentoAPI.Models.DTOs
{
    public class CreateMonitorRequest
    {
        public string Nome { get; set; }
        public string Url { get; set; }
        public string Tipo { get; set; }
        public int Intervalo { get; set; }
    }
}
