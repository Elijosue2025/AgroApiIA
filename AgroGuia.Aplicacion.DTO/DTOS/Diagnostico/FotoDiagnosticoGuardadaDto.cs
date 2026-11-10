using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroGuia.Aplicacion.DTO.DTOS.Diagnostico;

public class FotoDiagnosticoGuardadaDto
{
    public string RutaRelativa { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public string NombreArchivo { get; set; } = string.Empty;
}