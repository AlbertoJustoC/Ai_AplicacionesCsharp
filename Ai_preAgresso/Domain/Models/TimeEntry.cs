namespace Ai_preAgresso.Domain.Models;

public sealed class TimeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Fecha { get; set; }
    public string Proyecto { get; set; } = string.Empty;
    public string Actividad { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public double Horas { get; set; }
    public TipoJornada Tipo { get; set; } = TipoJornada.Normal;
}
