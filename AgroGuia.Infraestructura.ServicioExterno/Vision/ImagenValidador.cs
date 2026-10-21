namespace AgroGuia.Infraestructura.ServicioExterno.Vision;

/// <summary>
/// Validación barata ANTES de decodificar: tamaño y firma del archivo. No se confía en la extensión
/// ni en el Content-Type que declara el cliente.
/// </summary>
public static class ImagenValidador
{
    /// <summary>Devuelve null si la imagen es aceptable; si no, el mensaje de error para el usuario.</summary>
    public static string? Validar(byte[]? datos, long maxBytes)
    {
        if (datos is null || datos.Length == 0)
            return "La imagen está vacía.";

        if (datos.Length > maxBytes)
            return $"La imagen supera el máximo permitido ({maxBytes / (1024 * 1024)} MB).";

        if (!TieneFirmaSoportada(datos))
            return "Formato de imagen no soportado. Usa JPG, PNG, WebP o BMP.";

        return null;
    }

    private static bool TieneFirmaSoportada(byte[] d)
    {
        // JPEG: FF D8 FF
        if (d.Length >= 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF) return true;

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (d.Length >= 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47
            && d[4] == 0x0D && d[5] == 0x0A && d[6] == 0x1A && d[7] == 0x0A) return true;

        // WebP: "RIFF" .... "WEBP"
        if (d.Length >= 12 && d[0] == 'R' && d[1] == 'I' && d[2] == 'F' && d[3] == 'F'
            && d[8] == 'W' && d[9] == 'E' && d[10] == 'B' && d[11] == 'P') return true;

        // BMP: "BM"
        if (d.Length >= 2 && d[0] == 'B' && d[1] == 'M') return true;

        return false;
    }
}
