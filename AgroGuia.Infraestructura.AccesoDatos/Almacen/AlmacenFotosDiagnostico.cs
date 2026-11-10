using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Security.Cryptography;
using AgroGuia.Aplicacion.DTO.DTOS.Diagnostico;
using AgroGuia.Aplicacion.Servicio;

namespace AgroGuia.Infraestructura.AccesoDatos.Almacen;

public sealed class AlmacenFotosDiagnostico : IAlmacenFotosDiagnostico
{
    private readonly string _raiz;

    public AlmacenFotosDiagnostico(string raizFisica)
    {
        _raiz = Path.GetFullPath(raizFisica);
    }

    public async Task<FotoDiagnosticoGuardadaDto> GuardarAsync(
        long usuarioId, byte[] imagen, CancellationToken ct = default)
    {
        if (usuarioId <= 0)
            throw new ArgumentException("Usuario inválido.", nameof(usuarioId));
        if (imagen is not { Length: > 0 })
            throw new ArgumentException("La imagen está vacía.", nameof(imagen));

        var hash = Convert.ToHexString(SHA256.HashData(imagen)).ToLowerInvariant();
        var nombre = hash + ".jpg";
        var dir = Path.Combine(_raiz, usuarioId.ToString());
        Directory.CreateDirectory(dir);

        var abs = Path.Combine(dir, nombre);
        if (!File.Exists(abs))
            await File.WriteAllBytesAsync(abs, imagen, ct);

        return new FotoDiagnosticoGuardadaDto
        {
            RutaRelativa = $"diagnosticos/{usuarioId}/{nombre}",
            Hash = hash,
            NombreArchivo = nombre
        };
    }

    public async Task<byte[]?> LeerAsync(string rutaRelativa, CancellationToken ct = default)
    {
        var abs = Resolver(rutaRelativa);
        if (abs is null || !File.Exists(abs)) return null;
        return await File.ReadAllBytesAsync(abs, ct);
    }

    private string? Resolver(string rutaRelativa)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa)) return null;

        var rel = rutaRelativa.Replace('\\', '/').TrimStart('/');
        const string prefijo = "diagnosticos/";
        if (!rel.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase)) return null;

        var abs = Path.GetFullPath(Path.Combine(_raiz, rel[prefijo.Length..]));
        var raiz = _raiz.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return abs.StartsWith(raiz, StringComparison.OrdinalIgnoreCase) ? abs : null;
    }
}