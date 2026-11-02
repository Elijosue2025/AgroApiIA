using AgroGuia.Aplicacion.DTO.DTOS.Diagnostico;

namespace AgroGuia.Aplicacion.Servicio;

public interface IDiagnosticoServicio
{
    Task<DiagnosticoImagenResponseDto> AnalizarImagenAsync(
        byte[] imagen,
        string? cultivo = null,
        long usuarioId = 0,
        long? conversacionId = null,
        string? consulta = null,
        CancellationToken ct = default);

    DiagnosticoEstadoDto ObtenerEstado();

    Task<IReadOnlyList<VisionIncidenciaDto>> ListarIncidenciasMiasAsync(
        long usuarioId, int take = 50, CancellationToken ct = default);

    Task<VisionIncidenciaDto?> ObtenerIncidenciaMiaAsync(
        long usuarioId, long id, CancellationToken ct = default);
}