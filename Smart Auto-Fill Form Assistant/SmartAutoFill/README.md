# Smart Auto-Fill Form Assistant

An **AI-powered document → structured-JSON** assistant. Upload a PDF or image (resume, invoice,
passport, or any document) and get back clean, structured JSON with per-field confidence scores.

Built with **Blazor Web App (Server interactivity, .NET 9)**. **No database, no storage** — every
upload is processed in-memory and the result is shown on screen, then discarded.

```
Upload (PDF/image)
   → Input validation (type + size + magic-byte)
   → Azure Document Intelligence (OCR text)
   → OCR cleanup (strip checkbox artifacts)
   → PII masking (reversible, type-tagged)
   → AI model (Ollama local: Qwen2.5 / Mistral / Llama, or Google Gemini)
   → Un-mask (restore real PII) + contact-field backfill
   → JSON with per-field confidence scores
   → Post-extraction validation ("needs review" panel)
   → Display (document preview + highlighted JSON)
```

---

## Features

- **Multiple AI models, switchable at runtime** — a dropdown lists every configured Ollama model
  (e.g. Qwen2.5 7B, Mistral 7B, Llama 3.1 8B) plus **Google Gemini**.
- **Auto document-type detection** — the model classifies the document (resume / invoice / passport
  / other) and fills the matching nested JSON schema.
- **PII masking** — emails, phones, SSN, PAN, Aadhaar, credit cards (Luhn-checked), passport numbers,
  and dates are masked with reversible, type-tagged placeholders *before* any text reaches the model.
- **Per-field confidence scores** for every field, on every document type, with every model.
- **Data validation** — email/phone/date formats, invoice totals (`subtotal + tax ≈ total`),
  passport date sanity, low-confidence flags → a "needs review" panel.
- **Nice UX** — document preview (PDF viewer / image with **hover-to-zoom**), syntax-highlighted JSON
  with a **Copy** button, loading spinner, and success/warning/error toasts.

---

## Tech stack

| Layer | Choice |
|---|---|
| App | ASP.NET Core **Blazor Web App** (Server interactivity, **.NET 9**) |
| OCR | **Azure AI Document Intelligence** (`Azure.AI.DocumentIntelligence`) |
| Local LLM | **Ollama** (Qwen2.5 7B / Mistral 7B / Llama 3.1 8B) |
| Cloud LLM | **Google Gemini** (Generative Language REST API) |
| Masking | Custom regex service (reversible, type-tagged) |
| Storage | **None** (in-memory only) |

---

## Prerequisites

