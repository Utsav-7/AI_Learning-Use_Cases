import { LLMProvider, SummaryResult } from "./provider";
import { buildPrompt, parseResponse } from "../prompts";

export class OllamaProvider implements LLMProvider {
  private baseUrl: string;
  private model: string;

  constructor(model?: string) {
    this.baseUrl = process.env.OLLAMA_URL || "http://localhost:11434";
    this.model = model || process.env.OLLAMA_MODEL || "qwen2.5:7b-instruct";
  }

  async analyze(reviews: string[], date: string): Promise<SummaryResult> {
    const prompt = buildPrompt(reviews, date);

    const res = await fetch(`${this.baseUrl}/api/generate`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        model: this.model,
        prompt,
        stream: false,
        format: "json",
      }),
    });

    if (!res.ok) throw new Error(`Ollama error: ${res.status} ${res.statusText}`);

    const data = await res.json();
    return parseResponse(data.response, reviews.length);
  }
}
