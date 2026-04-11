namespace MonitoramentoAPI.Models.DTOs
{
    public class CreateAlertRequest
    {
        public string Tipo { get; set; } // email ou whatsapp
        public string Destino { get; set; }
    }
}