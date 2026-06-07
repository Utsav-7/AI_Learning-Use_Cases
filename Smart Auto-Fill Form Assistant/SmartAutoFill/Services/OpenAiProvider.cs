using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SmartAutoFill.Services;

/// <summary>GPT (OpenAI) provider via the Chat Completions REST API.</summary>
public class OpenAiProvider : ILlmProvider
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly ILogger<OpenAiProvider> _logger;

    public string Name => "GPT (OpenAI)";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public OpenAiProvider(IHttpClientFactory factory, IConfiguration config, ILogger<OpenAiProvider> logger)
    {
        _http = factory.CreateClient("openai");
        _apiKey = config["OpenAI:ApiKey"];
        _model = config["OpenAI:Model"] ?? "gpt-4o";
        _logger = logger;
    }

    public async Task<string?> ClassifyAndExtractAsync(string maskedText, string instruction, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("OpenAI provider has no API key configured (OpenAI:ApiKey); skipping.");
            return null;
        }
        if (string.IsNullOrWhiteSpace(maskedText)) return null;

        var userPrompt =
            instruction + "\n\n" +
            "Keep any {{TYPE_n}} placeholders verbatim. Return ONLY JSON.\n\n" +
            "Document text:\n" + maskedText;

        var body = new
        {
            model = _model,
            temperature = 0.1,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = "You convert documents into structured JSON. Output valid JSON only." },
                new { role = "user", content = userPrompt }
            }
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
            {
                Content = JsonContent.Create(body)
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            using var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return string.IsNullOrWhiteSpace(content) ? null : content;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI extraction failed; skipping LLM step.");
            return null;
        }
    }
}
