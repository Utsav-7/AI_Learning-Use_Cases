# Restaurant Review Intelligence System

An AI-powered restaurant feedback analyzer built with Next.js. Select a date, choose a local or cloud AI model, and get an instant structured daily summary of customer reviews — including sentiment, themes, recurring complaints, category scores, and actionable recommendations.

---

## Features

- **Date-based review analysis** — pick any date in the seeded range to analyze all reviews for that day
- **Dual AI provider support** — run analysis locally with Ollama or via Gemini API
- **Dynamic model selector** — auto-detects all locally installed Ollama models (Llama, Mistral, Qwen, etc.)
- **Structured output with Zod validation** — every LLM response is schema-validated and retried on failure
- **Daily summary** — overall score, sentiment, confidence, management summary, positive/negative themes, recurring complaints, notable quotes
- **Category scores** — Food Quality, Service, Cleanliness, Ambience, Pricing with progress bars
- **Action items** — prioritized (high / medium / low) with category labels
- **Reviews panel** — scrollable list of all reviews for the selected date; click any review to read in full
- **Processing log** — timestamped log (`DD/MM/YYYY HH:MM:SS`) showing each step of the analysis pipeline

---

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | Next.js 15 (App Router, TypeScript) |
| Database | PostgreSQL via Neon (serverless, free tier) |
| ORM | Prisma 5 |
| LLM — Local | Ollama (any installed model) |
| LLM — Cloud | Google Gemini 2.5 Flash |
| Validation | Zod |
| Styling | Tailwind CSS |

---

## Project Structure

```
restaurant-review-intelligence/
├── prisma/
│   └── schema.prisma           # Review model
├── src/
│   ├── app/
│   │   ├── api/
│   │   │   ├── models/route.ts     # GET — list installed Ollama models + Gemini
│   │   │   ├── reviews/route.ts    # GET — fetch reviews by date
│   │   │   └── summary/route.ts    # GET — run LLM analysis, return summary
│   │   ├── page.tsx                # Main dashboard UI
│   │   ├── layout.tsx
│   │   └── globals.css
│   └── lib/
│       ├── db.ts                   # Prisma client singleton
│       ├── prompts.ts              # LLM prompt builder + Zod schema + parser
│       └── llm/
│           ├── provider.ts         # Shared interface + type re-export
│           ├── ollama.ts           # Ollama provider
│           └── gemini.ts           # Gemini provider
└── seed/
    ├── seed.js                     # CSV → Neon import script
    └── package.json
```

---

## Prerequisites

