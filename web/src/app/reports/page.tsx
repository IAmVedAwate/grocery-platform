"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { NavBar } from "@/components/nav-bar";
import type { PagedResult, SalesByCategoryRow, SalesByDayRow, SalesByProductRow } from "@/lib/types";

const PAGE_SIZE = 20;

type Tab = "day" | "product" | "category";
const TABS: { key: Tab; label: string }[] = [
  { key: "day", label: "By Day" },
  { key: "product", label: "By Product" },
  { key: "category", label: "By Category" },
];

function toIsoRangeStart(date: string): string {
  return `${date}T00:00:00.000Z`;
}
function toIsoRangeEnd(date: string): string {
  return `${date}T23:59:59.999Z`;
}
function defaultFrom(): string {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() - 30);
  return d.toISOString().slice(0, 10);
}
function defaultTo(): string {
  return new Date().toISOString().slice(0, 10);
}

export default function ReportsPage() {
  const { accessToken, isLoading, authFetch } = useAuth();
  const router = useRouter();

  const [tab, setTab] = useState<Tab>("day");
  const [from, setFrom] = useState(defaultFrom());
  const [to, setTo] = useState(defaultTo());
  const [page, setPage] = useState(1);
  const [error, setError] = useState<string | null>(null);

  const [dayResult, setDayResult] = useState<PagedResult<SalesByDayRow> | null>(null);
  const [productResult, setProductResult] = useState<PagedResult<SalesByProductRow> | null>(null);
  const [categoryResult, setCategoryResult] = useState<PagedResult<SalesByCategoryRow> | null>(null);

  const load = useCallback(async () => {
    setError(null);
    const query = `from=${encodeURIComponent(toIsoRangeStart(from))}&to=${encodeURIComponent(toIsoRangeEnd(to))}&page=${page}&pageSize=${PAGE_SIZE}`;
    try {
      if (tab === "day") {
        setDayResult(await authFetch<PagedResult<SalesByDayRow>>(`/api/v1/reports/sales-by-day?${query}`));
      } else if (tab === "product") {
        setProductResult(await authFetch<PagedResult<SalesByProductRow>>(`/api/v1/reports/sales-by-product?${query}`));
      } else {
        setCategoryResult(await authFetch<PagedResult<SalesByCategoryRow>>(`/api/v1/reports/sales-by-category?${query}`));
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not load the report.");
    }
  }, [authFetch, tab, from, to, page]);

  useEffect(() => {
    if (!isLoading && !accessToken) router.push("/login");
  }, [isLoading, accessToken, router]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (accessToken) void load();
  }, [accessToken, load]);

  if (isLoading || !accessToken) return null;

  const result = tab === "day" ? dayResult : tab === "product" ? productResult : categoryResult;
  const totalPages = result ? Math.max(1, Math.ceil(result.totalCount / PAGE_SIZE)) : 1;

  function switchTab(next: Tab) {
    setTab(next);
    setPage(1);
  }

  return (
    <>
      <NavBar />
      <main className="mx-auto w-full max-w-4xl flex-1 p-6">
        <h1 className="mb-6 text-xl font-semibold">Reports</h1>

        <div className="mb-4 flex flex-wrap items-end gap-4">
          <label className="text-sm text-gray-600">
            From
            <input
              type="date"
              value={from}
              onChange={(e) => {
                setFrom(e.target.value);
                setPage(1);
              }}
              className="mt-1 block rounded-md border border-gray-300 px-2 py-1"
            />
          </label>
          <label className="text-sm text-gray-600">
            To
            <input
              type="date"
              value={to}
              onChange={(e) => {
                setTo(e.target.value);
                setPage(1);
              }}
              className="mt-1 block rounded-md border border-gray-300 px-2 py-1"
            />
          </label>
        </div>

        <div className="mb-4 flex gap-4 border-b border-gray-200 text-sm">
          {TABS.map((t) => (
            <button
              key={t.key}
              onClick={() => switchTab(t.key)}
              className={`-mb-px border-b-2 px-1 py-2 ${
                tab === t.key ? "border-gray-900 font-semibold text-gray-900" : "border-transparent text-gray-500 hover:text-gray-900"
              }`}
            >
              {t.label}
            </button>
          ))}
        </div>

        {error && <p className="mb-3 text-sm text-red-600">{error}</p>}

        {!result ? (
          <p className="text-sm text-gray-500">Loading…</p>
        ) : result.items.length === 0 ? (
          <p className="text-sm text-gray-500">No sales in this date range.</p>
        ) : (
          <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
            <table className="w-full text-left text-sm">
              {tab === "day" && (
                <>
                  <thead className="border-b border-gray-200 text-gray-500">
                    <tr>
                      <th className="px-3 py-2">Date</th>
                      <th className="px-3 py-2">Orders</th>
                      <th className="px-3 py-2">Revenue</th>
                      <th className="px-3 py-2">Avg Order Value</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(dayResult?.items ?? []).map((row) => (
                      <tr key={row.date} className="border-b border-gray-100 last:border-0">
                        <td className="px-3 py-2">{row.date}</td>
                        <td className="px-3 py-2">{row.orderCount}</td>
                        <td className="px-3 py-2">{row.revenue.toFixed(2)}</td>
                        <td className="px-3 py-2">{row.averageOrderValue.toFixed(2)}</td>
                      </tr>
                    ))}
                  </tbody>
                </>
              )}
              {tab === "product" && (
                <>
                  <thead className="border-b border-gray-200 text-gray-500">
                    <tr>
                      <th className="px-3 py-2">SKU</th>
                      <th className="px-3 py-2">Product</th>
                      <th className="px-3 py-2">Qty Sold</th>
                      <th className="px-3 py-2">Revenue</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(productResult?.items ?? []).map((row) => (
                      <tr key={row.productId} className="border-b border-gray-100 last:border-0">
                        <td className="px-3 py-2">{row.sku}</td>
                        <td className="px-3 py-2">{row.name}</td>
                        <td className="px-3 py-2">{row.quantitySold}</td>
                        <td className="px-3 py-2">{row.revenue.toFixed(2)}</td>
                      </tr>
                    ))}
                  </tbody>
                </>
              )}
              {tab === "category" && (
                <>
                  <thead className="border-b border-gray-200 text-gray-500">
                    <tr>
                      <th className="px-3 py-2">Category</th>
                      <th className="px-3 py-2">Qty Sold</th>
                      <th className="px-3 py-2">Revenue</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(categoryResult?.items ?? []).map((row) => (
                      <tr key={row.categoryId ?? "uncategorized"} className="border-b border-gray-100 last:border-0">
                        <td className="px-3 py-2">{row.categoryName}</td>
                        <td className="px-3 py-2">{row.quantitySold}</td>
                        <td className="px-3 py-2">{row.revenue.toFixed(2)}</td>
                      </tr>
                    ))}
                  </tbody>
                </>
              )}
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
