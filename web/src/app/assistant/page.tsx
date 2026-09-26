"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { NavBar } from "@/components/nav-bar";
import { AlertIcon, ArrowRightIcon, FileIcon, SparkIcon } from "@/components/ui/icons";
import { Badge, Button, Card, Input, PageShell, cn } from "@/components/ui/primitives";
import type { AssistantAnswer } from "@/lib/types";

type Turn = { question: string; answer: AssistantAnswer } | { question: string; error: string };

const SUGGESTIONS = [
  "What's low on stock?",
  "How did we do yesterday?",
  "What's our return policy?",
  "Which purchase orders are still open?",
];

export default function AssistantPage() {
  const { accessToken, isLoading, authFetch } = useAuth();
  const router = useRouter();
  const bottomRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const [question, setQuestion] = useState("");
  const [turns, setTurns] = useState<Turn[]>([]);
  const [isAsking, setIsAsking] = useState(false);

  useEffect(() => {
    if (!isLoading && !accessToken) router.push("/login");
  }, [isLoading, accessToken, router]);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [turns, isAsking]);

  if (isLoading || !accessToken) return null;

  async function ask(text: string) {
    const q = text.trim();
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
      inputRef.current?.focus();
    }
  }

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    void ask(question);
  }

  return (
    <>
      <NavBar />
      <PageShell width="narrow">
        <div className="flex min-h-[calc(100vh-10rem)] flex-col">
          <div className="mb-6 flex items-center gap-3">
            <span className="flex size-9 items-center justify-center rounded-xl bg-brand-soft text-brand-soft-foreground">
              <SparkIcon className="size-5" />
            </span>
            <div>
              <h1 className="text-lg font-semibold tracking-tight">Assistant</h1>
              <p className="text-xs text-muted">
                Answers from your live data and uploaded documents — limited to your own permissions.
              </p>
            </div>
          </div>

          <div className="flex-1 space-y-5">
            {turns.length === 0 && !isAsking && (
              <div className="animate-[--animate-fade-up]">
                <p className="mb-3 text-xs font-medium tracking-wide text-subtle uppercase">Try asking</p>
                <div className="grid gap-2 sm:grid-cols-2">
                  {SUGGESTIONS.map((s) => (
                    <button
                      key={s}
                      onClick={() => void ask(s)}
                      className="group flex items-center justify-between gap-3 rounded-xl border border-border bg-surface px-4 py-3 text-left text-sm shadow-card transition-colors hover:border-border-strong hover:bg-surface-hover"
                    >
                      <span>{s}</span>
                      <ArrowRightIcon className="size-4 shrink-0 text-subtle transition-transform group-hover:translate-x-0.5" />
                    </button>
                  ))}
                </div>
              </div>
            )}

            {turns.map((turn, i) => (
              <div key={i} className="animate-[--animate-fade-up] space-y-2.5">
                <div className="flex justify-end">
                  <p className="max-w-[85%] rounded-2xl rounded-br-sm bg-brand px-4 py-2.5 text-sm text-brand-foreground">
                    {turn.question}
                  </p>
                </div>

                {"error" in turn ? (
                  <div className="flex max-w-[85%] items-start gap-2 rounded-2xl rounded-bl-sm bg-danger-soft px-4 py-2.5 text-sm text-danger-soft-foreground">
                    <AlertIcon className="mt-0.5 size-4 shrink-0" />
                    <span>{turn.error}</span>
                  </div>
                ) : (
                  <Card className="max-w-[92%] overflow-hidden">
                    <p className="px-4 py-3 text-sm leading-relaxed whitespace-pre-wrap">{turn.answer.text}</p>

                    {(turn.answer.toolsUsed.length > 0 || turn.answer.citations.length > 0) && (
                      <div className="space-y-2.5 border-t border-border bg-surface-muted px-4 py-3">
                        {turn.answer.toolsUsed.length > 0 && (
                          <div className="flex flex-wrap items-center gap-1.5">
                            <span className="text-[11px] text-subtle">Used</span>
                            {turn.answer.toolsUsed.map((tool) => (
                              <Badge key={tool} tone="brand">
                                <code className="font-mono">{tool}</code>
                              </Badge>
                            ))}
                          </div>
                        )}

                        {turn.answer.citations.length > 0 && (
                          <div className="space-y-1.5">
                            <span className="text-[11px] text-subtle">Sources</span>
                            {turn.answer.citations.map((c, ci) => (
                              <div key={ci} className="flex gap-2 rounded-lg bg-surface px-3 py-2">
                                <FileIcon className="mt-0.5 size-3.5 shrink-0 text-subtle" />
                                <div className="min-w-0">
                                  <p className="truncate text-xs font-medium">{c.fileName}</p>
                                  <p className="mt-0.5 line-clamp-2 text-[11px] text-muted">{c.snippet}</p>
                                </div>
                              </div>
                            ))}
                          </div>
                        )}
                      </div>
                    )}
                  </Card>
                )}
              </div>
            ))}

            {isAsking && (
              <div className="flex items-center gap-2 text-sm text-muted">
                {[0, 1, 2].map((i) => (
                  <span
                    key={i}
                    className="size-1.5 animate-[--animate-shimmer] rounded-full bg-subtle"
                    style={{ animationDelay: `${i * 160}ms` }}
                  />
                ))}
                <span className="text-xs">Thinking…</span>
              </div>
            )}

            <div ref={bottomRef} />
          </div>

          {/* Sticks to the bottom of the viewport so the composer stays
              reachable in a long conversation. */}
          <form
            onSubmit={handleSubmit}
            className="sticky bottom-0 mt-6 flex gap-2 bg-background/80 py-3 backdrop-blur-xl"
          >
            <Input
              ref={inputRef}
              value={question}
              onChange={(e) => setQuestion(e.target.value)}
              placeholder="Ask about inventory, sales, or your documents…"
              disabled={isAsking}
              aria-label="Ask the assistant"
              className={cn("h-11", isAsking && "opacity-60")}
            />
            <Button type="submit" size="lg" disabled={isAsking || !question.trim()}>
              Ask
            </Button>
          </form>
        </div>
      </PageShell>
    </>
  );
}
