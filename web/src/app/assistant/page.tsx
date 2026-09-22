"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { NavBar } from "@/components/nav-bar";
import type { AssistantAnswer } from "@/lib/types";

type Turn = { question: string; answer: AssistantAnswer } | { question: string; error: string };

export default function AssistantPage() {
  const { accessToken, isLoading, authFetch } = useAuth();
  const router = useRouter();
  const bottomRef = useRef<HTMLDivElement>(null);

  const [question, setQuestion] = useState("");
  const [turns, setTurns] = useState<Turn[]>([]);
  const [isAsking, setIsAsking] = useState(false);

  useEffect(() => {
    if (!isLoading && !accessToken) router.push("/login");
  }, [isLoading, accessToken, router]);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [turns]);

  if (isLoading || !accessToken) return null;

  async function handleAsk(e: FormEvent) {
    e.preventDefault();
    const q = question.trim();
    if (!q || isAsking) return;

    setQuestion("");
    setIsAsking(true);
    try {
      const answer = await authFetch<AssistantAnswer>("/api/v1/assistant/ask", {
        method: "POST",
        body: JSON.stringify({ question: q }),
      });
      setTurns((prev) => [...prev, { question: q, answer }]);
    } catch (err) {
      const message = err instanceof ApiError ? err.message : "Something went wrong asking the assistant.";
      setTurns((prev) => [...prev, { question: q, error: message }]);
    } finally {
      setIsAsking(false);
    }
  }

  return (
    <>
      <NavBar />
      <main className="mx-auto flex w-full max-w-3xl flex-1 flex-col p-6">
        <h1 className="mb-1 text-xl font-semibold">Assistant</h1>
        <p className="mb-6 text-sm text-gray-500">
          Ask about live inventory, sales, purchasing, or anything in your uploaded documents. Every answer
          respects your own permissions — the assistant can&rsquo;t see or do anything you couldn&rsquo;t
          yourself.
        </p>

        <div className="flex-1 space-y-4">
          {turns.length === 0 && (
            <p className="text-sm text-gray-400">
              Try: &ldquo;What&rsquo;s low on stock?&rdquo; or &ldquo;What&rsquo;s our return policy?&rdquo;
            </p>
          )}

          {turns.map((turn, i) => (
            <div key={i} className="space-y-2">
              <div className="ml-auto max-w-[80%] rounded-lg bg-gray-900 px-4 py-2 text-sm text-white">
                {turn.question}
              </div>

              {"error" in turn ? (
                <div className="max-w-[80%] rounded-lg border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-600">
                  {turn.error}
                </div>
              ) : (
                <div className="max-w-[80%] rounded-lg border border-gray-200 bg-white px-4 py-2 text-sm text-gray-900">
                  <p className="whitespace-pre-wrap">{turn.answer.text}</p>

                  {turn.answer.toolsUsed.length > 0 && (
                    <p className="mt-2 text-xs text-gray-400">Used: {turn.answer.toolsUsed.join(", ")}</p>
                  )}

                  {turn.answer.citations.length > 0 && (
                    <div className="mt-2 border-t border-gray-100 pt-2 text-xs text-gray-500">
                      <p className="font-medium">Sources</p>
                      {turn.answer.citations.map((c, ci) => (
                        <p key={ci}>
                          {c.fileName}: &ldquo;{c.snippet}&rdquo;
                        </p>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>
          ))}

          {isAsking && <p className="text-sm text-gray-400">Thinking…</p>}
          <div ref={bottomRef} />
        </div>

        <form onSubmit={handleAsk} className="mt-6 flex gap-2">
          <input
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            placeholder="Ask a question…"
            disabled={isAsking}
            className="flex-1 rounded-md border border-gray-300 px-3 py-2 text-sm"
          />
          <button
            type="submit"
            disabled={isAsking || !question.trim()}
            className="rounded-md bg-gray-900 px-4 py-2 text-sm text-white disabled:opacity-50"
          >
            Ask
          </button>
        </form>
      </main>
    </>
  );
}
