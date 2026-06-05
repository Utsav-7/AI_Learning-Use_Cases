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
            { "name": null, "description": null }
          ],
          "certifications": [],
          "languages": []
        }
        """,

        ["Passport / ID"] = """
        {
          "firstName": null,
          "lastName": null,
          "documentNumber": null,
          "dateOfBirth": null,
          "expiryDate": null,
          "sex": null,
          "nationality": null,
          "placeOfBirth": null,
          "address": null
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
}
