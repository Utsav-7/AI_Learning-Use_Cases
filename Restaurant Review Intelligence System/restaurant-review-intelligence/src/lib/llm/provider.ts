export type { SummaryResult } from "../prompts";

export interface LLMProvider {
  analyze(reviews: string[], date: string): Promise<import("../prompts").SummaryResult>;
}
