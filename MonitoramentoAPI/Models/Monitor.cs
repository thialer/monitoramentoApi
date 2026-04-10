public class Monitor
{
    public int Id { get; set; }
    public int UserId { get; set; }

    public string Nome { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Tipo { get; set; } = "HTTP";

    public int Intervalo { get; set; } = 10; // minutos
    public int Timeout { get; set; } = 10;

    public bool Ativo { get; set; } = true;
}