namespace AgroGuia.Infraestructura.ServicioExterno.DocumentLoader
{
    public sealed class ChunkingOptions
    {
        public int MaxChars { get; set; } = 900;
        public int MinChars { get; set; } = 150;
        /// <summary>Párrafos repetidos entre chunks contiguos. 0 está bien porque la recuperación expande vecinos.</summary>
        public int OverlapParrafos { get; set; } = 0;
        /// <summary>
        /// Letras / caracteres no-espacio de un CHUNK. Debajo de esto se descarta (basura). Se bajó de 0.60 a 0.40
        /// porque las tablas de dosis ("N 120 kg/ha, P2O5 80...") tienen muchos números y con 0.60 se perdían.
        /// </summary>
        public double RatioLetrasMinimo { get; set; } = 0.40;
        /// <summary>
        /// Letras / caracteres no-espacio de una LÍNEA/párrafo suelto. Debajo de esto es ruido puro
        /// (símbolos de dibujos, "· ~ ^ 1 ; 2 |") y se descarta aunque el chunk completo sea bueno.
        /// </summary>
        public double RatioLetrasUnidadMinimo { get; set; } = 0.20;
    }
}