- **.NET 9 SDK**
- An **Azure Document Intelligence** resource (endpoint + key)
- For local models: **Ollama** installed (see [Local AI Agent setup](#local-ai-agent-setup-ollama))
- For Gemini (optional): a **Google AI Studio API key**

---

## Setup

### 1. Configure Azure Document Intelligence

Create the resource in the Azure portal, copy its **Endpoint** and a **Key**. Don't commit keys —
use **user-secrets** (recommended) or edit `appsettings.json`.

```powershell
# from the SmartAutoFill project folder
dotnet user-secrets init
dotnet user-secrets set "AzureDocumentIntelligence:Endpoint" "https://YOUR-RESOURCE.cognitiveservices.azure.com/"
dotnet user-secrets set "AzureDocumentIntelligence:ApiKey"   "YOUR_KEY_HERE"
```

### 2. Configure AI models

Models are configured in `appsettings.json`:

```json
"Ollama": {
  "BaseUrl": "http://localhost:11434",
  "Models": [
    { "Name": "Qwen2.5 7B", "Model": "qwen2.5:7b-instruct" },
    { "Name": "Mistral 7B",  "Model": "mistral:7b" },
    { "Name": "Llama 3.1 8B", "Model": "llama3.1:8b" }
  ]
},
"Llm": { "Provider": "Qwen2.5 7B" },          // default dropdown selection
"Gemini": { "ApiKey": "", "Model": "gemini-2.5-flash" }
```

- Each entry under `Ollama:Models` becomes a **dropdown option** (label + Ollama model id).
- `Llm:Provider` sets the default selection (must match a `Name` above, or `"Gemini (Google)"`).
- Add the Gemini key via user-secrets (don't commit it):
  ```powershell
  dotnet user-secrets set "Gemini:ApiKey" "AIza..."
  ```

### 3. Run

```powershell
dotnet run
```

Open the HTTPS URL shown (e.g. `https://localhost:7061`).

### 4. Use

1. Pick the **AI model** from the dropdown.
2. **Choose file** (PDF or image) and click **Upload**.
3. The document preview appears on the left; the extracted **JSON** (with `confidenceScores`) on the
   right. A "needs review" panel lists any fields that failed validation.

---

## Local AI Agent setup (Ollama)

The local models run through **Ollama** — a local LLM runtime serving an API at
`http://localhost:11434`. The app talks to it; nothing leaves your machine for local models.

### Install Ollama

- Download from <https://ollama.com/download/windows> and run the installer, **or** in PowerShell:
  ```powershell
  irm https://ollama.com/install.ps1 | iex
  ```

### Pull the models used by this app

```powershell
ollama pull qwen2.5:7b-instruct
ollama pull mistral:7b
ollama pull llama3.1:8b
ollama list          # verify
```

### (Optional) Store models on another drive (e.g. D:)

Models are large (~4–5 GB each). To keep them off C:, set `OLLAMA_MODELS` **before** pulling:

```powershell
New-Item -ItemType Directory -Force "D:\OllamaModels"
[Environment]::SetEnvironmentVariable("OLLAMA_MODELS", "D:\OllamaModels", "User")
$env:OLLAMA_MODELS = "D:\OllamaModels"          # this session too
Get-Process ollama* -ErrorAction SilentlyContinue | Stop-Process -Force
# relaunch Ollama from the Start menu, then pull the models
```

### Notes & recommendations

- **Quality vs speed (CPU):** `qwen2.5:7b-instruct` (best balance) > `mistral:7b` > `llama3.1:8b`.
  Use `llama3.2:3b` / `qwen2.5:3b` for faster, lower-quality runs.
- The **first request after start is slow** (the model cold-loads into memory); later requests are
  faster (`keep_alive` keeps it warm).
- **Gemini** is faster and more accurate than local 7–8B models for messy documents — switch to it
  in the dropdown when you want the cleanest result.
- Other runtimes that expose an OpenAI/Ollama-compatible API (**LM Studio**, **llama.cpp**) can also
  serve these GGUF models locally.

---

## How it works

### PII masking
`RegexMaskingService` detects structured PII (EMAIL, SSN, PAN, AADHAAR, CARD, PASSPORT, DOB, PHONE),
replaces each with a type-tagged placeholder (`{{EMAIL_1}}`), and keeps a `placeholder → real value`
map. Only the **masked** text is sent to the model; the real values are restored (`Unmask`) in the
final JSON. *(Names/addresses are free-form and not masked — that would need an NER step.)*

### Confidence scores
The layout OCR step yields plain text (no per-field confidence), so confidence is the **model's own
self-assessment** per field (`confidenceScores`: 1.0 = explicit in the document, lower = inferred).
If the model is unavailable, the app falls back to Azure's OCR key-value pairs with Azure's confidence.

### Contact-field backfill
Small local models sometimes drop masked placeholders. After extraction, `email` / `phone` /
`linkedIn` are re-filled from the original OCR text via regex if the model returned null — so they
always appear when present.

### Validation
`ResultValidator` checks formats (email/phone/date), cross-field rules (invoice totals, passport
date order), required fields, and low-confidence values, surfacing them in the "needs review" panel.

---

## Project structure

| Path | Purpose |
|---|---|
| `Program.cs` | DI wiring: Azure, masking, validator, per-model Ollama providers + Gemini, factory |
| `Components/Pages/Home.razor` | Upload → preview + JSON page, toasts, spinner, validation panel |
| `Components/App.razor` | Host page + client-side hover-zoom script |
| `Services/AzureDocumentExtractionService.cs` | Azure DI OCR + `:selected:` artifact cleanup |
| `Services/RegexMaskingService.cs` (`IMaskingService`) | Reversible PII masking |
| `Services/ILlmProvider.cs` | Provider contract + `LlmResult(Json, Error)` |
| `Services/OllamaProvider.cs` | One Ollama model exposed as a provider |
| `Services/GeminiProvider.cs` | Google Gemini provider (REST + API key) |
| `Services/LlmProviderFactory.cs` (`ILlmProviderFactory`) | Collects providers, runtime selection |
| `Services/OllamaModelOption.cs` | Config binding for `Ollama:Models` |
| `Services/ResultValidator.cs` (`IResultValidator`) | Post-extraction validation |
| `Models/JsonTemplates.cs` | Per-type target JSON schemas + the extraction prompt |
| `Models/FormSchema.cs`, `ExtractionResult.cs`, `ExtractedField.cs`, `MaskResult.cs` | Domain models |
| `wwwroot/app.css` | Styling (cards, chips, JSON highlighting, spinner, toasts, zoom) |

---

## Deployment

This is a **Blazor *Server*** app (persistent process + WebSocket/SignalR circuit), so it **cannot**
run on Vercel/serverless. Host it where a long-running .NET process + WebSockets are supported:

- **Azure App Service** (natural fit — same cloud as Document Intelligence)
- Azure Container Apps, Render, Railway, Fly.io (Docker), AWS App Runner, or a VPS/IIS

Set secrets as environment variables (double underscore = nested key), e.g.
`AzureDocumentIntelligence__ApiKey`, `Gemini__ApiKey`, `Ollama__BaseUrl`.

> **Cloud + Ollama caveat:** Ollama runs on *your* machine. In the cloud, only **Gemini** works
> unless you also host an Ollama server (GPU) and point `Ollama:BaseUrl` at it.

---

## Security notes

- API keys belong in **user-secrets** (dev) or **environment variables** (prod) — never in source.
- PII is masked before any text reaches an LLM, and nothing is persisted.
- Only genuine, non-empty PDF/image files (validated by extension **and** magic bytes) are processed.
