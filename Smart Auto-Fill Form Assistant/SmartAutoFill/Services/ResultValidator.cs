using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SmartAutoFill.Services;

/// <summary>
/// Post-extraction validation: field formats (email/phone/date), cross-field checks
/// (invoice totals, passport dates), required fields, and low-confidence flags.
/// </summary>
public class ResultValidator : IResultValidator
{
    private static readonly Regex EmailRx =
        new(@"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$", RegexOptions.Compiled);

    public IReadOnlyList<FieldIssue> Validate(string finalJson, string rawText)
    {
        var issues = new List<FieldIssue>();
        if (string.IsNullOrWhiteSpace(finalJson)) return issues;

        JsonObject? root;
        try { root = JsonNode.Parse(finalJson) as JsonObject; }
        catch { return issues; }
        if (root is null) return issues;

        var docType = root.TryGetPropertyValue("documentType", out var dt) ? dt?.ToString() : null;
        var data = root.TryGetPropertyValue("data", out var d) && d is JsonObject dObj ? dObj : root;

        // Generic per-field format checks.
        Walk(data, issues);

        // Type-specific cross-field checks.
        if (Eq(docType, "invoice")) ValidateInvoice(data, issues);
        else if (Eq(docType, "passport")) ValidatePassport(data, issues);
        else if (Eq(docType, "resume") && IsNull(data, "name"))
            issues.Add(new("name", "Candidate name not detected.", "warning"));

        // Low-confidence flags.
        if (data.TryGetPropertyValue("confidenceScores", out var cs) && cs is JsonObject scores)
        {
            foreach (var kv in scores)
            {
                if (kv.Value is JsonValue v && v.TryGetValue<double>(out var score) && score is > 0 and < 0.6)
                    issues.Add(new(kv.Key, $"Low confidence ({score:P0}) — please review.", "info"));
            }
        }

        return issues;
    }

    private static void Walk(JsonNode? node, List<FieldIssue> issues)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var kv in obj)
                {
                    if (Eq(kv.Key, "confidenceScores")) continue;
                    CheckLeaf(kv.Key, kv.Value, issues);
                    Walk(kv.Value, issues);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr) Walk(item, issues);
                break;
        }
    }

    private static void CheckLeaf(string key, JsonNode? value, List<FieldIssue> issues)
    {
        if (value is not JsonValue) return;
        var s = value.ToString();
        if (string.IsNullOrWhiteSpace(s)) return;

        var k = key.ToLowerInvariant();
        if (k == "email" && !EmailRx.IsMatch(s.Trim()))
            issues.Add(new(key, $"Email looks invalid: \"{s}\".", "warning"));
        else if (k is "phone" or "phonenumber" && s.Count(char.IsDigit) < 7)
            issues.Add(new(key, $"Phone number looks incomplete: \"{s}\".", "warning"));
        else if (k.Contains("date") && !LooksLikeDate(s))
            issues.Add(new(key, $"Date not recognised: \"{s}\".", "info"));
    }

    private static void ValidateInvoice(JsonObject data, List<FieldIssue> issues)
    {
        var sub = ParseMoney(GetString(data, "subtotal"));
        var tax = ParseMoney(GetString(data, "tax"));
        var total = ParseMoney(GetString(data, "total"));
        if (sub is not null && total is not null)
        {
            var expected = sub.Value + (tax ?? 0m);
            if (Math.Abs(expected - total.Value) > 0.02m)
                issues.Add(new("total",
                    $"Totals don't add up: subtotal {sub} + tax {tax ?? 0m} ≠ total {total}.", "warning"));
        }
        if (IsNull(data, "invoiceNumber"))
            issues.Add(new("invoiceNumber", "Invoice number not detected.", "info"));
    }

    private static void ValidatePassport(JsonObject data, List<FieldIssue> issues)
    {
        var issue = ParseDate(GetString(data, "dateOfIssue"));
        var expiry = ParseDate(GetString(data, "dateOfExpiry"));
        if (issue is not null && expiry is not null && expiry <= issue)
            issues.Add(new("dateOfExpiry", "Expiry date is not after the issue date.", "warning"));
        if (IsNull(data, "passportNumber"))
            issues.Add(new("passportNumber", "Passport number not detected.", "warning"));
    }

    // ---- helpers ----
    private static bool Eq(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool IsNull(JsonObject o, string key) =>
        !o.TryGetPropertyValue(key, out var v) || v is null || string.IsNullOrWhiteSpace(v.ToString());

    private static string? GetString(JsonObject o, string key) =>
        o.TryGetPropertyValue(key, out var v) && v is not null ? v.ToString() : null;

    private static bool LooksLikeDate(string s) =>
        DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
        Regex.IsMatch(s, @"(19|20)\d{2}"); // accept "Sep 2021", ranges, etc.

    private static DateTime? ParseDate(string? s) =>
        !string.IsNullOrWhiteSpace(s) &&
        DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static decimal? ParseMoney(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var cleaned = Regex.Replace(s, @"[^\d.\-]", "");
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var m) ? m : null;
    }
}
