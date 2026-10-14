namespace AgroGuia.Infraestructura.ServicioExterno.RAG
{
    public sealed class RagOptions
    {
        public int TopK { get; set; } = 3;
        /// <summary>Coseno mínimo (nomic con prefijos). Calibrar mirando los logs de "mejor score".</summary>
        public float MinScore { get; set; } = 0.50f;
        /// <summary>Chunks vecinos (misma sección) que se añaden a cada acierto.</summary>
        public int VecinosContexto { get; set; } = 1;
        public int CandidatosPorRanking { get; set; } = 30;
        public int RrfK { get; set; } = 60;
        public double BoostCultivo { get; set; } = 1.25;
        public int RangoKeywordRelevante { get; set; } = 5;
        public int ContextoMaxCaracteres { get; set; } = 7000;
    }
}
