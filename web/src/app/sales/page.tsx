"use client";

import { Suspense, useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { NavBar } from "@/components/nav-bar";
import { StatusBadge } from "@/components/status-badge";
import { ArrowRightIcon, CartIcon, CloseIcon } from "@/components/ui/icons";
import {
  Alert,
  Button,
  Card,
  EmptyState,
  PageHeader,
  PageShell,
  Pagination,
  TableSkeleton,
} from "@/components/ui/primitives";
import type { PagedResult, SalesOrderDto } from "@/lib/types";

const PAGE_SIZE = 20;

function SalesHistoryContent() {
  const { accessToken, isLoading, authFetch } = useAuth();
  const router = useRouter();
  // Customer purchase history (docs/PRD.md §5.6) is this same list, scoped
  // by a query parameter — see SalesController.List's own remarks for why
  // there's no separate endpoint for it.
  const customerId = useSearchParams().get("customerId");

  const [result, setResult] = useState<PagedResult<SalesOrderDto> | null>(null);
  const [page, setPage] = useState(1);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const query = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE) });
      if (customerId) query.set("customerId", customerId);
      const data = await authFetch<PagedResult<SalesOrderDto>>(`/api/v1/sales-orders?${query}`);
      setResult(data);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not load sales history.");
    }
  }, [authFetch, page, customerId]);

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
      <PageShell>
        <PageHeader
          title="Sales"
          description={customerId ? "Purchase history for one customer." : "Every completed sale, newest first."}
          action={
            customerId ? (
              <Link href="/sales">
                <Button variant="secondary" size="sm">
                  <CloseIcon className="size-3.5" />
                  Clear customer filter
                </Button>
              </Link>
            ) : undefined
          }
        />

        {error && <div className="mb-4"><Alert>{error}</Alert></div>}

        <Card className="overflow-hidden">
          {!result ? (
            <TableSkeleton rows={6} cols={4} />
          ) : result.items.length === 0 ? (
            <EmptyState
              icon={<CartIcon />}
              title={customerId ? "No sales for this customer" : "No sales yet"}
              description="Completed sales appear here with their invoice and total."
              action={
                <Link href="/checkout">
                  <Button>Start a sale</Button>
                </Link>
              }
            />
          ) : (
            <>
              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm">
                  <thead>
                    <tr className="border-b border-border text-xs text-muted">
                      <th scope="col" className="px-5 py-2.5 font-medium">Invoice</th>
                      <th scope="col" className="px-5 py-2.5 font-medium">Status</th>
                      <th scope="col" className="px-5 py-2.5 text-right font-medium">Total</th>
                      <th scope="col" className="px-5 py-2.5 font-medium">Date</th>
                      <th scope="col" className="px-5 py-2.5">
                        <span className="sr-only">View</span>
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border">
                    {result.items.map((o) => (
                      <tr key={o.id} className="group transition-colors hover:bg-surface-hover">
                        <td className="px-5 py-3">
                          {/* Whole row is a link target via the invoice cell —
                              nesting <a> around <tr> isn't valid HTML. */}
                          <Link
                            href={`/sales/${o.id}`}
                            className="font-mono text-xs font-medium underline-offset-2 hover:underline"
                          >
                            {o.invoiceNumber}
                          </Link>
                        </td>
                        <td className="px-5 py-3">
                          <StatusBadge status={o.status} />
                        </td>
                        <td className="px-5 py-3 text-right font-medium tabular">{o.totalAmount.toFixed(2)}</td>
                        <td className="px-5 py-3 text-muted">
                          {new Date(o.createdAtUtc).toLocaleString(undefined, {
                            dateStyle: "medium",
                            timeStyle: "short",
                          })}
                        </td>
                        <td className="px-5 py-3 text-right">
                          <Link
                            href={`/sales/${o.id}`}
                            aria-label={`View sale ${o.invoiceNumber}`}
                            className="inline-flex text-subtle transition-transform group-hover:translate-x-0.5 group-hover:text-foreground"
                          >
                            <ArrowRightIcon className="size-4" />
                          </Link>
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
    </>
  );
}

export default function SalesHistoryPage() {
  return (
    <Suspense>
      <SalesHistoryContent />
    </Suspense>
  );
}
