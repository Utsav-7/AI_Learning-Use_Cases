namespace SmartAutoFill.Services;

public interface IEmbeddingService
{
    /// <summary>Embed text with the local Ollama embedding model. Empty array on failure.</summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);

    static byte[] ToBytes(float[] v)
    {
        var bytes = new byte[v.Length * sizeof(float)];
        Buffer.BlockCopy(v, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    static float[] FromBytes(byte[] b)
    {
        if (b.Length == 0) return Array.Empty<float>();
        var v = new float[b.Length / sizeof(float)];
        Buffer.BlockCopy(b, 0, v, 0, b.Length);
        return v;
    }

    static double Cosine(float[] a, float[] b)
    {
        if (a.Length == 0 || a.Length != b.Length) return 0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb) + 1e-9);
    }
}