- Node.js 18+
- A [Neon](https://neon.tech) account (free tier is enough)
- One of:
  - **Ollama** installed locally (for local AI)
  - **Gemini API key** (for cloud AI)

---

## Setup

### 1. Clone and install

```bash
git clone https://github.com/Utsav-7/AI_Learning-Use_Cases.git
cd "AI_Learning-Use_Cases/Restaurant Review Intelligence System/restaurant-review-intelligence"
npm install
```

### 2. Create your Neon database

1. Go to [neon.tech](https://neon.tech) and sign up (free)
2. Create a new project → name it `restaurant-review`
3. In the dashboard, click **Connection Details** → select **Node.js**
4. Copy the connection string

### 3. Configure environment variables

Create a `.env` file in the project root:

```env
# Neon PostgreSQL connection string
DATABASE_URL="postgresql://user:password@ep-xxx.us-east-1.aws.neon.tech/restaurantdb?sslmode=require"

# LLM Provider: "ollama" or "gemini" (used as default, overridable from UI)
LLM_PROVIDER="ollama"

# Ollama settings (if using local AI)
OLLAMA_URL="http://localhost:11434"
OLLAMA_MODEL="qwen2.5:7b-instruct"

# Gemini settings (if using cloud AI)
GEMINI_API_KEY="your-gemini-api-key"
GEMINI_MODEL="gemini-2.5-flash"
```

### 4. Generate Prisma client

```bash
npx prisma generate
```

### 5. Seed the database

The seed script imports the restaurant reviews CSV into Neon.

```bash
cd ../seed
npm install
```

Create a `.env` file inside `seed/`:

```env
DATABASE_URL="postgresql://your-neon-connection-string"
```

Then run:

```bash
node seed.js
```

Expected output:
```
Table ready.
Total rows in CSV: 7514
Max rows to insert: 5000
  Inserted 100 rows...
  ...
--- Done ---
Inserted : 5000
Skipped  : 42
```

### 6. Run the app

```bash
cd ../restaurant-review-intelligence
npm run dev
```

Open [http://localhost:3000](http://localhost:3000).

---

## Setting Up Local AI with Ollama

Ollama lets you run large language models entirely on your own machine — no API key, no internet required after the initial model download.

### Install Ollama

**Windows / macOS:**
Download and install from [ollama.com/download](https://ollama.com/download)

**Linux:**
```bash
curl -fsSL https://ollama.com/install.sh | sh
```

### Start the Ollama server

```bash
ollama serve
```

This starts the local API on `http://localhost:11434`. Keep this terminal open while the app is running.

### Pull a model

Choose one or more of the following (smaller = faster, requires less RAM):

| Model | Command | RAM Required | Notes |
|---|---|---|---|
| Qwen 2.5 7B | `ollama pull qwen2.5:7b-instruct` | ~5 GB | Best for structured JSON output |
| Llama 3.1 8B | `ollama pull llama3.1:8b` | ~5 GB | Strong general reasoning |
| Mistral 7B | `ollama pull mistral:7b` | ~5 GB | Fast, good instruction following |
| Llama 3.2 3B | `ollama pull llama3.2:3b` | ~2 GB | Lightweight, lower accuracy |

```bash
# Example — pull Qwen (recommended for this project)
ollama pull qwen2.5:7b-instruct
```

### Verify models are installed

```bash
ollama list
```

The app's model dropdown automatically fetches this list from Ollama on page load — no config change needed.

### Using Ollama in the app

1. Make sure `ollama serve` is running
2. Open the app at [http://localhost:3000](http://localhost:3000)
3. The model dropdown will show all installed models under **Ollama — Local**
4. Select a date and click **Generate Summary**

> First run may be slow as the model loads into memory. Subsequent runs on the same model are faster.

---

## Using Gemini (Cloud AI)

1. Go to [aistudio.google.com](https://aistudio.google.com) and sign in
2. Click **Get API key** → **Create API key**
3. Copy the key and add it to your `.env`:
   ```env
   GEMINI_API_KEY="your-key-here"
   ```
4. Restart the dev server
5. Select **Gemini 2.5 Flash** from the model dropdown in the app

---

## API Reference

### `GET /api/models`
Returns all available models grouped by provider.

```json
{
  "ollama": [
    { "label": "qwen2.5:7b-instruct", "value": "qwen2.5:7b-instruct", "provider": "ollama" }
  ],
  "gemini": [
    { "label": "Gemini 2.5 Flash", "value": "gemini-2.5-flash", "provider": "gemini" }
  ]
}
```

### `GET /api/reviews?date=YYYY-MM-DD`
Returns all reviews stored for the given date.

```json
{
  "reviews": [
    { "id": 1, "reviewer": "John D.", "review": "Great food!", "rating": 5.0, "date": "..." }
  ],
  "total": 18
}
```

### `GET /api/summary?date=YYYY-MM-DD&provider=ollama&model=qwen2.5:7b-instruct`
Fetches reviews for the date, runs LLM analysis, and returns a structured summary.

**Query params:**
| Param | Required | Description |
|---|---|---|
| `date` | Yes | Date in `YYYY-MM-DD` format |
| `provider` | No | `ollama` (default) or `gemini` |
| `model` | No | Model name (e.g. `qwen2.5:7b-instruct`) |

**Response:**
```json
{
  "reviewsProcessed": 18,
  "overallSentiment": "positive",
  "overallScore": 78,
  "analysisConfidence": 85,
  "managementSummary": "Customers are largely satisfied with food quality...",
  "positiveThemes": [
    { "theme": "Tasty food and presentation", "frequency": 12, "confidence": 90, "category": "Food Quality" }
  ],
  "negativeThemes": [
    { "theme": "Slow service during peak hours", "frequency": 5, "confidence": 80, "category": "Service" }
  ],
  "recurringComplaints": ["Long wait times on weekends"],
  "actionItems": [
    { "action": "Add weekend evening staff", "priority": "high", "category": "Service" }
  ],
  "categoryScores": {
    "Food Quality": 85,
    "Service": 60,
    "Cleanliness": 75,
    "Ambience": 80,
    "Pricing": 70
  },
  "notableQuotes": ["Best biryani I have had in years"],
  "provider": "Ollama (qwen2.5:7b-instruct)"
}
```

---

## Data Seeding

The seed script reads `Restaurant reviews.csv` (located at `D:\Restaurant reviews.csv` by default — update the path in `seed.js` if needed) and inserts up to 5000 rows into Neon.

- **Skips** rows where the review text is empty
- **Skips** rows with a non-numeric rating
- **Generates** a random date per review within the range `Jun 13, 2025 – Jun 12, 2026`
- **Stores** rating as a float

To change the CSV path, edit line 36 of `seed/seed.js`:
```js
const csvPath = "D:\\Restaurant reviews.csv";
```

---

## Troubleshooting

| Problem | Fix |
|---|---|
| `No reviews found for this date` | Pick a date in the range Jun 13 2025 – Jun 12 2026; run the seed script if the DB is empty |
| Ollama model not appearing in dropdown | Run `ollama serve` then `ollama pull <model>` and refresh the page |
| `GEMINI_API_KEY is not set` | Add the key to `.env` and restart `npm run dev` |
| Prisma connection error | Verify `DATABASE_URL` in `.env` matches your Neon connection string exactly |
| Analysis times out | Ollama model may still be loading into RAM; wait ~30s and retry |
