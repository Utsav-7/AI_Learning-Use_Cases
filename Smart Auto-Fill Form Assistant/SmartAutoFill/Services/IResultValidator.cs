namespace SmartAutoFill.Services;

/// <summary>A validation finding about an extracted field. Severity: "warning" | "info".</summary>
public record FieldIssue(string Field, string Message, string Severity);

public interface IResultValidator
{
    /// <summary>Validate the final JSON (format, cross-field, confidence) and return issues to review.</summary>
    IReadOnlyList<FieldIssue> Validate(string finalJson, string rawText);
}
