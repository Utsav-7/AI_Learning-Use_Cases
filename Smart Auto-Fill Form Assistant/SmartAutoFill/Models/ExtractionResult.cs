namespace SmartAutoFill.Models;

/// <summary>The structured outcome returned to the UI after analysis.</summary>
public class ExtractionResult
{
    public string Category { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public List<ExtractedField> Fields { get; set; } = new();
    public string RawText { get; set; } = string.Empty;

    public double OverallConfidence =>
        Fields.Count == 0 ? 0 : Math.Round(Fields.Average(f => f.Confidence), 2);
}
