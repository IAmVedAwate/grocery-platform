"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { apiFetch, ApiError } from "@/lib/api-client";
import { Field } from "@/components/field";

type ForgotPasswordResponse = { resetToken: string | null };

export default function ForgotPasswordPage() {
  const [storeSlug, setStoreSlug] = useState("");
  const [email, setEmail] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [devResetLink, setDevResetLink] = useState<string | null>(null);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      const result = await apiFetch<ForgotPasswordResponse>("/api/v1/auth/forgot-password", null, {
        method: "POST",
        body: JSON.stringify({ storeSlug, email }),
      });
      setSubmitted(true);
      // No email delivery exists yet (docs/checkpoints/skills-inventory.md) —
      // the API only ever includes this in Development. In every other
      // environment resetToken is always null, whether or not the account
      // exists, which is the whole point.
      if (result.resetToken) setDevResetLink(`/reset-password?token=${encodeURIComponent(result.resetToken)}`);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong. Please try again.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="flex flex-1 items-center justify-center p-6">
      <div className="w-full max-w-sm rounded-xl border border-border bg-surface shadow-card p-6 shadow-sm">
        <h1 className="mb-1 text-xl font-semibold">Reset your password</h1>
        <p className="mb-6 text-sm text-muted">Enter your store and email — we&apos;ll send you a reset link.</p>

        {submitted ? (
          <div className="space-y-3 text-sm">
            <p className="text-muted">If that account exists, a reset link has been sent.</p>
            {devResetLink && (
              <p className="rounded-md bg-amber-50 p-3 text-amber-800">
                Dev mode (no email delivery configured yet):{" "}
                <Link href={devResetLink} className="font-medium underline">
                  Open reset link
                </Link>
              </p>
            )}
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4">
            <Field label="Store slug" value={storeSlug} onChange={setStoreSlug} placeholder="sharma-general" />
            <Field label="Email" type="email" value={email} onChange={setEmail} placeholder="owner@example.com" />

            {error && <p className="text-sm text-danger">{error}</p>}

            <button
              type="submit"
              disabled={isSubmitting}
              className="w-full rounded-md bg-brand px-4 py-2 text-sm font-medium text-brand-foreground hover:bg-brand-hover disabled:opacity-50"
            >
              {isSubmitting ? "Sending…" : "Send reset link"}
            </button>
          </form>
        )}

        <p className="mt-4 text-center text-sm text-muted">
          <Link href="/login" className="font-medium text-foreground underline">
            Back to sign in
          </Link>
        </p>
      </div>
    </main>
  );
}
