using AgroGuia.Infraestructura.ServicioExterno.Models;
using OpenCvSharp;

namespace AgroGuia.Infraestructura.ServicioExterno.Vision.Lesiones;

/// <summary>
/// Estimación del daño foliar por segmentación de color, SIN modelo entrenado (visión clásica):
///   1. Suaviza y pasa a HSV (en OpenCV: H 0-179, S 0-255, V 0-255).
///   2. Máscara "verde" = tejido sano; máscara "afectado" = amarillo/marrón (clorosis, necrosis).
///   3. Solo cuenta como afectado lo que está cerca de tejido verde, para no contar suelo o tallos secos lejanos.
///   4. % afectado = afectado / (verde + afectado); manchas = contornos externos de tamaño relevante.
///
/// LIMITACIONES (heurística, no diagnóstico): funciona mejor con una hoja de cerca, buena luz y fondo neutro.
/// Una hoja casi totalmente necrosada o con suelo marrón pegado al borde puede dar valores imprecisos.
/// Los rangos HSV son un punto de partida y se pueden calibrar con fotos reales de los cultivos.
/// </summary>
public sealed class AnalizadorLesionesHsv : IAnalizadorLesiones
{
    private static readonly Scalar VerdeMin = new(35, 35, 35);
    private static readonly Scalar VerdeMax = new(85, 255, 255);
    private static readonly Scalar AfectadoMin = new(8, 45, 35);    // marrón-naranja
    private static readonly Scalar AfectadoMax = new(34, 255, 255); // hasta amarillo

    /// <summary>Por debajo de esta fracción de la imagen no se considera que haya una hoja analizable.</summary>
    private const double TejidoMinimo = 0.02;

    /// <summary>Una mancha cuenta si ocupa al menos esta fracción de la imagen.</summary>
    private const double ManchaMinima = 0.0005;

    public ResultadoLesiones Analizar(Mat imagenBgr)
    {
        var totalPx = (double)imagenBgr.Width * imagenBgr.Height;

        using var suave = new Mat();
        Cv2.GaussianBlur(imagenBgr, suave, new Size(5, 5), 0);

        using var hsv = new Mat();
        Cv2.CvtColor(suave, hsv, ColorConversionCodes.BGR2HSV);

        using var verde = new Mat();
        Cv2.InRange(hsv, VerdeMin, VerdeMax, verde);

        using var afectadoBruto = new Mat();
        Cv2.InRange(hsv, AfectadoMin, AfectadoMax, afectadoBruto);

        // Zona de la hoja = verde dilatado (~2% del lado mayor, mínimo 5 px, siempre impar).
        var lado = Math.Max(imagenBgr.Width, imagenBgr.Height);
        var k = Math.Max(5, (int)(lado * 0.02)) | 1;

        using var nucleoHoja = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(k, k));
        using var zonaHoja = new Mat();
        Cv2.Dilate(verde, zonaHoja, nucleoHoja);

        using var afectadoEnHoja = new Mat();
        Cv2.BitwiseAnd(afectadoBruto, zonaHoja, afectadoEnHoja);

        // Quita ruido de píxeles sueltos.
        using var nucleoRuido = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(3, 3));
        using var afectado = new Mat();
        Cv2.MorphologyEx(afectadoEnHoja, afectado, MorphTypes.Open, nucleoRuido);

        double pxVerde = Cv2.CountNonZero(verde);
        double pxAfectado = Cv2.CountNonZero(afectado);
        var pxTejido = pxVerde + pxAfectado;

        if (totalPx <= 0 || pxTejido / totalPx < TejidoMinimo)
            return new ResultadoLesiones(false, 0, 0, "Indeterminada");

        var porcentaje = Math.Round(pxAfectado / pxTejido * 100.0, 1);

        Cv2.FindContours(afectado, out Point[][] contornos, out HierarchyIndex[] _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var areaMinima = totalPx * ManchaMinima;
        var manchas = contornos.Count(c => Cv2.ContourArea(c) >= areaMinima);

        return new ResultadoLesiones(true, porcentaje, manchas, ClasificarSeveridad(porcentaje));
    }

    private static string ClasificarSeveridad(double porcentaje) =>
        porcentaje < 5 ? "Baja" :
        porcentaje < 20 ? "Moderada" :
        "Alta";
}
