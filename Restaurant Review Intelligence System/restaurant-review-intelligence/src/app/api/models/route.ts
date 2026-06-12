import { NextResponse } from "next/server";

export async function GET() {
  const ollamaUrl = process.env.OLLAMA_URL || "http://localhost:11434";

  const ollamaModels: { label: string; value: string; provider: string }[] = [];

  try {
    const res = await fetch(`${ollamaUrl}/api/tags`, {
      signal: AbortSignal.timeout(3000),
    });
    if (res.ok) {
      const data = await res.json();
      for (const m of data.models ?? []) {
        ollamaModels.push({
          label: m.name,
          value: m.name,
          provider: "ollama",
        });
      }
    }
  } catch {
    // Ollama not running — return empty list; UI will show fallback
  }

  const gemini = [
    { label: "Gemini 2.5 Flash", value: "gemini-2.5-flash", provider: "gemini" },
  ];

  return NextResponse.json({ ollama: ollamaModels, gemini });
}
