using SmartAutoFill.Models;

namespace SmartAutoFill.Services;

public interface IOllamaService
{
    /// <summary>
    /// Ask the local LLM to map masked document text into the schema's fields.
    /// Returns key -> value for fields the model found. Values may still contain
    /// {{PII}} placeholders — caller restitches them.
    /// </summary>
    Task<Dictionary<string, string>> MapFieldsAsync(
        string maskedText,
        FormSchema schema,
        string examplesBlock,
        CancellationToken ct = default);

    /// <summary>
    /// Fill a nested JSON template from the masked document text. Returns the raw JSON
    /// string (values may still contain {{PII}} placeholders), or null if unavailable.
    /// </summary>
    Task<string?> ExtractStructuredAsync(
        string maskedText,
        string jsonTemplate,
        string examplesBlock,
        CancellationToken ct = default);
}
