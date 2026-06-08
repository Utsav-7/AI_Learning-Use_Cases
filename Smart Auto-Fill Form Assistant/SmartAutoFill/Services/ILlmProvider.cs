namespace SmartAutoFill.Services;

/// <summary>Outcome of an extraction call: the JSON, and/or a user-facing error message.</summary>
public record LlmResult(string? Json, string? Error = null);

/// <summary>
/// Provider-agnostic LLM contract. Implementations: local Ollama, Gemini (Google).
/// The app selects one at runtime via <see cref="ILlmProviderFactory"/>.
/// </summary>
public interface ILlmProvider
{
    /// <summary>Display name shown in the UI selector and used as the lookup key.</summary>
    string Name { get; }

    /// <summary>True when the provider has the configuration it needs (e.g. an API key).</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Classify the (masked) document and extract a nested JSON object following the
    /// given instruction. On failure, returns Json=null with a user-facing Error message.
    /// </summary>
    Task<LlmResult> ClassifyAndExtractAsync(string maskedText, string instruction, CancellationToken ct = default);
}
