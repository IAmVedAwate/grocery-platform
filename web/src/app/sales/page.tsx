"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { NavBar } from "@/components/nav-bar";
import { StatusBadge } from "@/components/status-badge";
import type { PagedResult, SalesOrderDto } from "@/lib/types";

const PAGE_SIZE = 20;

export default function SalesHistoryPage() {
  const { accessToken, isLoading, authFetch } = useAuth();
  const router = useRouter();

  const [result, setResult] = useState<PagedResult<SalesOrderDto> | null>(null);
  const [page, setPage] = useState(1);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const data = await authFetch<PagedResult<SalesOrderDto>>(`/api/v1/sales-orders?page=${page}&pageSize=${PAGE_SIZE}`);
      setResult(data);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not load sales history.");
    }
  }, [authFetch, page]);

  useEffect(() => {
    if (!isLoading && !accessToken) router.push("/login");
  }, [isLoading, accessToken, router]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (accessToken) void load();
  }, [accessToken, load]);

  if (isLoading || !accessToken) return null;

  const totalPages = result ? Math.max(1, Math.ceil(result.totalCount / PAGE_SIZE)) : 1;

  return (
    <>
      <NavBar />
      <main className="mx-auto w-full max-w-4xl flex-1 p-6">
        <h1 className="mb-6 text-xl font-semibold">Sales History</h1>
        {error && <p className="mb-3 text-sm text-red-600">{error}</p>}

        {!result ? (
          <p className="text-sm text-gray-500">Loading…</p>
        ) : result.items.length === 0 ? (
          <p className="text-sm text-gray-500">No sales yet — head to Checkout to record one.</p>
        ) : (
          <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-gray-200 text-gray-500">
                <tr>
                  <th className="px-3 py-2">Invoice</th>
                  <th className="px-3 py-2">Status</th>
                  <th className="px-3 py-2">Total</th>
                  <th className="px-3 py-2">Date</th>
                  <th className="px-3 py-2" />
                </tr>
              </thead>
              <tbody>
                {result.items.map((o) => (
                  <tr key={o.id} className="border-b border-gray-100 last:border-0">
                    <td className="px-3 py-2">{o.invoiceNumber}</td>
                    <td className="px-3 py-2">
                      <StatusBadge status={o.status} />
                    </td>
                    <td className="px-3 py-2">{o.totalAmount.toFixed(2)}</td>
                    <td className="px-3 py-2">{new Date(o.createdAtUtc).toLocaleString()}</td>
                    <td className="px-3 py-2">
                      <Link href={`/sales/${o.id}`} className="text-gray-900 underline">
                        View
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {result && result.totalCount > 0 && (
          <div className="mt-3 flex items-center justify-between text-sm text-gray-500">
            <span>
              Page {result.page} of {totalPages} ({result.totalCount} total)
            </span>
            <div className="flex gap-2">
              <button disabled={page <= 1} onClick={() => setPage((p) => p - 1)} className="rounded-md border border-gray-300 px-3 py-1 disabled:opacity-50">
                Previous
              </button>
              <button disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)} className="rounded-md border border-gray-300 px-3 py-1 disabled:opacity-50">
                Next
              </button>
            </div>
          </div>
        )}
      </main>
    </>
  );
}
