using AgroGuia.Aplicacion.DTO.DTOS.Diagnostico;
using AgroGuia.Dominio.Modelo.Entidades;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroGuia.Aplicacion.Servicio
{
    public interface IDiagnosticoServicio
    {
        /// <param name="imagen">Bytes de la foto (JPG/PNG/WebP/BMP).</param>
        /// <param name="cultivo">Cultivo indicado por el usuario (opcional); ayuda a afinar la búsqueda en los manuales.</param>
        Task<DiagnosticoImagenResponseDto> AnalizarImagenAsync(
            byte[] imagen, string? cultivo = null, CancellationToken ct = default);

        DiagnosticoEstadoDto ObtenerEstado();
        Task<IReadOnlyList<VisionIncidenciaDto>> ListarIncidenciasMiasAsync(long usuarioId, int take = 50, CancellationToken ct = default);
        Task<VisionIncidenciaDto?> ObtenerIncidenciaMiaAsync(long usuarioId, long id, CancellationToken ct = default);
        // Campos extra en el método: long usuarioId, long? conversacionId
        private async Task GuardarIncidenciaAsync(
            long usuarioId,
            long? conversacionId,
            DiagnosticoImagenResponseDto dto,
            string? modeloVersion,
            CancellationToken ct)
        {
            if (usuarioId <= 0) return;

            string? fuentesJson = null;
            if (dto.Fuentes is { Count: > 0 })
                fuentesJson = System.Text.Json.JsonSerializer.Serialize(dto.Fuentes);

            await _incidencias.CrearAsync(new VisionIncidencias
            {
                UsuarioId = usuarioId,
                ConversacionId = conversacionId is > 0 ? conversacionId : null,
                Cultivo = dto.Cultivo,
                ClavePredicha = dto.Detecciones.FirstOrDefault()?.Clave,
                NombrePredicho = dto.ClasePredicha,
                Confianza = dto.ConfianzaPrincipal,
                Estado = string.IsNullOrWhiteSpace(dto.Estado) ? "Incierta" : dto.Estado,
                ModeloVersion = modeloVersion,
                ResumenVision = dto.Resumen,
                RespuestaAsistente = dto.RespuestaAsistente,
                FuentesJson = fuentesJson
            }, ct);
        }
    }
}
