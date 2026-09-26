"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { Field } from "@/components/field";

export default function RegisterPage() {
  const { registerStore } = useAuth();
  const router = useRouter();
  const [storeName, setStoreName] = useState("");
  const [slug, setSlug] = useState("");
  const [adminEmail, setAdminEmail] = useState("");
  const [adminPassword, setAdminPassword] = useState("");
  const [adminDisplayName, setAdminDisplayName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await registerStore({ storeName, slug, adminEmail, adminPassword, adminDisplayName });
      router.push("/products");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong. Please try again.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="flex flex-1 items-center justify-center p-6">
      <div className="w-full max-w-sm rounded-xl border border-border bg-surface shadow-card p-6 shadow-sm">
        <h1 className="mb-1 text-xl font-semibold">Register your store</h1>
        <p className="mb-6 text-sm text-muted">Creates your store and its first admin account.</p>

        <form onSubmit={handleSubmit} className="space-y-4">
          <Field label="Store name" value={storeName} onChange={setStoreName} placeholder="Sharma General Store" />
          <Field label="Store slug" value={slug} onChange={setSlug} placeholder="sharma-general" />
          <Field label="Your name" value={adminDisplayName} onChange={setAdminDisplayName} placeholder="Ramesh Sharma" />
          <Field label="Email" type="email" value={adminEmail} onChange={setAdminEmail} placeholder="owner@example.com" />
          <Field label="Password" type="password" value={adminPassword} onChange={setAdminPassword} placeholder="At least 8 characters" />

          {error && <p className="text-sm text-danger">{error}</p>}

          <button
            type="submit"
            disabled={isSubmitting}
            className="w-full rounded-md bg-brand px-4 py-2 text-sm font-medium text-brand-foreground hover:bg-brand-hover disabled:opacity-50"
          >
            {isSubmitting ? "Creating your store…" : "Create store"}
          </button>
        </form>

        <p className="mt-4 text-center text-sm text-muted">
          Already have a store?{" "}
          <Link href="/login" className="font-medium text-foreground underline">
            Sign in
          </Link>
        </p>
      </div>
    </main>
  );
}
