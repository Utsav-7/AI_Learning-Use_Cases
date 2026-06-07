namespace SmartAutoFill.Models;

/// <summary>
/// Target JSON shapes per document type. The LLM fills these from the (masked) OCR text.
/// Every leaf is an object { "value": ..., "confidence": 0-1 } so the result carries a
/// per-field confidence score. Arrays (experience/education/line items) stay nested.
/// </summary>
public static class JsonTemplates
{
    // Shorthand leaf used throughout the templates below.
    private const string V = "{ \"value\": null, \"confidence\": 0 }";

    private static readonly Dictionary<string, string> Templates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Resume / CV"] = $$"""
        {
          "name": {{V}},
          "email": {{V}},
          "phone": {{V}},
          "linkedIn": {{V}},
          "address": {{V}},
          "summary": {{V}},
          "skills": [ {{V}} ],
          "experience": [
            { "company": {{V}}, "role": {{V}}, "duration": {{V}}, "location": {{V}}, "description": {{V}} }
          ],
          "education": [
            { "institution": {{V}}, "degree": {{V}}, "field": {{V}}, "year": {{V}} }
          ],
          "projects": [
            { "name": {{V}}, "description": {{V}} }
          ],
          "certifications": [ {{V}} ],
          "languages": [ {{V}} ]
        }
        """,

        ["Passport / ID"] = $$"""
        {
          "firstName": {{V}},
          "lastName": {{V}},
          "documentNumber": {{V}},
          "dateOfBirth": {{V}},
          "expiryDate": {{V}},
          "sex": {{V}},
          "nationality": {{V}},
          "placeOfBirth": {{V}},
          "address": {{V}}
        }
        """,

        ["Invoice"] = $$"""
        {
          "vendorName": {{V}},
          "vendorAddress": {{V}},
          "customerName": {{V}},
          "customerAddress": {{V}},
          "invoiceNumber": {{V}},
          "invoiceDate": {{V}},
          "dueDate": {{V}},
          "subtotal": {{V}},
          "tax": {{V}},
          "total": {{V}},
          "currency": {{V}},
          "lineItems": [
            { "description": {{V}}, "quantity": {{V}}, "unitPrice": {{V}}, "amount": {{V}} }
          ]
        }
        """,

        ["General Document (Layout)"] = $$"""
        {
          "title": {{V}},
          "summary": {{V}},
          "fields": [
            { "label": {{V}}, "value": {{V}} }
          ]
        }
        """,
    };

    public static string Get(string category) =>
        Templates.TryGetValue(category, out var t) ? t : Templates["General Document (Layout)"];

    /// <summary>
    /// Auto-detect instruction: the LLM classifies the document and fills the matching shape.
    /// Output is { "documentType": "...", "data": { ... } } with per-field confidence.
    /// </summary>
    public static string AutoInstruction() =>
        "First identify the document type — one of: \"resume\", \"invoice\", \"passport\", \"other\".\n" +
        "Then output JSON in EXACTLY this shape:\n" +
        "{ \"documentType\": \"<type>\", \"data\": { ... } }\n" +
        "where \"data\" follows the structure for the detected type below.\n\n" +
        "RULES:\n" +
        "- Every leaf field MUST be an object: { \"value\": <text or null>, \"confidence\": <number 0 to 1> }.\n" +
        "- confidence = how certain you are the value is correct: 1.0 when the value is stated explicitly in the document, lower when inferred or ambiguous, 0 when the field is absent (value null).\n" +
        "- For arrays, include one entry per item found in the document (each job, each line item). Use [] if none.\n" +
        "- Do NOT invent data. Keep values concise.\n\n" +
        "resume -> " + Templates["Resume / CV"] + "\n\n" +
        "invoice -> " + Templates["Invoice"] + "\n\n" +
        "passport -> " + Templates["Passport / ID"] + "\n\n" +
        "other -> " + Templates["General Document (Layout)"];
}
