"use client";

import { Fragment, useCallback, useEffect, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { NavBar } from "@/components/nav-bar";
import type { InventoryOverviewRow, PagedResult } from "@/lib/types";

const PAGE_SIZE = 20;

export default function InventoryPage() {
  const { accessToken, isLoading, authFetch } = useAuth();
  const router = useRouter();

  const [result, setResult] = useState<PagedResult<InventoryOverviewRow> | null>(null);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [lowStockOnly, setLowStockOnly] = useState(false);
  const [listError, setListError] = useState<string | null>(null);

  const [adjustingProductId, setAdjustingProductId] = useState<string | null>(null);
  const [quantityDelta, setQuantityDelta] = useState("");
  const [reason, setReason] = useState("");
  const [adjustError, setAdjustError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const load = useCallback(async () => {
    setListError(null);
    try {
      const query = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE), lowStockOnly: String(lowStockOnly) });
      if (search.trim()) query.set("search", search.trim());
      const data = await authFetch<PagedResult<InventoryOverviewRow>>(`/api/v1/inventory?${query}`);
      setResult(data);
    } catch (err) {
      setListError(err instanceof ApiError ? err.message : "Could not load inventory.");
    }
  }, [authFetch, page, search, lowStockOnly]);

  useEffect(() => {
    if (!isLoading && !accessToken) router.push("/login");
  }, [isLoading, accessToken, router]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (accessToken) void load();
  }, [accessToken, load]);

  function startAdjust(productId: string) {
    setAdjustingProductId(productId);
    setQuantityDelta("");
    setReason("");
    setAdjustError(null);
  }

  async function submitAdjust(e: FormEvent) {
    e.preventDefault();
    if (!adjustingProductId) return;
    setIsSaving(true);
    setAdjustError(null);
    try {
      await authFetch(`/api/v1/inventory/${adjustingProductId}/adjust`, {
        method: "POST",
        body: JSON.stringify({ quantityDelta: Number(quantityDelta), reason }),
      });
      setAdjustingProductId(null);
      await load();
    } catch (err) {
      setAdjustError(err instanceof ApiError ? err.message : "Could not adjust stock.");
    } finally {
      setIsSaving(false);
    }
  }

  if (isLoading || !accessToken) return null;

  const totalPages = result ? Math.max(1, Math.ceil(result.totalCount / PAGE_SIZE)) : 1;

  return (
    <>
      <NavBar />
      <main className="mx-auto w-full max-w-4xl flex-1 p-6">
        <h1 className="mb-6 text-xl font-semibold">Inventory</h1>

        <div className="mb-3 flex flex-wrap items-center gap-3">
          <input
            value={search}
            onChange={(e) => {
              setPage(1);
              setSearch(e.target.value);
            }}
            placeholder="Search by name or SKU…"
            className="flex-1 rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-500 focus:outline-none"
          />
          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input
              type="checkbox"
              checked={lowStockOnly}
              onChange={(e) => {
                setPage(1);
                setLowStockOnly(e.target.checked);
              }}
            />
            Low stock only
          </label>
        </div>

        {listError && <p className="mb-3 text-sm text-red-600">{listError}</p>}

        {!result ? (
          <p className="text-sm text-gray-500">Loading…</p>
        ) : result.items.length === 0 ? (
          <p className="text-sm text-gray-500">No products match.</p>
        ) : (
          <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-gray-200 text-gray-500">
                <tr>
                  <th className="px-3 py-2">SKU</th>
                  <th className="px-3 py-2">Name</th>
                  <th className="px-3 py-2">On hand</th>
                  <th className="px-3 py-2">Threshold</th>
                  <th className="px-3 py-2">Status</th>
                  <th className="px-3 py-2" />
                </tr>
              </thead>
              <tbody>
                {result.items.map((row) => (
                  <Fragment key={row.productId}>
                    <tr className="border-b border-gray-100 last:border-0">
                      <td className="px-3 py-2">{row.sku}</td>
                      <td className="px-3 py-2">{row.name}</td>
                      <td className="px-3 py-2">{row.quantityOnHand}</td>
                      <td className="px-3 py-2">{row.lowStockThreshold}</td>
                      <td className="px-3 py-2">
                        {row.isLowStock ? (
                          <span className="rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-800">Low stock</span>
                        ) : (
                          <span className="text-gray-400">OK</span>
                        )}
                      </td>
                      <td className="px-3 py-2">
                        <button onClick={() => startAdjust(row.productId)} className="text-gray-900 underline">
                          Adjust
                        </button>
                      </td>
                    </tr>
                    {adjustingProductId === row.productId && (
                      <tr className="border-b border-gray-100 bg-gray-50">
                        <td colSpan={6} className="px-3 py-3">
                          <form onSubmit={submitAdjust} className="flex flex-wrap items-end gap-3">
                            <label className="block">
                              <span className="mb-1 block text-xs font-medium text-gray-700">Quantity change (+/-)</span>
                              <input
                                required
                                type="number"
                                value={quantityDelta}
                                onChange={(e) => setQuantityDelta(e.target.value)}
                                className="w-32 rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-gray-500 focus:outline-none"
                              />
                            </label>
                            <label className="block flex-1">
                              <span className="mb-1 block text-xs font-medium text-gray-700">Reason</span>
                              <input
                                required
                                value={reason}
                                onChange={(e) => setReason(e.target.value)}
                                placeholder="e.g. damaged stock, stock count correction"
                                className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-gray-500 focus:outline-none"
                              />
                            </label>
                            <button
                              type="submit"
                              disabled={isSaving}
                              className="rounded-md bg-gray-900 px-3 py-1.5 text-sm font-medium text-white hover:bg-gray-800 disabled:opacity-50"
                            >
                              Save
                            </button>
                            <button type="button" onClick={() => setAdjustingProductId(null)} className="text-sm text-gray-500 underline">
                              Cancel
                            </button>
                          </form>
                          {adjustError && <p className="mt-2 text-sm text-red-600">{adjustError}</p>}
                        </td>
                      </tr>
                    )}
                  </Fragment>
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
