namespace SmartAutoFill.Models;

/// <summary>
/// One field in the structured form, populated from a document.
/// Carries the confidence score (Req 8) and missing flag (Req 5).
/// </summary>
public class ExtractedField
{
    public string Key { get; set; } = string.Empty;          // e.g. "FullName"
    public string DisplayName { get; set; } = string.Empty;  // e.g. "Full Name"
    public string? Value { get; set; }                       // editable value (Req 6)
    public double Confidence { get; set; }                   // 0.0 - 1.0 (Req 8)
    public bool IsMissing => string.IsNullOrWhiteSpace(Value); // (Req 5)
    public bool IsSensitive { get; set; }                    // PII flag
    public bool UserEdited { get; set; }                     // set true when user changes it
    public string Source { get; set; } = "Azure";            // Azure / Ollama / User

    /// <summary>UI badge colour for the confidence level.</summary>
    public string ConfidenceLevel => Confidence switch
    {
        >= 0.85 => "high",
        >= 0.60 => "medium",
        _ => "low"
    };
}
