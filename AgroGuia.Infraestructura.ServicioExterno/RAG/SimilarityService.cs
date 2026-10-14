using System.Numerics;

namespace AgroGuia.Infraestructura.ServicioExterno.RAG;

/// <summary>
/// MEJORAS: se normaliza una sola vez (al cargar el índice) y luego el coseno es solo
/// producto punto, vectorizado con SIMD. Es stateless → singleton.
/// </summary>
public sealed class SimilarityService
{
    public float[] Normalizar(float[] v)
    {
        double suma = 0;
        foreach (var x in v) suma += (double)x * x;
        var norma = (float)Math.Sqrt(suma);
        var r = new float[v.Length];
        if (norma == 0) { Array.Copy(v, r, v.Length); return r; }
        for (int i = 0; i < v.Length; i++) r[i] = v[i] / norma;
        return r;
    }

    /// <summary>Con vectores ya normalizados, esto ES el coseno.</summary>
    public float ProductoPunto(float[] a, float[] b)
    {
        int n = Math.Min(a.Length, b.Length), i = 0;
        var acc = Vector<float>.Zero;
        int w = Vector<float>.Count;
        for (; i <= n - w; i += w) acc += new Vector<float>(a, i) * new Vector<float>(b, i);
        float suma = Vector.Sum(acc);
        for (; i < n; i++) suma += a[i] * b[i];
        return suma;
    }

    public double CalcularCosineSimilarity(float[] a, float[] b) =>
        a.Length != b.Length ? 0 : ProductoPunto(Normalizar(a), Normalizar(b));
}
