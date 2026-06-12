namespace SmartAutoFill.Models;

/// <summary>
/// Target JSON shapes per document type. The LLM fills these from the (masked) OCR text,
/// producing nested output (arrays of objects for experience/education/line items, etc.).
/// Every type ends with a "confidenceScores" object holding a 0.0-1.0 score for each field.
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
          ],
          "confidenceScores": {
            "name": 0.0, "email": 0.0, "phone": 0.0, "linkedIn": 0.0, "address": 0.0,
            "summary": 0.0, "skills": 0.0, "experience": 0.0, "education": 0.0,
            "projects": 0.0, "certifications": 0.0, "languages": 0.0
          }
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
            "mrz": 0.0, "surname": 0.0, "givenName": 0.0, "fullName": 0.0, "nationality": 0.0,
            "dateOfBirth": 0.0, "sex": 0.0, "placeOfBirth": 0.0, "passportNumber": 0.0,
            "dateOfIssue": 0.0, "dateOfExpiry": 0.0, "issuingCountry": 0.0, "issuingAuthority": 0.0,
            "personalNumber": 0.0, "religion": 0.0, "profession": 0.0, "nationalId": 0.0
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
          ],
          "confidenceScores": {
            "vendorName": 0.0, "vendorAddress": 0.0, "customerName": 0.0, "customerAddress": 0.0,
            "invoiceNumber": 0.0, "invoiceDate": 0.0, "dueDate": 0.0, "subtotal": 0.0,
            "tax": 0.0, "total": 0.0, "currency": 0.0, "lineItems": 0.0
          }
        }
        """,

        ["General Document (Layout)"] = """
        {
          "title": null,
          "summary": null,
          "fields": [
            { "label": null, "value": null }
          ],
          "confidenceScores": {
            "title": 0.0, "summary": 0.0, "fields": 0.0
          }
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

        "CONFIDENCE RULES (apply to EVERY document type):\n" +
        "- Each schema's \"data\" object ends with a \"confidenceScores\" object.\n" +
        "- Fill a score from 0.0 to 1.0 for EVERY field listed in confidenceScores — never leave them at 0 if the field has a value.\n" +
        "- 1.0 = the value is stated explicitly in the document; lower = inferred or ambiguous; 0.0 = the field is absent/null.\n" +
        "- Also set the top-level \"confidence\" to your overall certainty about the document type.\n\n" +

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
        "- fullName: combine givenName + surname.\n\n" +

        "resume schema -> " + Templates["Resume / CV"] + "\n\n" +
        "invoice schema -> " + Templates["Invoice"] + "\n\n" +
        "passport schema -> " + Templates["Passport / ID"] + "\n\n" +
        "other schema -> " + Templates["General Document (Layout)"];
}
