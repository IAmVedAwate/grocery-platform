"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { BoxIcon, ChartIcon, LayersIcon, ScanIcon } from "@/components/ui/icons";
import { Alert, Button, Field } from "@/components/ui/primitives";

const HIGHLIGHTS = [
  { icon: ScanIcon, title: "Barcode-first checkout", body: "Scan straight into the cart — no menu digging between customers." },
  { icon: LayersIcon, title: "Stock that stays honest", body: "Every sale, receipt and adjustment moves inventory in one transaction." },
  { icon: ChartIcon, title: "Answers, not spreadsheets", body: "Ask what's low on stock or what sold yesterday, in plain English." },
];

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
      router.push("/checkout");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong. Please try again.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="flex flex-1 lg:grid lg:grid-cols-2">
      {/* Form column */}
      <div className="flex flex-1 items-center justify-center px-4 py-10 sm:px-6">
        <div className="w-full max-w-sm animate-[--animate-fade-up]">
          <Link href="/" className="mb-8 inline-flex items-center gap-2">
            <span className="flex size-8 items-center justify-center rounded-lg bg-brand text-brand-foreground shadow-card">
              <BoxIcon className="size-[18px]" />
            </span>
            <span className="text-base font-semibold tracking-tight">QuickStock</span>
          </Link>

          <h1 className="text-2xl font-semibold tracking-tight">Welcome back</h1>
          <p className="mt-1.5 mb-7 text-sm text-muted">Sign in to your store to keep selling.</p>

          <form onSubmit={handleSubmit} className="space-y-4">
            <Field
              label="Store"
              value={storeSlug}
              onChange={setStoreSlug}
              placeholder="sharma-general"
              autoComplete="organization"
            />
            <Field
              label="Email"
              type="email"
              value={email}
              onChange={setEmail}
              placeholder="owner@example.com"
              autoComplete="email"
            />
            <div>
              <Field
                label="Password"
                type="password"
                value={password}
                onChange={setPassword}
                autoComplete="current-password"
              />
              <p className="mt-1.5 text-right">
                <Link
                  href="/forgot-password"
                  className="text-xs text-muted underline-offset-2 hover:text-foreground hover:underline"
                >
                  Forgot password?
                </Link>
              </p>
            </div>

            {error && <Alert>{error}</Alert>}

            <Button type="submit" size="lg" disabled={isSubmitting} className="w-full">
              {isSubmitting ? "Signing in…" : "Sign in"}
            </Button>
          </form>

          <p className="mt-6 text-center text-sm text-muted">
            New store?{" "}
            <Link href="/register" className="font-medium text-foreground underline underline-offset-2">
              Create an account
            </Link>
          </p>
        </div>
      </div>

      {/* Brand column — hidden on mobile, where it would just push the form
          below the fold. */}
      <aside className="relative hidden overflow-hidden bg-surface lg:flex lg:flex-col lg:justify-center lg:border-l lg:border-border">
        <div
          aria-hidden="true"
          className="absolute inset-0 opacity-[0.07]"
          style={{
            backgroundImage:
              "radial-gradient(circle at 20% 20%, var(--brand) 0, transparent 45%), radial-gradient(circle at 80% 70%, var(--brand) 0, transparent 40%)",
          }}
        />
        <div className="relative px-14 py-16">
          <p className="mb-2 text-xs font-medium tracking-wide text-brand uppercase">Built for the counter</p>
          <h2 className="max-w-md text-2xl font-semibold tracking-tight">
            Everything a grocery store runs on, without the spreadsheet.
          </h2>
          <ul className="mt-10 space-y-7">
            {HIGHLIGHTS.map(({ icon: IconComponent, title, body }) => (
              <li key={title} className="flex max-w-md gap-4">
                <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-brand-soft text-brand-soft-foreground">
                  <IconComponent className="size-[18px]" />
                </span>
                <span>
                  <span className="block text-sm font-medium">{title}</span>
                  <span className="mt-0.5 block text-sm text-muted">{body}</span>
                </span>
              </li>
            ))}
          </ul>
        </div>
      </aside>
    </main>
  );
}
