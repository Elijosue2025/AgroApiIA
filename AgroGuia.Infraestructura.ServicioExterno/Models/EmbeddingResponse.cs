using System;

namespace AgroGuia.Infraestructura.ServicioExterno.Models
{
    public sealed class EmbeddingResponse
    {
        public bool Exito { get; init; }
        public float[] Vector { get; init; } = Array.Empty<float>();
        public string ErrorMensaje { get; init; } = string.Empty;

        public static EmbeddingResponse Ok(float[] vector) => new() { Exito = true, Vector = vector };
        public static EmbeddingResponse Fallo(string mensaje) => new() { Exito = false, ErrorMensaje = mensaje };
    }
}
