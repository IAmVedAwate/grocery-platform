"use client";

import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { NavBar } from "@/components/nav-bar";
import { Drawer } from "@/components/ui/drawer";
import { AlertIcon, LayersIcon, SearchIcon } from "@/components/ui/icons";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  Field,
  Input,
  Label,
  PageHeader,
  PageShell,
  Pagination,
  TableSkeleton,
  cn,
} from "@/components/ui/primitives";
import type { InventoryOverviewRow, PagedResult } from "@/lib/types";

const PAGE_SIZE = 20;

export default function InventoryPage() {
  const { accessToken, isLoading, authFetch } = useAuth();
  const router = useRouter();

  const [result, setResult] = useState<PagedResult<InventoryOverviewRow> | null>(null);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [lowStockOnly, setLowStockOnly] = useState(false);
  const [listError, setListError] = useState<string | null>(null);

  const [adjustingProductId, setAdjustingProductId] = useState<string | null>(null);
  const [quantityDelta, setQuantityDelta] = useState("");
  const [reason, setReason] = useState("");
  const [adjustError, setAdjustError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1);
    }, 250);
    return () => clearTimeout(timer);
  }, [search]);

  const load = useCallback(async () => {
    setListError(null);
    try {
      const query = new URLSearchParams({
        page: String(page),
        pageSize: String(PAGE_SIZE),
        lowStockOnly: String(lowStockOnly),
      });
      if (debouncedSearch.trim()) query.set("search", debouncedSearch.trim());
      const data = await authFetch<PagedResult<InventoryOverviewRow>>(`/api/v1/inventory?${query}`);
      setResult(data);
    } catch (err) {
      setListError(err instanceof ApiError ? err.message : "Could not load inventory.");
    }
  }, [authFetch, page, debouncedSearch, lowStockOnly]);

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

  const adjusting = useMemo(
    () => result?.items.find((r) => r.productId === adjustingProductId) ?? null,
    [result, adjustingProductId],
  );

  if (isLoading || !accessToken) return null;

  const totalPages = result ? Math.max(1, Math.ceil(result.totalCount / PAGE_SIZE)) : 1;
  const lowCount = result?.items.filter((r) => r.isLowStock).length ?? 0;
  const projected = Number(quantityDelta) && adjusting ? adjusting.quantityOnHand + Number(quantityDelta) : null;

  return (
    <>
      <NavBar />
      <PageShell>
        <PageHeader
          title="Inventory"
          description="Live stock levels across the catalog."
          action={
            lowCount > 0 && !lowStockOnly ? (
              <Button variant="secondary" onClick={() => { setLowStockOnly(true); setPage(1); }}>
                <AlertIcon className="size-4 text-warning" />
                {lowCount} low on this page
              </Button>
            ) : undefined
          }
        />

        <div className="mb-4 flex flex-wrap items-center gap-3">
          <div className="relative min-w-0 flex-1 sm:max-w-sm">
            <SearchIcon className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-subtle" />
            <Input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search by name or SKU…"
              aria-label="Search inventory"
              className="pl-9"
            />
          </div>
          {/* Segmented filter rather than a bare checkbox — bigger target,
              and the active state is obvious at a glance. */}
          <div className="flex rounded-lg border border-border p-0.5">
            {[
              { label: "All", value: false },
              { label: "Low stock", value: true },
            ].map((option) => (
              <button
                key={option.label}
                onClick={() => {
                  setPage(1);
                  setLowStockOnly(option.value);
                }}
                aria-pressed={lowStockOnly === option.value}
                className={cn(
                  "h-8 rounded-md px-3 text-xs font-medium transition-colors",
                  lowStockOnly === option.value
                    ? "bg-surface-muted text-foreground"
                    : "text-muted hover:text-foreground",
                )}
              >
                {option.label}
              </button>
            ))}
          </div>
        </div>

        {listError && <div className="mb-4"><Alert>{listError}</Alert></div>}

        <Card className="overflow-hidden">
          {!result ? (
            <TableSkeleton rows={6} cols={5} />
          ) : result.items.length === 0 ? (
            <EmptyState
              icon={<LayersIcon />}
              title={lowStockOnly ? "Nothing is low on stock" : "No products match"}
              description={
                lowStockOnly
                  ? "Every product on this page is above its threshold."
                  : "Try a different name or SKU."
              }
            />
          ) : (
            <>
              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm">
                  <thead>
                    <tr className="border-b border-border text-xs text-muted">
                      <th scope="col" className="px-5 py-2.5 font-medium">Product</th>
                      <th scope="col" className="px-5 py-2.5 text-right font-medium">On hand</th>
                      <th scope="col" className="px-5 py-2.5 text-right font-medium">Threshold</th>
                      <th scope="col" className="px-5 py-2.5 font-medium">Status</th>
                      <th scope="col" className="px-5 py-2.5 text-right font-medium">
                        <span className="sr-only">Actions</span>
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border">
                    {result.items.map((row) => (
                      <tr key={row.productId} className="transition-colors hover:bg-surface-hover">
                        <td className="px-5 py-3">
                          <p className="font-medium">{row.name}</p>
                          <p className="font-mono text-xs text-subtle">{row.sku}</p>
                        </td>
                        <td
                          className={cn(
                            "px-5 py-3 text-right font-medium tabular",
                            row.isLowStock && "text-warning",
                          )}
                        >
                          {row.quantityOnHand}
                        </td>
                        <td className="px-5 py-3 text-right text-muted tabular">{row.lowStockThreshold}</td>
                        <td className="px-5 py-3">
                          {row.isLowStock ? (
                            <Badge tone="warning">
                              <AlertIcon className="size-3" />
                              Low stock
                            </Badge>
                          ) : (
                            <Badge tone="success">In stock</Badge>
                          )}
                        </td>
                        <td className="px-5 py-3 text-right">
                          <Button variant="secondary" size="sm" onClick={() => startAdjust(row.productId)}>
                            Adjust
                          </Button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
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

      {/* The adjust form used to expand a row inline, which pushed the rest
          of the table around. A drawer keeps the list stable. */}
      <Drawer
        open={adjustingProductId !== null}
        onClose={() => setAdjustingProductId(null)}
        title="Adjust stock"
        description={adjusting ? `${adjusting.name} · ${adjusting.sku}` : undefined}
        footer={
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setAdjustingProductId(null)}>
              Cancel
            </Button>
            <Button form="adjust-stock" type="submit" disabled={isSaving}>
              {isSaving ? "Saving…" : "Save adjustment"}
            </Button>
          </div>
        }
      >
        <form id="adjust-stock" onSubmit={submitAdjust} className="space-y-4 p-5">
          {adjusting && (
            <div className="flex items-center justify-between rounded-lg bg-surface-muted px-4 py-3">
              <span className="text-xs text-muted">Current</span>
              <span className="text-lg font-semibold tabular">{adjusting.quantityOnHand}</span>
            </div>
          )}

          <div>
            <Label hint="use a negative number to remove">Quantity change</Label>
            <Input
              required
              type="number"
              inputMode="numeric"
              value={quantityDelta}
              onChange={(e) => setQuantityDelta(e.target.value)}
              placeholder="e.g. -3"
            />
            {projected !== null && (
              <p className={cn("mt-1.5 text-xs", projected < 0 ? "text-danger" : "text-muted")}>
                {projected < 0
                  ? "That would take stock below zero — the server will reject it."
                  : `New level will be ${projected}.`}
              </p>
            )}
          </div>

          <Field
            label="Reason"
            value={reason}
            onChange={setReason}
            placeholder="e.g. damaged stock, stock count correction"
          />

          {adjustError && <Alert>{adjustError}</Alert>}
        </form>
      </Drawer>
    </>
  );
}
