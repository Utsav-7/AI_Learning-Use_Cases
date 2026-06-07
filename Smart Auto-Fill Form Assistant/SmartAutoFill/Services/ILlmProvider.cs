namespace SmartAutoFill.Services;

/// <summary>
/// Provider-agnostic LLM contract. Implementations: local Ollama, Claude (Anthropic),
/// GPT (OpenAI). The app selects one at runtime via <see cref="ILlmProviderFactory"/>.
/// </summary>
public interface ILlmProvider
{
    /// <summary>Display name shown in the UI selector and used as the lookup key.</summary>
    string Name { get; }

    /// <summary>True when the provider has the configuration it needs (e.g. an API key).</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Classify the (masked) document and extract a nested JSON object following the
    /// given instruction. Returns raw JSON (may contain {{PII}} placeholders), or null
    /// if the provider is unavailable.
    /// </summary>
    Task<string?> ClassifyAndExtractAsync(string maskedText, string instruction, CancellationToken ct = default);
}
