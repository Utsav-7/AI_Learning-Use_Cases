using System.Text;
using System.Text.RegularExpressions;
using SmartAutoFill.Models;

namespace SmartAutoFill.Services;

/// <summary>
/// Regex-based PII masking (detection method #1 + technique #8 from the masking
/// discussion): type-tagged, reversible placeholder tokenization. Patterns are
/// ordered most-specific-first so e.g. a PAN/Aadhaar is matched before a generic
/// phone/number pattern can claim its digits.
/// </summary>
public class RegexMaskingService : IMaskingService
{
    private sealed record Rule(string Tag, Regex Pattern, Func<string, bool>? Validate = null);

    private static readonly Rule[] Rules =
    {
        // Email
        new("EMAIL", new Regex(@"\b[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled)),
        // US SSN  123-45-6789
        new("SSN", new Regex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled)),
        // India PAN  ABCDE1234F
        new("PAN", new Regex(@"\b[A-Z]{5}[0-9]{4}[A-Z]\b", RegexOptions.Compiled)),
        // India Aadhaar  1234 5678 9012
        new("AADHAAR", new Regex(@"\b\d{4}\s?\d{4}\s?\d{4}\b", RegexOptions.Compiled)),
        // Credit card  13-16 digits (with spaces/dashes) — validated by Luhn
        new("CARD", new Regex(@"\b(?:\d[ -]?){13,16}\b", RegexOptions.Compiled), Luhn),
        // Passport  one letter + 7 digits (generic)
        new("PASSPORT", new Regex(@"\b[A-Z][0-9]{7}\b", RegexOptions.Compiled)),
        // Date (DOB etc.)  dd/mm/yyyy, d-m-yy, etc.
        new("DOB", new Regex(@"\b\d{1,2}[/\-.]\d{1,2}[/\-.]\d{2,4}\b", RegexOptions.Compiled)),
        // Phone  optional +cc then 10 digits, allowing spaces/dashes
        new("PHONE", new Regex(@"\b(?:\+?\d{1,3}[\s\-]?)?(?:\d[\s\-]?){10}\b", RegexOptions.Compiled)),
    };

    public MaskResult Mask(string text)
    {
        var result = new MaskResult();
        if (string.IsNullOrEmpty(text))
        {
            result.MaskedText = text ?? string.Empty;
            return result;
        }

        var counters = new Dictionary<string, int>();
        var working = text;

        foreach (var rule in Rules)
        {
            working = rule.Pattern.Replace(working, m =>
            {
                // Skip if it's already a placeholder we inserted, or fails validation.
                if (m.Value.Contains("{{")) return m.Value;
                if (rule.Validate is not null && !rule.Validate(m.Value)) return m.Value;

                counters.TryGetValue(rule.Tag, out var n);
                counters[rule.Tag] = ++n;
                var placeholder = $"{{{{{rule.Tag}_{n}}}}}";
                result.Map[placeholder] = m.Value;
                return placeholder;
            });
        }

        result.MaskedText = working;
        return result;
    }

    public string Unmask(string maskedText, IReadOnlyDictionary<string, string> map)
    {
        if (string.IsNullOrEmpty(maskedText) || map.Count == 0) return maskedText;

        var sb = new StringBuilder(maskedText);
        foreach (var kv in map)
            sb.Replace(kv.Key, kv.Value);
        return sb.ToString();
    }

    /// <summary>Luhn checksum to avoid masking random long numbers as cards.</summary>
    private static bool Luhn(string value)
    {
        var digits = value.Where(char.IsDigit).Select(c => c - '0').ToArray();
        if (digits.Length is < 13 or > 16) return false;

        var sum = 0;
        var alt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i];
            if (alt) { d *= 2; if (d > 9) d -= 9; }
            sum += d;
            alt = !alt;
        }
        return sum % 10 == 0;
    }
}
