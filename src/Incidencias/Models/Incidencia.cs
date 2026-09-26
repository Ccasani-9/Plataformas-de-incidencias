using System.ComponentModel.DataAnnotations;

namespace Incidencias.Models;

public static class EstadosIncidencia
{
    public const string Abierta = "Abierta";
    public const string Cerrada = "Cerrada";
}

public class Incidencia
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Estación")]
    public string Estacion { get; set; } = string.Empty;

    [Required, StringLength(500)]
    [Display(Name = "Descripción")]
    public string Descripcion { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Prioridad { get; set; } = "Media";

    [Required, StringLength(20)]
    public string Estado { get; set; } = EstadosIncidencia.Abierta;
}
