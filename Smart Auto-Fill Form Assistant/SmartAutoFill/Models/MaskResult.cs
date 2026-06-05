namespace SmartAutoFill.Models;

/// <summary>
/// Output of the masking layer (Section 4 / 4.5 of the plan).
/// MaskedText is safe to send to an LLM; Map restores the real values afterwards.
/// </summary>
public class MaskResult
{
    /// <summary>Text with PII replaced by type-tagged placeholders, e.g. {{SSN_1}}.</summary>
    public string MaskedText { get; set; } = string.Empty;

    /// <summary>placeholder -> real value, kept only in the backend.</summary>
    public Dictionary<string, string> Map { get; set; } = new();

    /// <summary>Number of PII spans that were masked.</summary>
    public int MaskedCount => Map.Count;
}
