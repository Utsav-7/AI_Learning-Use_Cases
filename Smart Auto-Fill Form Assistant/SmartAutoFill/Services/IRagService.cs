using SmartAutoFill.Models;

namespace SmartAutoFill.Services;

public interface IRagService
{
    /// <summary>Retrieve the top-K most similar past mappings and format them as a few-shot block.</summary>
    Task<string> GetExamplesBlockAsync(string category, string maskedText, int k = 4, CancellationToken ct = default);

    /// <summary>Save confirmed field mappings back to the knowledge base (feedback loop).</summary>
    Task SaveExamplesAsync(string category, IEnumerable<ExtractedField> fields, IMaskingService masking, CancellationToken ct = default);
}
