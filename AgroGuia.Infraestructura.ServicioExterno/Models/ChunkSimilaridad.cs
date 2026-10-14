namespace AgroGuia.Infraestructura.ServicioExterno.Models
{
    public sealed class ChunkSimilaridad
    {
        public long Id { get; init; }
        public string Archivo { get; init; } = string.Empty;
        public int Indice { get; init; }
        public string Titulo { get; init; } = string.Empty;
        public string? Seccion { get; init; }
        public int? Pagina { get; init; }
        public string Contenido { get; init; } = string.Empty;
        public string Cultivo { get; init; } = "General";
        public string Tema { get; init; } = "General";
        /// <summary>Coseno real (0 si no hubo embedding de la pregunta).</summary>
        public double Score { get; init; }
        /// <summary>Puntaje de fusión RRF (vector + palabras clave).</summary>
        public double ScoreFusion { get; init; }
    }
}
