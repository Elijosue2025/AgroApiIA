namespace AgroGuia.Infraestructura.ServicioExterno.Models
{
    /// <summary>
    /// Una clase del modelo de visión. En etiquetas.json la POSICIÓN en el arreglo es el índice de
    /// la clase en la salida del modelo, así que el orden debe coincidir con el del entrenamiento.
    /// </summary>
    public sealed class EtiquetaVision
    {
        /// <summary>Nombre original de la clase en el dataset (ej. "Potato___Late_blight").</summary>
        public string Clave { get; init; } = string.Empty;

        /// <summary>Nombre para mostrar al agricultor (ej. "Tizón tardío").</summary>
        public string Nombre { get; init; } = string.Empty;

        /// <summary>Cultivo al que aplica (ej. "Papa"). Null si la clase no es específica de un cultivo.</summary>
        public string? Cultivo { get; init; }

        public TipoHallazgo Tipo { get; init; } = TipoHallazgo.Desconocido;

        public string? Descripcion { get; init; }
    }
}
