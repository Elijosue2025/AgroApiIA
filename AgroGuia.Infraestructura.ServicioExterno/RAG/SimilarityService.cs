namespace AgroGuia.Infraestructura.ServicioExterno.RAG
{
    public class SimilarityService
    {
        public double CalcularCosineSimilarity(
            List<float> vectorA,
            List<float> vectorB)
        {
            if (vectorA.Count != vectorB.Count)
            {
                return 0;
            }

            double productoPunto = 0;
            double magnitudA = 0;
            double magnitudB = 0;

            for (int i = 0; i < vectorA.Count; i++)
            {
                productoPunto += vectorA[i] * vectorB[i];

                magnitudA += Math.Pow(vectorA[i], 2);

                magnitudB += Math.Pow(vectorB[i], 2);
            }

            magnitudA = Math.Sqrt(magnitudA);
            magnitudB = Math.Sqrt(magnitudB);

            if (magnitudA == 0 || magnitudB == 0)
            {
                return 0;
            }

            return productoPunto / (magnitudA * magnitudB);
        }
    }
}