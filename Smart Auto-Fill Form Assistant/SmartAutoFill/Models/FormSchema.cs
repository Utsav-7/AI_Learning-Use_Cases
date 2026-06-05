namespace SmartAutoFill.Models;

/// <summary>
/// A predefined form definition (Req 4). Each document category maps to a
/// prebuilt Azure model and a set of target fields. The AzureFieldNames list
/// tells the service which Azure field(s) feed each target field.
/// </summary>
public class FormSchema
{
    public string Category { get; set; } = string.Empty;   // "Passport / ID", "Invoice"
    public string AzureModelId { get; set; } = string.Empty; // e.g. "prebuilt-idDocument"
    public List<FormFieldDefinition> Fields { get; set; } = new();

    /// <summary>
    /// When true (e.g. Resume), Azure has no structured prebuilt model, so we OCR with
    /// prebuilt-layout and pre-fill what regex can find (email/phone). Remaining fields are
    /// left for the Ollama mapping layer to populate from the masked text.
    /// </summary>
    public bool HeuristicExtraction { get; set; }
}

public class FormFieldDefinition
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsSensitive { get; set; }

    /// <summary>Azure field names (from the prebuilt model) that map to this field.</summary>
    public string[] AzureFieldNames { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Built-in catalogue of supported document types and their form fields.
/// Azure field names follow the prebuilt model schemas:
///   - prebuilt-idDocument: https://aka.ms/azsdk/formrecognizer/iddocumentfieldschema
///   - prebuilt-invoice:    https://aka.ms/azsdk/formrecognizer/invoicefieldschema
/// </summary>
public static class FormCatalog
{
    public static readonly List<FormSchema> Schemas = new()
    {
        new FormSchema
        {
            Category = "Passport / ID",
            AzureModelId = "prebuilt-idDocument",
            Fields = new()
            {
                new() { Key = "FirstName",     DisplayName = "First Name",      AzureFieldNames = new[] { "FirstName" } },
                new() { Key = "LastName",      DisplayName = "Last Name",       AzureFieldNames = new[] { "LastName" } },
                new() { Key = "DocumentNumber",DisplayName = "Document Number",  IsSensitive = true, AzureFieldNames = new[] { "DocumentNumber" } },
                new() { Key = "DateOfBirth",   DisplayName = "Date of Birth",   IsSensitive = true, AzureFieldNames = new[] { "DateOfBirth" } },
                new() { Key = "DateOfExpiration", DisplayName = "Expiry Date",  AzureFieldNames = new[] { "DateOfExpiration" } },
                new() { Key = "Sex",           DisplayName = "Sex",             AzureFieldNames = new[] { "Sex" } },
                new() { Key = "Nationality",   DisplayName = "Nationality",     AzureFieldNames = new[] { "Nationality", "CountryRegion" } },
                new() { Key = "Address",       DisplayName = "Address",         IsSensitive = true, AzureFieldNames = new[] { "Address" } },
            }
        },
        new FormSchema
        {
            Category = "Invoice",
            AzureModelId = "prebuilt-invoice",
            Fields = new()
            {
                new() { Key = "VendorName",    DisplayName = "Vendor Name",     AzureFieldNames = new[] { "VendorName" } },
                new() { Key = "CustomerName",  DisplayName = "Customer Name",   AzureFieldNames = new[] { "CustomerName" } },
                new() { Key = "InvoiceId",     DisplayName = "Invoice Number",  AzureFieldNames = new[] { "InvoiceId" } },
                new() { Key = "InvoiceDate",   DisplayName = "Invoice Date",    AzureFieldNames = new[] { "InvoiceDate" } },
                new() { Key = "DueDate",       DisplayName = "Due Date",        AzureFieldNames = new[] { "DueDate" } },
                new() { Key = "InvoiceTotal",  DisplayName = "Total Amount",    AzureFieldNames = new[] { "InvoiceTotal" } },
                new() { Key = "SubTotal",      DisplayName = "Subtotal",        AzureFieldNames = new[] { "SubTotal" } },
                new() { Key = "TotalTax",      DisplayName = "Tax",             AzureFieldNames = new[] { "TotalTax" } },
            }
        },
        new FormSchema
        {
            // Resumes have no prebuilt Azure model — OCR via layout, regex-prefill email/phone,
            // and leave the rest for the Ollama mapping layer.
            Category = "Resume / CV",
            AzureModelId = "prebuilt-layout",
            HeuristicExtraction = true,
            Fields = new()
            {
                new() { Key = "FullName",   DisplayName = "Full Name" },
                new() { Key = "Email",      DisplayName = "Email",      AzureFieldNames = new[] { "Email" } },
                new() { Key = "Phone",      DisplayName = "Phone",      IsSensitive = true, AzureFieldNames = new[] { "Phone" } },
                new() { Key = "LinkedIn",   DisplayName = "LinkedIn / URL" },
                new() { Key = "Skills",     DisplayName = "Skills" },
                new() { Key = "Experience", DisplayName = "Total Experience" },
                new() { Key = "Education",  DisplayName = "Education" },
            }
        },
        new FormSchema
        {
            // General/layout: no per-field schema; we surface any detected key-value pairs.
            Category = "General Document (Layout)",
            AzureModelId = "prebuilt-layout",
            Fields = new()
        }
    };

    public static FormSchema GetByCategory(string category) =>
        Schemas.First(s => s.Category == category);
}
