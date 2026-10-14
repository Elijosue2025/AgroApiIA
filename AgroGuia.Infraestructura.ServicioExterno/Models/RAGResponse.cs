using System.Collections.Generic;

namespace AgroGuia.Infraestructura.ServicioExterno.Models
{
    public sealed class RAGResponse
    {
        public bool Exito { get; set; }
        public string Pregunta { get; set; } = string.Empty;
        public string Respuesta { get; set; } = string.Empty;
        public List<string> ChunksUtilizados { get; set; } = new();
        public List<FuenteCitada> Fuentes { get; set; } = new();
        /// <summary>true cuando no hubo contexto relevante y NO se llamó al LLM.</summary>
        public bool SinContexto { get; set; }
        /// <summary>true cuando el LLM falló y se respondió con extractos de los manuales.</summary>
        public bool ModoFallback { get; set; }
        public int TokensEntrada { get; set; }
        public int TokensSalida { get; set; }
        public string ErrorMensaje { get; set; } = string.Empty;
    }
}
