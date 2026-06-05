using System.Net.Http.Json;
using System.Text.Json;

namespace SmartAutoFill.Services;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly ILogger<OllamaEmbeddingService> _logger;

    public OllamaEmbeddingService(IHttpClientFactory factory, IConfiguration config, ILogger<OllamaEmbeddingService> logger)
    {
        _http = factory.CreateClient("ollama");
        _model = config["Ollama:EmbeddingModel"] ?? "nomic-embed-text";
        _logger = logger;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<float>();

        try
        {
            var body = new { model = _model, prompt = text, keep_alive = "10m" };
            using var resp = await _http.PostAsJsonAsync("/api/embeddings", body, ct);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("embedding", out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
                return Array.Empty<float>();

            var vec = new float[arr.GetArrayLength()];
            var i = 0;
            foreach (var n in arr.EnumerateArray())
                vec[i++] = (float)n.GetDouble();
            return vec;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama embeddings unavailable; RAG disabled for this call.");
            return Array.Empty<float>();
        }
    }
}
