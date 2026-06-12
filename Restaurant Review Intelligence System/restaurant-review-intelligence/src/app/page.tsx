"use client";

import { useState, useEffect } from "react";
import { SummaryResult } from "@/lib/llm/provider";

type SummaryResponse = SummaryResult & { provider: string };

interface Review {
  id: number;
  reviewer: string;
  review: string;
  rating: number;
  date: string;
}

interface ModelOption {
  label: string;
  value: string;
  provider: string;
}

/* ---------- helpers ---------- */

function formatTimestamp(d: Date): string {
  const dd = String(d.getDate()).padStart(2, "0");
  const mm = String(d.getMonth() + 1).padStart(2, "0");
  const yyyy = d.getFullYear();
  const hh = String(d.getHours()).padStart(2, "0");
  const min = String(d.getMinutes()).padStart(2, "0");
  const ss = String(d.getSeconds()).padStart(2, "0");
  return `${dd}/${mm}/${yyyy} ${hh}:${min}:${ss}`;
}

function StarRating({ rating }: { rating: number }) {
  return (
    <div className="flex items-center gap-0.5">
      {[1, 2, 3, 4, 5].map((s) => (
        <svg
          key={s}
          className={`w-3.5 h-3.5 ${s <= Math.round(rating) ? "text-amber-400" : "text-gray-200"}`}
          fill="currentColor"
          viewBox="0 0 20 20"
        >
          <path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z" />
        </svg>
      ))}
      <span className="text-xs text-gray-500 ml-1">{rating.toFixed(1)}</span>
    </div>
  );
}

function PriorityBadge({ priority }: { priority: string }) {
  const styles: Record<string, string> = {
    high: "bg-red-100 text-red-700",
    medium: "bg-yellow-100 text-yellow-700",
    low: "bg-gray-100 text-gray-500",
  };
  return (
    <span className={`text-[10px] font-semibold px-2 py-0.5 rounded uppercase tracking-wide ${styles[priority] ?? styles.low}`}>
      {priority}
    </span>
  );
}

/* ---------- Review Modal ---------- */

function ReviewModal({ review, onClose }: { review: Review; onClose: () => void }) {
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      onClick={onClose}
    >
      <div
        className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 space-y-4"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-start justify-between gap-3">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-full bg-emerald-100 text-emerald-700 flex items-center justify-center text-sm font-bold shrink-0">
              {review.reviewer.charAt(0).toUpperCase()}
            </div>
            <div>
              <p className="font-semibold text-gray-900 text-sm">{review.reviewer}</p>
              <StarRating rating={review.rating} />
            </div>
          </div>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 transition-colors mt-0.5"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <p className="text-sm text-gray-700 leading-relaxed border-t border-gray-100 pt-4">
          {review.review}
        </p>
      </div>
    </div>
  );
}

/* ---------- Main Page ---------- */

