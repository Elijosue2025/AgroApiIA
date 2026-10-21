namespace AgroGuia.Infraestructura.ServicioExterno.Models
{
    /// <summary>Una clase predicha por el modelo, con su probabilidad (0 a 1).</summary>
    public sealed record DeteccionVision(int Indice, EtiquetaVision Etiqueta, double Confianza);

    /// <summary>
    /// Resultado del análisis de color (HSV) de la hoja. No necesita modelo entrenado:
    /// estima qué porción del tejido vegetal visible está amarilla/marrón.
    /// </summary>
    public sealed record ResultadoLesiones(
        bool TejidoVegetalDetectado,
        double PorcentajeAfectado,
        int NumeroManchas,
        string Severidad);

    /// <summary>Estado del módulo de visión (útil para diagnosticar la configuración).</summary>
    public sealed record EstadoVision(
        bool ModeloDisponible,
        string? Modelo,
        int NumeroEtiquetas,
        string? Detalle);

    public sealed record ResultadoVision(
        bool Exito,
        string ErrorMensaje,
        IReadOnlyList<DeteccionVision> Detecciones,
        ResultadoLesiones? Lesiones,
        bool ModeloDisponible,
        int Ancho,
        int Alto,
        long TiempoMs)
    {
        public static ResultadoVision Fallo(string mensaje) =>
            new(false, mensaje, Array.Empty<DeteccionVision>(), null, false, 0, 0, 0);
    }
}
