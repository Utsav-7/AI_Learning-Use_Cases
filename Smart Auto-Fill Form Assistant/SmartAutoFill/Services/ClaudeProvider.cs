using Anthropic;
using Anthropic.Models.Messages;

namespace SmartAutoFill.Services;

/// <summary>Claude (Anthropic) provider via the official C# SDK.</summary>
public class ClaudeProvider : ILlmProvider
{
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly ILogger<ClaudeProvider> _logger;
    private AnthropicClient? _client;

    public string Name => "Claude (Anthropic)";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public ClaudeProvider(IConfiguration config, ILogger<ClaudeProvider> logger)
    {
        _apiKey = config["Claude:ApiKey"];
        _model = config["Claude:Model"] ?? "claude-opus-4-8";
        _logger = logger;
    }

    public async Task<string?> ClassifyAndExtractAsync(string maskedText, string instruction, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Claude provider has no API key configured (Claude:ApiKey); skipping.");
            return null;
        }
        if (string.IsNullOrWhiteSpace(maskedText)) return null;

        _client ??= new AnthropicClient { ApiKey = _apiKey };

        var prompt =
            instruction + "\n\n" +
            "Keep any {{TYPE_n}} placeholders verbatim. Return ONLY JSON.\n\n" +
            "Document text:\n" + maskedText;

        try
        {
            var parameters = new MessageCreateParams
            {
                Model = _model, // implicit string -> Model conversion
                MaxTokens = 4096,
                System = "You convert documents into structured JSON. Output valid JSON only — no prose, no code fences.",
                Messages = [new() { Role = Role.User, Content = prompt }],
            };

            Message response = await _client.Messages.Create(parameters, cancellationToken: ct);

            // Content is a union list; unwrap text blocks.
            var text = string.Concat(response.Content
                .Select(b => b.Value)
                .OfType<TextBlock>()
                .Select(t => t.Text));

            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Claude extraction failed; skipping LLM step.");
            return null;
        }
    }
}
