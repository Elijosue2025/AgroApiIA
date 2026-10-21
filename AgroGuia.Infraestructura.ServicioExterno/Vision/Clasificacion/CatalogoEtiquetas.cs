using System.Text.Json;
using System.Text.Json.Serialization;
using AgroGuia.Infraestructura.ServicioExterno.Models;

namespace AgroGuia.Infraestructura.ServicioExterno.Vision.Clasificacion;

/// <summary>Lee etiquetas.json: arreglo de <see cref="EtiquetaVision"/> cuyo orden es el de las clases del modelo.</summary>
public static class CatalogoEtiquetas
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static IReadOnlyList<EtiquetaVision> Cargar(string ruta)
    {
        using var stream = File.OpenRead(ruta);
        var etiquetas = JsonSerializer.Deserialize<List<EtiquetaVision>>(stream, Json);

        if (etiquetas is null || etiquetas.Count == 0)
            throw new InvalidDataException($"'{Path.GetFileName(ruta)}' no contiene etiquetas.");

        var sinNombre = etiquetas.FindIndex(e => string.IsNullOrWhiteSpace(e.Nombre));
        if (sinNombre >= 0)
            throw new InvalidDataException(
                $"'{Path.GetFileName(ruta)}': la etiqueta en la posición {sinNombre} no tiene 'nombre'.");

        return etiquetas;
    }
}
