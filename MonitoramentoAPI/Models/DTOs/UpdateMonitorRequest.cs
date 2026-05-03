namespace Monitoramento.Shared.Models.DTOs
{
    public class UpdateApiMonitorRequest
    {

        public string Nome { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Tipo { get; set; } = "HTTP";
        public int Intervalo { get; set; }

        public string Metodo { get; set; } = "GET";
        public string? Headers { get; set; }
        public string? Body { get; set; }
    }
}