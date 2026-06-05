# SmartAutoFill — Blazor App

A simple Blazor Web App (Server interactivity, .NET 9) implementing the document → form pipeline
from `../Implementation-Plan.md`. It covers requirements 1–8: upload → OCR/extract (Azure Document
Intelligence) → map to a predefined form → highlight missing fields → review/edit → confidence
scores → final JSON.

**Implemented now:** Azure extraction + review/JSON + **PII masking** + **SQL Server persistence**.
**Not yet wired:** Ollama mapping/normalization and RAG against the `MappingExamples` table.

## 1. Configure your Azure Document Intelligence resource

After you create the resource in the Azure portal, copy its **Endpoint** and a **Key**.
Don't commit the key — use **user-secrets** (recommended) or edit `appsettings.json`.

```powershell
# from the SmartAutoFill project folder
dotnet user-secrets init
dotnet user-secrets set "AzureDocumentIntelligence:Endpoint" "https://YOUR-RESOURCE.cognitiveservices.azure.com/"
dotnet user-secrets set "AzureDocumentIntelligence:ApiKey"   "YOUR_KEY_HERE"
```

(Or just replace the placeholders in `appsettings.json`.)

## 1b. Database (SQL Server)

The app persists to SQL Server via EF Core. The connection string in `appsettings.json` points at
`localhost\SQLEXPRESS` / database `AutoFillFormDb`. The database + tables have already been created
via migration. To re-create on another machine:

```powershell
dotnet ef database update
```

Tables: `Documents`, `ExtractedFields`, `AuditLogs` (+ `__EFMigrationsHistory`).

## 2. Run

```powershell
dotnet run
```

Open the HTTPS URL shown (e.g. https://localhost:7061).

## 3. Use

1. Pick a **Document type** — Passport/ID, Invoice, or General (Layout).
2. Choose a PDF/image and click **Extract**.
3. Review the form: each field shows a **confidence badge**; missing fields are highlighted; 🔒 marks PII.
4. Edit any value (marked *edited*, confidence → 100%).
5. Click **Generate Final JSON**.

## Project map

| Path | Purpose |
|---|---|
| `Models/ExtractedField.cs` | One form field + confidence + missing/PII flags |
| `Models/FormSchema.cs` | Predefined forms per doc type + Azure model mapping |
| `Models/ExtractionResult.cs` | Result returned to the UI |
| `Services/AzureDocumentExtractionService.cs` | Calls Azure DI, maps fields into the schema |
| `Services/RegexMaskingService.cs` | Type-tagged, reversible PII masking (email/SSN/PAN/Aadhaar/card/passport/DOB/phone) |
| `Services/DocumentRepository.cs` | Saves documents/fields/audit to SQL Server (EF Core) |
| `Data/AppDbContext.cs`, `Data/Entities.cs` | EF Core context + entities |
| `Components/Pages/Home.razor` | Upload → review/edit → JSON page |

## Supported document types

| Type | Azure model | Fields |
|---|---|---|
| Passport / ID | `prebuilt-idDocument` | name, document no., DOB, expiry, sex, nationality, address |
| Invoice | `prebuilt-invoice` | vendor, customer, invoice no., dates, totals, tax |
| General (Layout) | `prebuilt-layout` | detected key-value pairs + raw text |

## Next steps (from the plan)

- **Ollama** mapping/normalization (send the *masked* text to the local model).
- **RAG** against a SQL `MappingExamples` table + the feedback loop on user edits.
