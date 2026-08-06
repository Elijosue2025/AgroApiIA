using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroGuia.Infraestructura.ServicioExterno.RAG
{
    public class SimilarityService
    {
        /// <summary>
        /// Calcula la similitud coseno entre dos vectores de embedding.
        /// Retorna un valor entre 0.0 (sin similitud) y 1.0 (idénticos).
        /// </summary>
        public double CalcularCosineSimilarity(
            List<float> vectorA,
            List<float> vectorB)
        {
            if (vectorA.Count != vectorB.Count)
            {
                Console.WriteLine($"⚠️ Dimensiones distintas: {vectorA.Count} vs {vectorB.Count}");
                return 0;
            }

            double productoPunto = 0;
            double magnitudA = 0;
            double magnitudB = 0;

            for (int i = 0; i < vectorA.Count; i++)
            {
                productoPunto += vectorA[i] * vectorB[i];
                magnitudA += vectorA[i] * vectorA[i];
                magnitudB += vectorB[i] * vectorB[i];
            }

            magnitudA = Math.Sqrt(magnitudA);
            magnitudB = Math.Sqrt(magnitudB);

            if (magnitudA == 0 || magnitudB == 0)
                return 0;

            return productoPunto / (magnitudA * magnitudB);
        }
    }
}