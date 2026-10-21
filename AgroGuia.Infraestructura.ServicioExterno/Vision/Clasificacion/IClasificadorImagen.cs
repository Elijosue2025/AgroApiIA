using AgroGuia.Infraestructura.ServicioExterno.Models;
using OpenCvSharp;

namespace AgroGuia.Infraestructura.ServicioExterno.Vision.Clasificacion;

/// <summary>
/// Punto de extensión del reconocimiento. Hoy: clasificador ONNX vía OpenCV DNN. Para detectar plagas
/// (insectos) con cajas delimitadoras se puede añadir otra implementación (p. ej. YOLO) sin tocar
/// el orquestador ni las capas superiores.
/// </summary>
public interface IClasificadorImagen
{
    /// <summary>true si el modelo y las etiquetas se cargaron correctamente.</summary>
    bool Disponible { get; }

    EstadoVision Estado { get; }

    /// <summary>Devuelve las <paramref name="topK"/> clases más probables, de mayor a menor confianza.</summary>
    IReadOnlyList<DeteccionVision> Clasificar(Mat imagenBgr, int topK);
}
