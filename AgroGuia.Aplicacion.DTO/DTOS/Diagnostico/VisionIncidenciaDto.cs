using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace AgroGuia.Aplicacion.DTO.DTOS.Diagnostico;

public class VisionIncidenciaDto
{
    public long Id { get; set; }
    public long? ConversacionId { get; set; }
    public string? Cultivo { get; set; }
    public string? RutaImagen { get; set; }
    public string? ClavePredicha { get; set; }
    public string? NombrePredicho { get; set; }
    public double? Confianza { get; set; }
    public string Estado { get; set; } = "";
    public string? ModeloVersion { get; set; }
    public string? ResumenVision { get; set; }
    public string? RespuestaAsistente { get; set; }
    public List<string> Fuentes { get; set; } = new();
    public DateTime FechaCreacion { get; set; }
}