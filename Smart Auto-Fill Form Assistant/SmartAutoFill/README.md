# SmartAutoFill — Blazor App

A simple Blazor Web App (Server interactivity, .NET 9). **No database, no storage** — a straight
pipeline that returns JSON:

```
Upload (PDF/image)  →  Azure Document Intelligence (OCR)  →  PII masking
                    →  AI model (Ollama / Claude / GPT)   →  JSON with value + confidence
```

Nothing is persisted; each upload is processed in-memory and the result is shown on screen.

## 1. Configure Azure Document Intelligence

After creating the resource in the Azure portal, copy its **Endpoint** and a **Key**.
Don't commit keys — use **user-secrets** (recommended) or edit `appsettings.json`.

```powershell
# from the SmartAutoFill project folder
dotnet user-secrets init
dotnet user-secrets set "AzureDocumentIntelligence:Endpoint" "https://YOUR-RESOURCE.cognitiveservices.azure.com/"
dotnet user-secrets set "AzureDocumentIntelligence:ApiKey"   "YOUR_KEY_HERE"
```

## 2. Configure the AI model provider(s)

Pick the provider from the **AI model** dropdown in the UI. Defaults to local Ollama.

```powershell
# Local Ollama (no key) — just have it running with a model pulled:
ollama pull llama3.1:8b

# Claude (optional):
dotnet user-secrets set "Claude:ApiKey" "sk-ant-..."

# GPT / OpenAI (optional):
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
```

Model IDs / default provider live in `appsettings.json` (`Ollama:ChatModel`, `Claude:Model`,
`OpenAI:Model`, `Llm:Provider`).

## 3. Run

```powershell
dotnet run
```

Open the HTTPS URL shown (e.g. https://localhost:7061).

## 4. Use

1. Pick the **AI model** (Ollama / Claude / GPT).
2. Choose a PDF/image and click **Upload**.
3. The page shows the document preview and the extracted **JSON** — the model auto-detects the
   document type and returns each field as `{ "value": ..., "confidence": 0-1 }`.

## Project map

| Path | Purpose |
|---|---|
| `Services/AzureDocumentExtractionService.cs` | Calls Azure DI for OCR text |
| `Services/RegexMaskingService.cs` | Type-tagged, reversible PII masking before the LLM call |
| `Services/ILlmProvider.cs` + `LlmProviderFactory.cs` | Provider abstraction + runtime selection |
| `Services/OllamaProvider.cs` / `ClaudeProvider.cs` / `OpenAiProvider.cs` | The three AI backends |
| `Models/JsonTemplates.cs` | Per-type target JSON shape (value + confidence) the model fills |
| `Components/Pages/Home.razor` | Upload → preview + JSON page |

## How confidence works

OCR (layout) gives plain text, so per-field confidence comes from the **AI model's own
self-assessment** (1.0 = explicit in the document, lower = inferred/ambiguous). If the model is
unavailable, the app falls back to Azure's OCR key-value pairs and uses Azure's confidence.
