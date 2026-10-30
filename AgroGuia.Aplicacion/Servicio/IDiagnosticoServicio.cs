using AgroGuia.Aplicacion.DTO.DTOS.Diagnostico;
using AgroGuia.Dominio.Modelo.Entidades;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace AgroGuia.Aplicacion.Servicio;

public interface IDiagnosticoServicio
{
    Task<DiagnosticoImagenResponseDto> AnalizarImagenAsync(
        byte[] imagen,
        string? cultivo = null,
        long usuarioId = 0,
        long? conversacionId = null,
        CancellationToken ct = default);

    DiagnosticoEstadoDto ObtenerEstado();

    Task<IReadOnlyList<VisionIncidenciaDto>> ListarIncidenciasMiasAsync(
        long usuarioId, int take = 50, CancellationToken ct = default);

    Task<VisionIncidenciaDto?> ObtenerIncidenciaMiaAsync(
        long usuarioId, long id, CancellationToken ct = default);
}