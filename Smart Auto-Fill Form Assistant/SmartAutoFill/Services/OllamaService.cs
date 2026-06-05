using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using SmartAutoFill.Models;

namespace SmartAutoFill.Services;

public class OllamaService : IOllamaService
{
    private readonly HttpClient _http;
    private readonly string _chatModel;
    private readonly ILogger<OllamaService> _logger;

    public OllamaService(IHttpClientFactory factory, IConfiguration config, ILogger<OllamaService> logger)
    {
        _http = factory.CreateClient("ollama");
        _chatModel = config["Ollama:ChatModel"] ?? "llama3.1:8b";
        _logger = logger;
    }

    public async Task<Dictionary<string, string>> MapFieldsAsync(
        string maskedText, FormSchema schema, string examplesBlock, CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (schema.Fields.Count == 0 || string.IsNullOrWhiteSpace(maskedText))
            return result;

        var fieldList = string.Join(", ", schema.Fields.Select(f => f.Key));
        var prompt =
            "You map document text to predefined form fields. Return ONLY JSON.\n\n" +
            (string.IsNullOrWhiteSpace(examplesBlock) ? "" : examplesBlock + "\n\n") +
            $"Document text (PII is masked as {{{{TYPE_n}}}} — keep those tokens verbatim):\n{maskedText}\n\n" +
            $"Target fields: [{fieldList}]\n" +
            "Return exactly: { \"fields\": [ {\"key\":\"<one of the target fields>\",\"value\":\"<text>\",\"found\":true} ] }\n" +
            "Use \"found\": false and value null for any field not present. Do not invent values.\n" +
            "Keep each value concise (under 200 characters); summarize long sections instead of copying them.";

        var body = new
        {
            model = _chatModel,
            format = "json",
            stream = false,
            keep_alive = "10m",                 // keep the model loaded between requests
            options = new
            {
                temperature = 0.1,
                num_predict = 2048,             // cap output length so generation can't run away
                num_ctx = 8192                  // context window for the document text
            },
            messages = new[]
            {
                new { role = "system", content = "You extract structured fields from documents. Output valid JSON only." },
                new { role = "user", content = prompt }
            }
        };

        try
        {
            using var resp = await _http.PostAsJsonAsync("/api/chat", body, ct);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var content = doc.RootElement.GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content)) return result;

            var validKeys = schema.Fields.Select(f => f.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

            try
            {
                ParseStrict(content, validKeys, result);
            }
            catch (JsonException jx)
            {
                // Output was likely truncated mid-string — salvage the complete field objects.
                _logger.LogWarning(jx, "LLM JSON was malformed/truncated; attempting lenient salvage.");
                ParseLenient(content, validKeys, result);
            }
        }
        catch (Exception ex)
        {
            // Ollama not running / model missing — degrade gracefully, fields stay empty.
            _logger.LogWarning(ex, "Ollama mapping unavailable; skipping LLM step.");
        }

        return result;
    }

    private static void ParseStrict(string content, HashSet<string> validKeys, Dictionary<string, string> result)
    {
        using var inner = JsonDocument.Parse(content);
        if (!inner.RootElement.TryGetProperty("fields", out var fields) ||
            fields.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in fields.EnumerateArray())
        {
            if (item.TryGetProperty("found", out var found) && found.ValueKind == JsonValueKind.False) continue;

            var key = item.TryGetProperty("key", out var k) ? k.GetString() : null;
            var value = item.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;

            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value) && validKeys.Contains(key))
                result[key] = value;
        }
    }

    /// <summary>Recover complete {"key":...,"value":...} pairs from a truncated/invalid JSON blob.</summary>
    private static void ParseLenient(string content, HashSet<string> validKeys, Dictionary<string, string> result)
    {
        var matches = Regex.Matches(
            content,
            @"""key""\s*:\s*""(?<key>[^""]+)""\s*,\s*""value""\s*:\s*""(?<value>(?:[^""\\]|\\.)*)""",
            RegexOptions.Singleline);

        foreach (Match m in matches)
        {
            var key = m.Groups["key"].Value;
            var value = m.Groups["value"].Value.Replace("\\\"", "\"").Replace("\\n", " ").Trim();
            if (!string.IsNullOrWhiteSpace(value) && validKeys.Contains(key) && !result.ContainsKey(key))
                result[key] = value;
        }
    }

    public async Task<string?> ExtractStructuredAsync(
        string maskedText, string jsonTemplate, string examplesBlock, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(maskedText)) return null;

        var prompt =
            "You extract information from a document into a fixed JSON structure. Return ONLY JSON.\n\n" +
            (string.IsNullOrWhiteSpace(examplesBlock) ? "" : examplesBlock + "\n\n") +
            "Rules:\n" +
            "- Fill every field you can find. Use null for missing scalar values and [] for missing arrays.\n" +
            "- For arrays, include one object per item found in the document (e.g. each job, each degree).\n" +
            "- Do NOT invent data. Keep any {{TYPE_n}} placeholders verbatim.\n" +
            "- Keep individual text values concise.\n\n" +
            "Output JSON in exactly this structure (same keys):\n" + jsonTemplate + "\n\n" +
            "Document text:\n" + maskedText;

        var body = new
        {
            model = _chatModel,
            format = "json",
            stream = false,
            keep_alive = "10m",
            options = new
            {
                temperature = 0.1,
                num_predict = 4096,   // nested arrays can be long
                num_ctx = 8192
            },
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
            _logger.LogWarning(ex, "Ollama structured extraction unavailable; skipping LLM step.");
            return null;
        }
    }
}
