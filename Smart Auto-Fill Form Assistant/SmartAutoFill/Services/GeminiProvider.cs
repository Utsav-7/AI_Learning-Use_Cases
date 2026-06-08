using System.Net.Http.Json;
using System.Text.Json;

namespace SmartAutoFill.Services;

/// <summary>Google Gemini provider via the Generative Language REST API (API-key auth).</summary>
public class GeminiProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly ILogger<GeminiProvider> _logger;

    public string Name => "Gemini (Google)";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public GeminiProvider(IHttpClientFactory factory, IConfiguration config, ILogger<GeminiProvider> logger)
    {
        _http = factory.CreateClient("gemini");
        _apiKey = config["Gemini:ApiKey"];
        _model = config["Gemini:Model"] ?? "gemini-2.5-flash";
        _logger = logger;
    }

    public async Task<LlmResult> ClassifyAndExtractAsync(string maskedText, string instruction, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Gemini provider has no API key configured (Gemini:ApiKey); skipping.");
            return new LlmResult(null, "Gemini API key not configured (Gemini:ApiKey).");
        }
        if (string.IsNullOrWhiteSpace(maskedText)) return new LlmResult(null);

        var userPrompt =
            instruction + "\n\n" +
            "Keep any {{TYPE_n}} placeholders verbatim. Return ONLY JSON.\n\n" +
            "Document text:\n" + maskedText;

        var body = new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                    new { text = "You are an information-extraction assistant. Read and understand the document, reason about what each piece of text means, and extract clean, normalised values into the requested JSON structure. Interpret — do not copy raw text verbatim. Output valid JSON only." }
                }
            },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = userPrompt } } }
            },
            generationConfig = new
            {
                temperature = 0.1,
                responseMimeType = "application/json",
                maxOutputTokens = 4096
            }
        };

        try
        {
             using var req = new HttpRequestMessage(HttpMethod.Post,
                $"/v1beta/models/{_model}:generateContent")
            {
                Content = JsonContent.Create(body)
            };
            req.Headers.Add("x-goog-api-key", _apiKey);

            using var resp = await _http.SendAsync(req, ct);
            var payload = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                var detail = ParseError(payload) ?? $"HTTP {(int)resp.StatusCode}";
                _logger.LogWarning("Gemini returned {Status}: {Detail}", (int)resp.StatusCode, detail);
                var friendly = (int)resp.StatusCode switch
                {
                    503 => "Gemini is busy right now (503). Please try again in a moment.",
                    429 => "Gemini rate limit reached (429). Wait a bit and retry.",
                    400 or 404 => $"Gemini request rejected: {detail}",
                    401 or 403 => "Gemini API key invalid or unauthorized.",
                    _ => $"Gemini error ({(int)resp.StatusCode}): {detail}"
                };
                return new LlmResult(null, friendly);
            }

            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
                candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
            {
                _logger.LogWarning("Gemini returned no candidates (possibly blocked).");
                return new LlmResult(null, "Gemini returned no result (the content may have been blocked).");
            }

            var text = string.Concat(candidates[0]
                .GetProperty("content")
                .GetProperty("parts")
                .EnumerateArray()
                .Select(p => p.TryGetProperty("text", out var t) ? t.GetString() : null));

            return string.IsNullOrWhiteSpace(text) ? new LlmResult(null, "Gemini returned an empty response.") : new LlmResult(text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini extraction failed; skipping LLM step.");
            return new LlmResult(null, $"Gemini unavailable: {ex.Message}");
        }
    }

    private static string? ParseError(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("error", out var err) &&
                err.TryGetProperty("message", out var m))
                return m.GetString();
        }
        catch { /* non-JSON body */ }
        return null;
    }
}
