using AgroGuia.Aplicacion.DTO.DTOS.Diagnostico;
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
    }
}
