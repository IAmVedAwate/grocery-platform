"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { NavBar } from "@/components/nav-bar";
import { Field } from "@/components/field";
import type { CustomerDto, PagedResult } from "@/lib/types";

export default function CustomersPage() {
  const { accessToken, isLoading, authFetch } = useAuth();
  const router = useRouter();

  const [result, setResult] = useState<PagedResult<CustomerDto> | null>(null);
  const [search, setSearch] = useState("");
  const [listError, setListError] = useState<string | null>(null);

  const [name, setName] = useState("");
  const [phone, setPhone] = useState("");
  const [email, setEmail] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const load = useCallback(async () => {
    setListError(null);
    try {
      const query = new URLSearchParams({ pageSize: "50" });
      if (search.trim()) query.set("search", search.trim());
      const data = await authFetch<PagedResult<CustomerDto>>(`/api/v1/customers?${query}`);
      setResult(data);
    } catch (err) {
      setListError(err instanceof ApiError ? err.message : "Could not load customers.");
    }
  }, [authFetch, search]);

  useEffect(() => {
    if (!isLoading && !accessToken) router.push("/login");
  }, [isLoading, accessToken, router]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (accessToken) void load();
  }, [accessToken, load]);

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    setFormError(null);
    setIsSaving(true);
    try {
      await authFetch("/api/v1/customers", {
        method: "POST",
        body: JSON.stringify({ name, phone: phone || null, email: email || null }),
      });
      setName("");
      setPhone("");
      setEmail("");
      await load();
    } catch (err) {
      setFormError(err instanceof ApiError ? err.message : "Could not save customer.");
    } finally {
      setIsSaving(false);
    }
  }

  if (isLoading || !accessToken) return null;

  return (
    <>
      <NavBar />
      <main className="mx-auto w-full max-w-3xl flex-1 p-6">
        <h1 className="mb-6 text-xl font-semibold">Customers</h1>

        <form onSubmit={handleCreate} className="mb-8 rounded-lg border border-gray-200 bg-white p-4">
          <h2 className="mb-3 text-sm font-medium text-gray-700">Add a customer</h2>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <Field label="Name" value={name} onChange={setName} />
            <Field label="Phone" value={phone} onChange={setPhone} required={false} />
            <Field label="Email" type="email" value={email} onChange={setEmail} required={false} />
          </div>
          {formError && <p className="mt-2 text-sm text-red-600">{formError}</p>}
          <button
            type="submit"
            disabled={isSaving}
            className="mt-3 rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white hover:bg-gray-800 disabled:opacity-50"
          >
            {isSaving ? "Saving…" : "Add customer"}
          </button>
        </form>

        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search by name or phone…"
          className="mb-3 w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-500 focus:outline-none"
        />

        {listError && <p className="mb-3 text-sm text-red-600">{listError}</p>}

        {!result ? (
          <p className="text-sm text-gray-500">Loading…</p>
        ) : result.items.length === 0 ? (
          <p className="text-sm text-gray-500">No customers yet.</p>
        ) : (
          <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-gray-200 text-gray-500">
                <tr>
                  <th className="px-3 py-2">Name</th>
                  <th className="px-3 py-2">Phone</th>
                  <th className="px-3 py-2">Email</th>
                  <th className="px-3 py-2" />
                </tr>
              </thead>
              <tbody>
                {result.items.map((c) => (
                  <tr key={c.id} className="border-b border-gray-100 last:border-0">
                    <td className="px-3 py-2">{c.name}</td>
                    <td className="px-3 py-2">{c.phone ?? "—"}</td>
                    <td className="px-3 py-2">{c.email ?? "—"}</td>
                    <td className="px-3 py-2">
                      <Link href={`/sales?customerId=${c.id}`} className="text-gray-900 underline">
                        Purchase history
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </main>
    </>
  );
}