export default function Home() {
  const [date, setDate] = useState("");
  const [selectedModel, setSelectedModel] = useState<ModelOption | null>(null);
  const [modelOptions, setModelOptions] = useState<ModelOption[]>([]);
  const [modelsLoading, setModelsLoading] = useState(true);

  const [loading, setLoading] = useState(false);
  const [summary, setSummary] = useState<SummaryResponse | null>(null);
  const [reviews, setReviews] = useState<Review[]>([]);
  const [logs, setLogs] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [activeReview, setActiveReview] = useState<Review | null>(null);

  /* fetch available models on mount */
  useEffect(() => {
    fetch("/api/models")
      .then((r) => r.json())
      .then((data) => {
        const ollama: ModelOption[] = data.ollama ?? [];
        const gemini: ModelOption[] = data.gemini ?? [];
        const all = [...ollama, ...gemini];
        setModelOptions(all);
        if (all.length > 0) setSelectedModel(all[0]);
      })
      .catch(() => {
        const fallback: ModelOption[] = [
          { label: "Gemini 2.5 Flash", value: "gemini-2.5-flash", provider: "gemini" },
        ];
        setModelOptions(fallback);
        setSelectedModel(fallback[0]);
      })
      .finally(() => setModelsLoading(false));
  }, []);

  function addLog(msg: string) {
    setLogs((prev) => [...prev, `[${formatTimestamp(new Date())}] ${msg}`]);
  }

  async function handleGenerate() {
    if (!date || !selectedModel) return;
    setLoading(true);
    setError(null);
    setSummary(null);
    setReviews([]);
    setLogs([]);

    try {
      addLog(`Fetching reviews for ${date}...`);

      const reviewRes = await fetch(`/api/reviews?date=${date}`);
      const reviewData = await reviewRes.json();

      if (!reviewRes.ok) {
        setError(reviewData.error || "No reviews found for this date");
        addLog(`Error: ${reviewData.error}`);
        return;
      }

      setReviews(reviewData.reviews);
      addLog(`Found ${reviewData.total} reviews`);
      addLog(`Sending to ${selectedModel.label}...`);
      addLog(`Analyzing ${reviewData.total} reviews — this may take a moment...`);

      const params = new URLSearchParams({
        date,
        provider: selectedModel.provider,
        model: selectedModel.value,
      });

      const summaryRes = await fetch(`/api/summary?${params}`);
      const summaryData = await summaryRes.json();

      if (!summaryRes.ok) {
        setError(summaryData.error || "Analysis failed");
        addLog(`Error: ${summaryData.error}`);
        return;
      }

      setSummary(summaryData);
      addLog(`Summary generated successfully`);
    } catch {
      setError("Failed to reach the server");
      addLog("Error: Could not reach the server");
    } finally {
      setLoading(false);
    }
  }

  const hasResults = summary && reviews.length > 0;

  /* group model options for <select> */
  const ollamaOptions = modelOptions.filter((m) => m.provider === "ollama");
  const geminiOptions = modelOptions.filter((m) => m.provider === "gemini");

  return (
    <div className="min-h-screen bg-white">
      <main className="max-w-7xl mx-auto px-6 pt-8 pb-12 space-y-6">
        {/* Header — matches reference: large bold title + gray subtitle, no bar */}
        <div className="pb-2">
          <h1 className="text-2xl font-bold text-gray-900">Restaurant Review Intelligence</h1>
          <p className="text-sm text-gray-500 mt-1">Select a date to analyze reviews and generate an AI-powered daily summary.</p>
        </div>
        {/* Controls card */}
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
          <h2 className="text-sm font-semibold text-gray-700 mb-4">Generate Daily Summary</h2>
          <div className="flex flex-wrap items-end gap-4">
            {/* Date */}
            <div className="flex flex-col gap-1">
              <label className="text-xs font-medium text-gray-500">Date</label>
              <input
                type="date"
                value={date}
                onChange={(e) => setDate(e.target.value)}
                min="2025-06-13"
                max="2026-06-12"
                className="border border-gray-300 text-gray-800 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
              />
            </div>

            {/* Model selector */}
            <div className="flex flex-col gap-1">
              <label className="text-xs font-medium text-gray-500">Model</label>
              <select
                disabled={modelsLoading}
                value={selectedModel ? `${selectedModel.provider}::${selectedModel.value}` : ""}
                onChange={(e) => {
                  const found = modelOptions.find(
                    (m) => `${m.provider}::${m.value}` === e.target.value
                  );
                  if (found) setSelectedModel(found);
                }}
                className="border border-gray-300 text-gray-800 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-transparent min-w-[200px] disabled:bg-gray-100 disabled:text-gray-400"
              >
                {modelsLoading && <option>Loading models...</option>}

                {ollamaOptions.length > 0 && (
                  <optgroup label="Ollama — Local">
                    {ollamaOptions.map((m) => (
                      <option key={m.value} value={`${m.provider}::${m.value}`}>
                        {m.label}
                      </option>
                    ))}
                  </optgroup>
                )}

                {geminiOptions.length > 0 && (
                  <optgroup label="Gemini — Cloud">
                    {geminiOptions.map((m) => (
                      <option key={m.value} value={`${m.provider}::${m.value}`}>
                        {m.label}
                      </option>
                    ))}
                  </optgroup>
                )}
              </select>
            </div>

            {/* Button */}
            <button
              onClick={handleGenerate}
              disabled={!date || !selectedModel || loading || modelsLoading}
              className="flex items-center gap-2 bg-emerald-600 hover:bg-emerald-700 disabled:bg-gray-200 disabled:text-gray-400 text-white font-medium rounded-lg px-5 py-2 text-sm transition-colors"
            >
              {loading && (
                <svg className="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                </svg>
              )}
              {loading ? "Analyzing..." : "Generate Summary"}
            </button>
          </div>
        </div>

        {/* Error */}
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-4 py-3 text-sm">
            {error}
          </div>
        )}

        {/* Processing log */}
        {logs.length > 0 && (
          <div className="bg-gray-900 rounded-xl border border-gray-700 p-4">
            <p className="text-xs font-medium text-gray-400 uppercase tracking-wide mb-2">
              Processing Log
            </p>
            <div className="space-y-1 font-mono text-xs">
              {logs.map((log, i) => (
                <p
                  key={i}
                  className={
                    log.includes("Error")
                      ? "text-red-400"
                      : log.includes("successfully")
                      ? "text-emerald-400"
                      : "text-gray-300"
                  }
                >
                  {log}
                </p>
              ))}
              {loading && (
                <p className="text-yellow-400 animate-pulse">processing...</p>
              )}
            </div>
          </div>
        )}

        {/* Results */}
        {hasResults && (
          <>
            {/* Side by side: summary + reviews */}
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 items-start">
              {/* Daily Summary — dark terminal card */}
              <div className="bg-gray-900 rounded-xl border border-gray-700 p-6 space-y-5">
                <div>
                  <h2 className="text-white font-bold text-base">Daily Summary</h2>
                  <p className="text-gray-500 text-xs mt-0.5">{summary.provider}</p>
                </div>

                <div className="font-mono text-sm space-y-4">
                  {/* Key stats */}
                  <div className="grid grid-cols-2 gap-x-4 gap-y-1">
                    <p className="text-emerald-400">Reviews Processed: {summary.reviewsProcessed}</p>
                    <p className="text-emerald-400">Overall Score: {summary.overallScore}/100</p>
                    <p className="text-emerald-400">Sentiment: {summary.overallSentiment}</p>
                    <p className="text-emerald-400">Confidence: {summary.analysisConfidence}%</p>
                  </div>

                  {/* Management summary */}
                  {summary.managementSummary && (
                    <div>
                      <p className="text-emerald-400 font-semibold mb-1">Management Summary:</p>
                      <p className="text-gray-300 text-xs leading-relaxed">{summary.managementSummary}</p>
                    </div>
                  )}

                  {/* Positive themes */}
                  <div>
                    <p className="text-emerald-400 font-semibold mb-1">Positive Themes:</p>
                    {summary.positiveThemes.map((t, i) => (
                      <p key={i} className="text-emerald-400">
                        - {t.theme}
                        <span className="text-gray-500 text-xs ml-2">({t.category}, {t.frequency}x)</span>
                      </p>
                    ))}
                  </div>

                  {/* Negative themes */}
                  <div>
                    <p className="text-emerald-400 font-semibold mb-1">Negative Themes:</p>
                    {summary.negativeThemes.map((t, i) => (
                      <p key={i} className="text-emerald-400">
                        - {t.theme}
                        <span className="text-gray-500 text-xs ml-2">({t.category}, {t.frequency}x)</span>
                      </p>
                    ))}
                  </div>

                  {/* Recurring complaints */}
                  {summary.recurringComplaints.length > 0 && (
                    <div>
                      <p className="text-emerald-400 font-semibold mb-1">Recurring Complaints:</p>
                      {summary.recurringComplaints.map((c, i) => (
                        <p key={i} className="text-emerald-400">- {c}</p>
                      ))}
                    </div>
                  )}

                  {/* Notable quotes */}
                  {summary.notableQuotes.length > 0 && (
                    <div>
                      <p className="text-emerald-400 font-semibold mb-1">Notable Quotes:</p>
                      {summary.notableQuotes.map((q, i) => (
                        <p key={i} className="text-gray-400 italic text-xs">"{q}"</p>
                      ))}
                    </div>
                  )}
                </div>

                {/* Category score bars */}
                <div>
                  <p className="text-emerald-400 font-mono text-sm font-semibold mb-3">Category Scores:</p>
                  <div className="space-y-2">
                    {Object.entries(summary.categoryScores).map(([cat, score]) =>
                      score !== null ? (
                        <div key={cat}>
                          <div className="flex justify-between font-mono text-xs text-gray-400 mb-1">
                            <span>{cat}</span>
                            <span>{score}/100</span>
                          </div>
                          <div className="w-full bg-gray-700 rounded-full h-1.5">
                            <div
                              className="bg-emerald-500 h-1.5 rounded-full"
                              style={{ width: `${score}%` }}
                            />
                          </div>
                        </div>
                      ) : null
                    )}
                  </div>
                </div>
              </div>

              {/* Reviews list */}
              <div className="bg-white rounded-xl border border-gray-200 shadow-sm flex flex-col">
                <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
                  <h2 className="text-sm font-semibold text-gray-800">Reviews</h2>
                  <span className="bg-emerald-100 text-emerald-700 text-xs font-medium px-2.5 py-0.5 rounded-full">
                    {reviews.length} total
                  </span>
                </div>
                <p className="text-xs text-gray-400 px-5 pt-3 pb-1">Click a review to read in full</p>
                <div className="overflow-y-auto max-h-[600px] divide-y divide-gray-100">
                  {reviews.map((r) => (
                    <button
                      key={r.id}
                      onClick={() => setActiveReview(r)}
                      className="w-full text-left px-5 py-4 hover:bg-gray-50 transition-colors"
                    >
                      <div className="flex items-center justify-between gap-2 mb-1.5">
                        <div className="flex items-center gap-2">
                          <div className="w-7 h-7 rounded-full bg-emerald-100 text-emerald-700 flex items-center justify-center text-xs font-bold shrink-0">
                            {r.reviewer.charAt(0).toUpperCase()}
                          </div>
                          <span className="text-sm font-medium text-gray-800 truncate">{r.reviewer}</span>
                        </div>
                        <StarRating rating={r.rating} />
                      </div>
                      <p className="text-xs text-gray-500 leading-relaxed ml-9 line-clamp-2">{r.review}</p>
                    </button>
                  ))}
                </div>
              </div>
            </div>

            {/* Action items — full width */}
            {summary.actionItems.length > 0 && (
              <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5">
                <h2 className="text-sm font-semibold text-gray-800 mb-4">Action Items</h2>
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                  {summary.actionItems.map((a, i) => (
                    <div key={i} className="border border-gray-100 rounded-lg p-3 space-y-2">
                      <div className="flex items-center justify-between">
                        <span className="text-xs text-gray-400">{a.category}</span>
                        <PriorityBadge priority={a.priority} />
                      </div>
                      <p className="text-sm text-gray-800">{a.action}</p>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </>
        )}
      </main>

      {/* Review modal */}
      {activeReview && (
        <ReviewModal review={activeReview} onClose={() => setActiveReview(null)} />
      )}
    </div>
  );
}
