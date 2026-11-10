using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using AgroGuia.Aplicacion.DTO.DTOS.Diagnostico;

namespace AgroGuia.Aplicacion.Servicio;

public interface IAlmacenFotosDiagnostico
{
    Task<FotoDiagnosticoGuardadaDto> GuardarAsync(
        long usuarioId, byte[] imagen, CancellationToken ct = default);

    Task<byte[]?> LeerAsync(string rutaRelativa, CancellationToken ct = default);
}