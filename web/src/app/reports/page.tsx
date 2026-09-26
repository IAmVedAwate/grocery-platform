"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { NavBar } from "@/components/nav-bar";
import { ChartIcon } from "@/components/ui/icons";
import {
  Alert,
  Card,
  EmptyState,
  Input,
  Label,
  PageHeader,
  PageShell,
  Pagination,
  TableSkeleton,
  cn,
} from "@/components/ui/primitives";
import type { PagedResult, SalesByCategoryRow, SalesByDayRow, SalesByProductRow } from "@/lib/types";

const PAGE_SIZE = 20;

type Tab = "day" | "product" | "category";
const TABS: { key: Tab; label: string }[] = [
  { key: "day", label: "By day" },
  { key: "product", label: "By product" },
  { key: "category", label: "By category" },
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

  // Totals for the page in view. Labelled "on this page" rather than
  // "total" because the API pages the rows — claiming a period total here
  // would be wrong the moment there's more than one page.
  const dayRows = dayResult?.items ?? [];
  const revenue = dayRows.reduce((s, r) => s + r.revenue, 0);
  const orders = dayRows.reduce((s, r) => s + r.orderCount, 0);

  return (
    <>
      <NavBar />
      <PageShell>
        <PageHeader title="Reports" description="Sales performance over a date range." />

        <Card className="mb-5">
          <div className="flex flex-wrap items-end gap-4 p-4">
            <label className="block">
              <Label>From</Label>
              <Input
                type="date"
                value={from}
                onChange={(e) => {
                  setFrom(e.target.value);
                  setPage(1);
                }}
                className="w-44"
              />
            </label>
            <label className="block">
              <Label>To</Label>
              <Input
                type="date"
                value={to}
                onChange={(e) => {
                  setTo(e.target.value);
                  setPage(1);
                }}
                className="w-44"
              />
            </label>
          </div>
        </Card>

        {tab === "day" && dayRows.length > 0 && (
          <div className="mb-5 grid gap-3 sm:grid-cols-3">
            <Stat label="Revenue on this page" value={revenue.toFixed(2)} />
            <Stat label="Orders on this page" value={String(orders)} />
            <Stat
              label="Average order value"
              value={orders > 0 ? (revenue / orders).toFixed(2) : "—"}
            />
          </div>
        )}

        <div className="mb-4 flex gap-1 border-b border-border" role="tablist">
          {TABS.map((t) => (
            <button
              key={t.key}
              role="tab"
              aria-selected={tab === t.key}
              onClick={() => switchTab(t.key)}
              className={cn(
                "-mb-px border-b-2 px-3 py-2 text-sm transition-colors",
                tab === t.key
                  ? "border-brand font-medium text-foreground"
                  : "border-transparent text-muted hover:text-foreground",
              )}
            >
              {t.label}
            </button>
          ))}
        </div>

        {error && <div className="mb-4"><Alert>{error}</Alert></div>}

        <Card className="overflow-hidden">
          {!result ? (
            <TableSkeleton rows={6} cols={4} />
          ) : result.items.length === 0 ? (
            <EmptyState
              icon={<ChartIcon />}
              title="No sales in this range"
              description="Widen the date range, or record a sale from Checkout."
            />
          ) : (
            <>
              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm">
                  {tab === "day" && (
                    <>
                      <Head cols={["Date", "Orders", "Revenue", "Avg order value"]} numericFrom={1} />
                      <tbody className="divide-y divide-border">
                        {dayRows.map((row) => (
                          <tr key={row.date} className="transition-colors hover:bg-surface-hover">
                            <td className="px-5 py-3 font-medium">{row.date}</td>
                            <td className="px-5 py-3 text-right tabular">{row.orderCount}</td>
                            <td className="px-5 py-3 text-right font-medium tabular">{row.revenue.toFixed(2)}</td>
                            <td className="px-5 py-3 text-right text-muted tabular">
                              {row.averageOrderValue.toFixed(2)}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </>
                  )}

                  {tab === "product" && (
                    <>
                      <Head cols={["Product", "SKU", "Qty sold", "Revenue"]} numericFrom={2} />
                      <tbody className="divide-y divide-border">
                        {(productResult?.items ?? []).map((row) => (
                          <tr key={row.productId} className="transition-colors hover:bg-surface-hover">
                            <td className="px-5 py-3 font-medium">{row.name}</td>
                            <td className="px-5 py-3 font-mono text-xs text-muted">{row.sku}</td>
                            <td className="px-5 py-3 text-right tabular">{row.quantitySold}</td>
                            <td className="px-5 py-3 text-right font-medium tabular">{row.revenue.toFixed(2)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </>
                  )}

                  {tab === "category" && (
                    <>
                      <Head cols={["Category", "Qty sold", "Revenue"]} numericFrom={1} />
                      <tbody className="divide-y divide-border">
                        {(categoryResult?.items ?? []).map((row) => (
                          <tr
                            key={row.categoryId ?? "uncategorized"}
                            className="transition-colors hover:bg-surface-hover"
                          >
                            <td className="px-5 py-3 font-medium">{row.categoryName}</td>
                            <td className="px-5 py-3 text-right tabular">{row.quantitySold}</td>
                            <td className="px-5 py-3 text-right font-medium tabular">{row.revenue.toFixed(2)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </>
                  )}
                </table>
              </div>
              <Pagination
                page={result.page}
                totalPages={totalPages}
                totalCount={result.totalCount}
                onPrev={() => setPage((p) => p - 1)}
                onNext={() => setPage((p) => p + 1)}
              />
            </>
          )}
        </Card>
      </PageShell>
    </>
  );
}

function Head({ cols, numericFrom }: { cols: string[]; numericFrom: number }) {
  return (
    <thead>
      <tr className="border-b border-border text-xs text-muted">
        {cols.map((c, i) => (
          <th key={c} scope="col" className={cn("px-5 py-2.5 font-medium", i >= numericFrom && "text-right")}>
            {c}
          </th>
        ))}
      </tr>
    </thead>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <Card className="px-5 py-4">
      <p className="text-xs text-muted">{label}</p>
      <p className="mt-1 text-2xl font-semibold tracking-tight tabular">{value}</p>
    </Card>
  );
}
