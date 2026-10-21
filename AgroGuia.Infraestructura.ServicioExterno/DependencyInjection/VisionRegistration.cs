using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using AgroGuia.Infraestructura.ServicioExterno.Vision;
using AgroGuia.Infraestructura.ServicioExterno.Vision.Clasificacion;
using AgroGuia.Infraestructura.ServicioExterno.Vision.Lesiones;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgroGuia.Infraestructura.ServicioExterno.DependencyInjection;

public static class VisionRegistration
{
    /// <summary>
    /// Registra el módulo de visión (OpenCV). Todo es singleton: sin estado por petición y el modelo
    /// ONNX pesa/tarda en cargar, así que se mantiene en memoria durante toda la vida de la API.
    /// </summary>
    public static IServiceCollection AddVisionOpenCv(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<VisionOptions>(configuration.GetSection("Vision"));

        services.AddSingleton<IClasificadorImagen, ClasificadorOnnxOpenCv>();
        services.AddSingleton<IAnalizadorLesiones, AnalizadorLesionesHsv>();
        services.AddSingleton<IAnalizadorVision, AnalizadorVisionOpenCv>();

        return services;
    }
}