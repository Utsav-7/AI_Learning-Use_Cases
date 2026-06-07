using System.Net.Http.Json;
using System.Text.Json;

namespace SmartAutoFill.Services;

/// <summary>Local Ollama provider via the /api/chat endpoint (JSON mode).</summary>
public class OllamaProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly string _chatModel;
    private readonly ILogger<OllamaProvider> _logger;

    public string Name => "Ollama (local)";
    public bool IsConfigured => true; // local; assumed available

    public OllamaProvider(IHttpClientFactory factory, IConfiguration config, ILogger<OllamaProvider> logger)
    {
        _http = factory.CreateClient("ollama");
        _chatModel = config["Ollama:ChatModel"] ?? "llama3.1:8b";
        _logger = logger;
    }

    public async Task<string?> ClassifyAndExtractAsync(string maskedText, string instruction, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(maskedText)) return null;

        var prompt =
            instruction + "\n\n" +
            "Keep any {{TYPE_n}} placeholders verbatim. Return ONLY JSON.\n\n" +
            "Document text:\n" + maskedText;

        var body = new
        {
            model = _chatModel,
            format = "json",
            stream = false,
            keep_alive = "10m",
            options = new { temperature = 0.1, num_predict = 4096, num_ctx = 8192 },
            messages = new[]
            {
                new { role = "system", content = "You convert documents into structured JSON. Output valid JSON only." },
                new { role = "user", content = prompt }
            }
        };

        try
        {
            using var resp = await _http.PostAsJsonAsync("/api/chat", body, ct);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            return doc.RootElement.GetProperty("message").GetProperty("content").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama extraction unavailable; skipping LLM step.");
            return null;
        }
    }
}
