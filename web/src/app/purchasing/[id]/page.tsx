"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import { useParams, useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { NavBar } from "@/components/nav-bar";
import { StatusBadge } from "@/components/status-badge";
import type { PagedResult, ProductDto, PurchaseOrderDto } from "@/lib/types";

export default function PurchaseOrderDetailPage() {
  const { accessToken, isLoading, authFetch } = useAuth();
  const router = useRouter();
  const params = useParams<{ id: string }>();

  const [order, setOrder] = useState<PurchaseOrderDto | null>(null);
  const [products, setProducts] = useState<ProductDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);
  const [receiveQuantities, setReceiveQuantities] = useState<Record<string, string>>({});

  const load = useCallback(async () => {
    setError(null);
    try {
      const [orderResult, productResult] = await Promise.all([
        authFetch<PurchaseOrderDto>(`/api/v1/purchase-orders/${params.id}`),
        authFetch<PagedResult<ProductDto>>("/api/v1/products?pageSize=100"),
      ]);
      setOrder(orderResult);
      setProducts(productResult.items);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not load this purchase order.");
    }
  }, [authFetch, params.id]);

  useEffect(() => {
    if (!isLoading && !accessToken) router.push("/login");
  }, [isLoading, accessToken, router]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (accessToken) void load();
  }, [accessToken, load]);

  async function runAction(action: "submit" | "approve" | "cancel") {
    setActionError(null);
    setIsBusy(true);
    try {
      await authFetch(`/api/v1/purchase-orders/${params.id}/${action}`, { method: "POST" });
      await load();
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : `Could not ${action} this order.`);
    } finally {
      setIsBusy(false);
    }
  }

  async function submitReceive(e: FormEvent) {
    e.preventDefault();
    setActionError(null);
    setIsBusy(true);
    try {
      const lines = Object.entries(receiveQuantities)
        .filter(([, qty]) => qty && Number(qty) > 0)
        .map(([productId, qty]) => ({ productId, quantity: Number(qty) }));
      if (lines.length === 0) throw new Error("Enter a quantity for at least one line.");

      await authFetch(`/api/v1/purchase-orders/${params.id}/receive`, {
        method: "POST",
        body: JSON.stringify({ lines }),
      });
      setReceiveQuantities({});
      await load();
    } catch (err) {
      setActionError(err instanceof ApiError ? err.message : err instanceof Error ? err.message : "Could not receive stock.");
    } finally {
      setIsBusy(false);
    }
  }

  if (isLoading || !accessToken) return null;

  const productName = (id: string) => products.find((p) => p.id === id)?.name ?? id;
  const canReceive = order?.status === "Approved" || order?.status === "PartiallyReceived";

  return (
    <>
      <NavBar />
      <main className="mx-auto w-full max-w-3xl flex-1 p-6">
        {error && <p className="text-sm text-red-600">{error}</p>}
        {!order ? (
          <p className="text-sm text-gray-500">Loading…</p>
        ) : (
          <>
            <div className="mb-4 flex items-center gap-3">
              <h1 className="text-xl font-semibold">Purchase Order</h1>
              <StatusBadge status={order.status} />
            </div>

            <div className="mb-4 flex flex-wrap gap-2">
              {order.status === "Draft" && (
                <ActionButton label="Submit" onClick={() => runAction("submit")} disabled={isBusy} />
              )}
              {order.status === "Submitted" && (
                <ActionButton label="Approve" onClick={() => runAction("approve")} disabled={isBusy} />
              )}
              {(order.status === "Draft" || order.status === "Submitted" || order.status === "Approved") && (
                <ActionButton label="Cancel" onClick={() => runAction("cancel")} disabled={isBusy} variant="danger" />
              )}
            </div>
            {actionError && <p className="mb-4 text-sm text-red-600">{actionError}</p>}

            <div className="mb-6 overflow-x-auto rounded-lg border border-gray-200 bg-white">
              <table className="w-full text-left text-sm">
                <thead className="border-b border-gray-200 text-gray-500">
                  <tr>
                    <th className="px-3 py-2">Product</th>
                    <th className="px-3 py-2">Ordered</th>
                    <th className="px-3 py-2">Unit cost</th>
                    <th className="px-3 py-2">Received</th>
                    {canReceive && <th className="px-3 py-2">Receive now</th>}
                  </tr>
                </thead>
                <tbody>
                  {order.items.map((item) => (
                    <tr key={item.productId} className="border-b border-gray-100 last:border-0">
                      <td className="px-3 py-2">{productName(item.productId)}</td>
                      <td className="px-3 py-2">{item.quantityOrdered}</td>
                      <td className="px-3 py-2">{item.unitCost.toFixed(2)}</td>
                      <td className="px-3 py-2">{item.quantityReceived}</td>
                      {canReceive && (
                        <td className="px-3 py-2">
                          {item.quantityReceived < item.quantityOrdered ? (
                            <input
                              type="number"
                              min={0}
                              max={item.quantityOrdered - item.quantityReceived}
                              value={receiveQuantities[item.productId] ?? ""}
                              onChange={(e) =>
                                setReceiveQuantities((prev) => ({ ...prev, [item.productId]: e.target.value }))
                              }
                              className="w-20 rounded-md border border-gray-300 px-2 py-1 text-sm focus:border-gray-500 focus:outline-none"
                            />
                          ) : (
                            <span className="text-gray-400">Complete</span>
                          )}
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {canReceive && (
              <form onSubmit={submitReceive}>
                <button
                  type="submit"
                  disabled={isBusy}
                  className="rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white hover:bg-gray-800 disabled:opacity-50"
                >
                  Record receipt
                </button>
              </form>
            )}
          </>
        )}
      </main>
    </>
  );
}

function ActionButton({
  label,
  onClick,
  disabled,
  variant = "primary",
}: {
  label: string;
  onClick: () => void;
  disabled?: boolean;
  variant?: "primary" | "danger";
}) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      className={`rounded-md px-4 py-2 text-sm font-medium text-white disabled:opacity-50 ${
        variant === "danger" ? "bg-red-600 hover:bg-red-500" : "bg-gray-900 hover:bg-gray-800"
      }`}
    >
      {label}
    </button>
  );
}
