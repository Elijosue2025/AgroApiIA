namespace AgroGuia.Infraestructura.ServicioExterno.Vision;

/// <summary>
/// Sección "Vision" de appsettings.json. Las rutas relativas se resuelven contra la carpeta de la
/// aplicación (AppContext.BaseDirectory); también se aceptan rutas absolutas.
/// </summary>
public sealed class VisionOptions
{
    public string RutaModeloOnnx { get; set; } = "ModelosVision/plagas_enfermedades.onnx";
    public string RutaEtiquetas { get; set; } = "ModelosVision/etiquetas.json";

    // ---- Preprocesado: debe replicar el del entrenamiento ----
    public int AnchoEntrada { get; set; } = 224;
    public int AltoEntrada { get; set; } = 224;

    /// <summary>Factor aplicado a cada píxel (0-255). 1/255 deja los valores en [0,1].</summary>
    public double Escala { get; set; } = 1.0 / 255.0;

    /// <summary>Media restada por canal, en escala 0-255 y orden RGB (antes de aplicar Escala).</summary>
    public double MediaR { get; set; }
    public double MediaG { get; set; }
    public double MediaB { get; set; }

    /// <summary>OpenCV carga BGR; casi todos los modelos esperan RGB.</summary>
    public bool IntercambiarRB { get; set; } = true;

    // ---- Resultados ----
    public int TopK { get; set; } = 3;

    /// <summary>Probabilidad mínima para reportar una clase (0 a 1).</summary>
    public double ConfianzaMinima { get; set; } = 0.35;

    // ---- Entrada ----
    public long MaxBytesImagen { get; set; } = 8 * 1024 * 1024;

    /// <summary>Lado mayor máximo en píxeles; fotos más grandes se reducen antes de analizarlas.</summary>
    public int MaxLadoPx { get; set; } = 1280;

    // ---- Integración con el RAG ----
    /// <summary>Buscar en los manuales cómo manejar lo detectado.</summary>
    public bool IncluirRecomendacionesRag { get; set; } = true;
    public int MaxRecomendaciones { get; set; } = 2;
}
