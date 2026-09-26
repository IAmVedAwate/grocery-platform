"use client";

import { Suspense, useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { apiFetch, ApiError } from "@/lib/api-client";
import { Field } from "@/components/field";

function ResetPasswordForm() {
  const router = useRouter();
  const token = useSearchParams().get("token") ?? "";
  const [newPassword, setNewPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await apiFetch("/api/v1/auth/reset-password", null, {
        method: "POST",
        body: JSON.stringify({ token, newPassword }),
      });
      router.push("/login");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "That reset link is invalid or expired.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="flex flex-1 items-center justify-center p-6">
      <div className="w-full max-w-sm rounded-xl border border-border bg-surface shadow-card p-6 shadow-sm">
        <h1 className="mb-1 text-xl font-semibold">Choose a new password</h1>
        <p className="mb-6 text-sm text-muted">At least 8 characters.</p>

        {!token ? (
          <p className="text-sm text-danger">Missing reset token — use the link from your reset email.</p>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4">
            <Field label="New password" type="password" value={newPassword} onChange={setNewPassword} />

            {error && <p className="text-sm text-danger">{error}</p>}

            <button
              type="submit"
              disabled={isSubmitting}
              className="w-full rounded-md bg-brand px-4 py-2 text-sm font-medium text-brand-foreground hover:bg-brand-hover disabled:opacity-50"
            >
              {isSubmitting ? "Saving…" : "Reset password"}
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

export default function ResetPasswordPage() {
  return (
    <Suspense>
      <ResetPasswordForm />
    </Suspense>
  );
}
