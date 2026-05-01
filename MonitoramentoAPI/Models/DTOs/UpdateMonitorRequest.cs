namespace Monitoramento.Shared.Models.DTOs
{
    public class UpdateApiMonitorRequest
    {
        public string Nome { get; set; }
        public string Url { get; set; }
        public int Intervalo { get; set; }
    }
}