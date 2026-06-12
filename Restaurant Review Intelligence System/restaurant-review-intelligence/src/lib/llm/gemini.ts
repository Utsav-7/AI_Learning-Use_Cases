import { LLMProvider, SummaryResult } from "./provider";
import { buildPrompt, parseResponse } from "../prompts";

export class GeminiProvider implements LLMProvider {
  private apiKey: string;
  private model: string;

  constructor(model?: string) {
    this.apiKey = process.env.GEMINI_API_KEY || "";
    this.model = model || process.env.GEMINI_MODEL || "gemini-2.5-flash";
  }

  async analyze(reviews: string[], date: string): Promise<SummaryResult> {
    if (!this.apiKey) throw new Error("GEMINI_API_KEY is not set in .env");

    const prompt = buildPrompt(reviews, date);

    const res = await fetch(
      `https://generativelanguage.googleapis.com/v1beta/models/${this.model}:generateContent?key=${this.apiKey}`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          contents: [{ parts: [{ text: prompt }] }],
          generationConfig: { responseMimeType: "application/json" },
        }),
      }
    );

    if (!res.ok) throw new Error(`Gemini error: ${res.status} ${res.statusText}`);

    const data = await res.json();
    const text = data.candidates?.[0]?.content?.parts?.[0]?.text || "{}";
    return parseResponse(text, reviews.length);
  }
}
