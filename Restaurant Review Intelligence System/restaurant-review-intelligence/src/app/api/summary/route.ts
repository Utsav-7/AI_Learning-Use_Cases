import { NextRequest, NextResponse } from "next/server";
import { prisma } from "@/lib/db";
import { OllamaProvider } from "@/lib/llm/ollama";
import { GeminiProvider } from "@/lib/llm/gemini";

export async function GET(req: NextRequest) {
  const { searchParams } = new URL(req.url);
  const date = searchParams.get("date");
  const provider = searchParams.get("provider") || process.env.LLM_PROVIDER || "ollama";
  const model = searchParams.get("model") || undefined;

  if (!date) {
    return NextResponse.json(
      { error: "date param required (YYYY-MM-DD)" },
      { status: 400 }
    );
  }

  const start = new Date(`${date}T00:00:00.000Z`);
  const end = new Date(`${date}T23:59:59.999Z`);

  const rows = await prisma.review.findMany({
    where: { date: { gte: start, lte: end } },
    select: { review: true },
  });

  if (rows.length === 0) {
    return NextResponse.json(
      { error: `No reviews found for ${date}` },
      { status: 404 }
    );
  }

  const reviews = rows.map((r) => r.review);
  const llm =
    provider === "gemini"
      ? new GeminiProvider(model)
      : new OllamaProvider(model);

  try {
    const summary = await llm.analyze(reviews, date);
    const displayLabel =
      provider === "gemini"
        ? `Gemini (${model ?? "gemini-2.5-flash"})`
        : `Ollama (${model ?? "qwen2.5:7b-instruct"})`;
    return NextResponse.json({ ...summary, provider: displayLabel });
  } catch (err) {
    const message = err instanceof Error ? err.message : "LLM error";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
