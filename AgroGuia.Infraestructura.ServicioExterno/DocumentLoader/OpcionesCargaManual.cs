namespace AgroGuia.Infraestructura.ServicioExterno.DocumentLoader;

/// <summary>Opciones de una carga. Vacío = detección automática y chunking de appsettings.</summary>
public sealed class OpcionesCargaManual
{
    public string? Cultivo { get; init; }
    public string? Tema { get; init; }
    public int? MaxChars { get; init; }
    public int? MinChars { get; init; }
    /// <summary>True: solo copia el PDF a la carpeta, como el alojamiento local, sin embeddings.</summary>
    public bool SoloGuardar { get; init; }
}
