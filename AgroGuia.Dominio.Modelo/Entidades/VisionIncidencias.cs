using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroGuia.Dominio.Modelo.Entidades;

/// <summary>
/// Registro de un análisis visual (foto en chat o diagnóstico).
/// No contiene lógica de OpenCV ni de RAG: solo datos de negocio persistidos.
/// </summary>
public class VisionIncidencias
{
    public long Id { get; set; }

    public long UsuarioId { get; set; }

    public long? ConversacionId { get; set; }

    public string? Cultivo { get; set; }

    public string? RutaImagen { get; set; }

    public string? HashImagen { get; set; }

    public string? ClavePredicha { get; set; }

    public string? NombrePredicho { get; set; }

    public double? Confianza { get; set; }

    /// <summary>Identificada | Incierta | SinModelo | SinDeteccion | Error</summary>
    public string Estado { get; set; } = "Incierta";

    public string? ModeloVersion { get; set; }

    public string? ResumenVision { get; set; }

    public string? RespuestaAsistente { get; set; }

    /// <summary>JSON serializado de fuentes (lista de strings).</summary>
    public string? FuentesJson { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}