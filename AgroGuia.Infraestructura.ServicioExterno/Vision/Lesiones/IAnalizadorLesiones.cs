using AgroGuia.Infraestructura.ServicioExterno.Models;
using OpenCvSharp;

namespace AgroGuia.Infraestructura.ServicioExterno.Vision.Lesiones;

public interface IAnalizadorLesiones
{
    ResultadoLesiones Analizar(Mat imagenBgr);
}
