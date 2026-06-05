using SmartAutoFill.Models;

namespace SmartAutoFill.Services;

public interface IMaskingService
{
    /// <summary>Replace detected PII with type-tagged placeholders (reversible).</summary>
    MaskResult Mask(string text);

    /// <summary>Swap placeholders back to real values using a mask map.</summary>
    string Unmask(string maskedText, IReadOnlyDictionary<string, string> map);
}
