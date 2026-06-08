namespace SmartAutoFill.Models;

/// <summary>
/// Target JSON shapes per document type. The LLM fills these from the (masked) OCR text,
/// producing nested output (arrays of objects for experience/education/line items, etc.).
/// </summary>
public static class JsonTemplates
{
    private static readonly Dictionary<string, string> Templates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Resume / CV"] = """
        {
          "name": null,
          "email": null,
          "phone": null,
          "linkedIn": null,
          "address": null,
          "summary": null,
          "skills": [],
          "experience": [
            { "company": null, "role": null, "duration": null, "location": null, "description": null }
          ],
          "education": [
            { "institution": null, "degree": null, "field": null, "year": null }
          ],
          "projects": [
            { "name": null, "technologies": [], "link": null, "description": null }
          ],
          "certifications": [],
          "languages": [
            { "language": null, "proficiency": null }
          ]
        }
        """,

        ["Passport / ID"] = """
        {
          "mrz": { "line1": null, "line2": null },
          "surname": null,
          "givenName": null,
          "fullName": null,
          "nationality": null,
          "dateOfBirth": null,
          "sex": null,
          "placeOfBirth": null,
          "passportNumber": null,
          "dateOfIssue": null,
          "dateOfExpiry": null,
          "issuingCountry": null,
          "issuingAuthority": null,
          "personalNumber": null,
          "religion": null,
          "profession": null,
          "nationalId": null,
          "confidenceScores": {
            "mrz": 0.0,
            "passportNumber": 0.0,
            "dateOfBirth": 0.0,
            "dateOfExpiry": 0.0,
            "fullName": 0.0
          }
        }
        """,

        ["Invoice"] = """
        {
          "vendorName": null,
          "vendorAddress": null,
          "customerName": null,
          "customerAddress": null,
          "invoiceNumber": null,
          "invoiceDate": null,
          "dueDate": null,
          "subtotal": null,
          "tax": null,
          "total": null,
          "currency": null,
          "lineItems": [
            { "description": null, "quantity": null, "unitPrice": null, "amount": null }
          ]
        }
        """,

        ["General Document (Layout)"] = """
        {
          "title": null,
          "summary": null,
          "fields": [
            { "label": null, "value": null }
          ]
        }
        """,
    };

    public static string Get(string category) =>
        Templates.TryGetValue(category, out var t) ? t : Templates["General Document (Layout)"];

    /// <summary>
    /// Auto-detect instruction: the LLM classifies the document, analyses the text, and fills the
    /// matching shape. Output is { "documentType": "...", "confidence": 0-1, "data": { ... } }.
    /// </summary>
    public static string AutoInstruction() =>
        "You are a precise document parser. Extract structured data from the raw text below.\n\n" +

        "STEP 1 — Identify the document type: \"resume\", \"invoice\", \"passport\", or \"other\".\n\n" +

        "STEP 2 — Output JSON in EXACTLY this shape:\n" +
        "{ \"documentType\": \"<type>\", \"confidence\": 0.0-1.0, \"data\": { ... } }\n\n" +

        "GLOBAL CLEANING RULES (apply to every field before writing a value):\n" +
        "- Analyse and understand the text, then derive the value — do NOT copy raw lines verbatim.\n" +
        "- Strip OCR artifacts: remove :selected:, :unselected:, ●, ○, □, ■ from all values.\n" +
        "- Trim leading/trailing whitespace and punctuation from every string.\n" +
        "- Do NOT invent data. If a value is absent, use null for scalars and [] for arrays.\n" +
        "- Never copy raw layout separators (|, —, •) into field values unless they are part of a URL.\n\n" +

        "RESUME RULES (when documentType = \"resume\"):\n" +
        "- name: The candidate's full name. It is almost never labelled 'Name:' — look for the " +
        "largest or first prominent text at the very top of the document, before any contact info.\n" +
        "- phone: digits + country code only. Strip all non-numeric suffix text.\n" +
        "- skills: Return a FLAT string array. Split across ALL sub-sections " +
        "(Languages, Frameworks, Tools, Databases etc.) — one technology per array entry. " +
        "Do not group; do not include category headings.\n" +
        "- experience[].description: Plain prose only. Do not include bullet symbols.\n" +
        "- projects[]: For each project found:\n" +
        "    name        → title only, everything BEFORE the first ' | ' separator.\n" +
        "    technologies → string[] parsed from the segment BETWEEN the first and last ' | '.\n" +
        "    description  → the body text below the title line.\n" +
        "    Drop any trailing '| Code', '| Link', '| GitHub' labels entirely.\n" +
        "- languages: one entry per language with its proficiency (e.g. Native, Fluent, B2).\n\n" +

        "PASSPORT RULES (when documentType = \"passport\"):\n" +
        "- mrz: the two machine-readable lines at the bottom (the long strings full of '<'). " +
        "Put them verbatim in mrz.line1 and mrz.line2; many fields can be cross-checked from them.\n" +
        "- sex: normalise to one of \"M\", \"F\", or \"X\".\n" +
        "- dates (dateOfBirth/dateOfIssue/dateOfExpiry): keep as printed on the document.\n" +
        "- fullName: combine givenName + surname.\n" +
        "- confidenceScores: a 0.0-1.0 certainty for each listed key.\n\n" +

        "resume schema -> " + Templates["Resume / CV"] + "\n\n" +
        "invoice schema -> " + Templates["Invoice"] + "\n\n" +
        "passport schema -> " + Templates["Passport / ID"] + "\n\n" +
        "other schema -> " + Templates["General Document (Layout)"];
}
