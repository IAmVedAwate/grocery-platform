"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { Field } from "@/components/field";

export default function LoginPage() {
  const { login } = useAuth();
  const router = useRouter();
  const [storeSlug, setStoreSlug] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await login(storeSlug, email, password);
      router.push("/products");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong. Please try again.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="flex flex-1 items-center justify-center p-6">
      <div className="w-full max-w-sm rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
        <h1 className="mb-1 text-xl font-semibold">Sign in to QuickStock</h1>
        <p className="mb-6 text-sm text-gray-500">Enter your store, email, and password.</p>

        <form onSubmit={handleSubmit} className="space-y-4">
          <Field label="Store slug" value={storeSlug} onChange={setStoreSlug} placeholder="sharma-general" />
          <Field label="Email" type="email" value={email} onChange={setEmail} placeholder="owner@example.com" />
          <Field label="Password" type="password" value={password} onChange={setPassword} />
          <p className="text-right text-sm">
            <Link href="/forgot-password" className="text-gray-500 underline">
              Forgot password?
            </Link>
          </p>

          {error && <p className="text-sm text-red-600">{error}</p>}

          <button
            type="submit"
            disabled={isSubmitting}
            className="w-full rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white hover:bg-gray-800 disabled:opacity-50"
          >
            {isSubmitting ? "Signing in…" : "Sign in"}
          </button>
        </form>

        <p className="mt-4 text-center text-sm text-gray-500">
          New store?{" "}
          <Link href="/register" className="font-medium text-gray-900 underline">
            Register here
          </Link>
        </p>
      </div>
    </main>
  );
}
