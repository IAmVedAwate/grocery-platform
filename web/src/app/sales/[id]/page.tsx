"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import { useParams, useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { NavBar } from "@/components/nav-bar";
import { StatusBadge } from "@/components/status-badge";
import type { PagedResult, ProductDto, SalesOrderDto } from "@/lib/types";

export default function SalesOrderDetailPage() {
  const { accessToken, isLoading, authFetch } = useAuth();
  const router = useRouter();
  const params = useParams<{ id: string }>();

  const [order, setOrder] = useState<SalesOrderDto | null>(null);
  const [products, setProducts] = useState<ProductDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [refundError, setRefundError] = useState<string | null>(null);
  const [isRefunding, setIsRefunding] = useState(false);
  const [refundQuantities, setRefundQuantities] = useState<Record<string, string>>({});

  const load = useCallback(async () => {
    setError(null);
    try {
      const [orderResult, productResult] = await Promise.all([
        authFetch<SalesOrderDto>(`/api/v1/sales-orders/${params.id}`),
        authFetch<PagedResult<ProductDto>>("/api/v1/products?pageSize=100"),
      ]);
      setOrder(orderResult);
      setProducts(productResult.items);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not load this sale.");
    }
  }, [authFetch, params.id]);

  useEffect(() => {
    if (!isLoading && !accessToken) router.push("/login");
  }, [isLoading, accessToken, router]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (accessToken) void load();
  }, [accessToken, load]);

  async function submitRefund(e: FormEvent) {
    e.preventDefault();
    setRefundError(null);
    setIsRefunding(true);
    try {
      const lines = Object.entries(refundQuantities)
        .filter(([, qty]) => qty && Number(qty) > 0)
        .map(([productId, qty]) => ({ productId, quantity: Number(qty) }));
      if (lines.length === 0) throw new Error("Enter a quantity to refund for at least one line.");

      await authFetch(`/api/v1/sales-orders/${params.id}/refund`, {
        method: "POST",
        body: JSON.stringify({ lines }),
      });
      setRefundQuantities({});
      await load();
    } catch (err) {
      setRefundError(err instanceof ApiError ? err.message : err instanceof Error ? err.message : "Could not process refund.");
    } finally {
      setIsRefunding(false);
    }
  }

  if (isLoading || !accessToken) return null;

  const productName = (id: string) => products.find((p) => p.id === id)?.name ?? id;
  const canRefund = order && order.status !== "Refunded" && order.items.some((i) => i.quantity - i.quantityRefunded > 0);

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
              <h1 className="text-xl font-semibold">Invoice {order.invoiceNumber}</h1>
              <StatusBadge status={order.status} />
            </div>

            <div className="mb-6 overflow-x-auto rounded-lg border border-gray-200 bg-white">
              <table className="w-full text-left text-sm">
                <thead className="border-b border-gray-200 text-gray-500">
                  <tr>
                    <th className="px-3 py-2">Product</th>
                    <th className="px-3 py-2">Qty</th>
                    <th className="px-3 py-2">Unit price</th>
                    <th className="px-3 py-2">Refunded</th>
                    {canRefund && <th className="px-3 py-2">Refund now</th>}
                  </tr>
                </thead>
                <tbody>
                  {order.items.map((item) => {
                    const remaining = item.quantity - item.quantityRefunded;
                    return (
                      <tr key={item.productId} className="border-b border-gray-100 last:border-0">
                        <td className="px-3 py-2">{productName(item.productId)}</td>
                        <td className="px-3 py-2">{item.quantity}</td>
                        <td className="px-3 py-2">{item.unitPrice.toFixed(2)}</td>
                        <td className="px-3 py-2">{item.quantityRefunded}</td>
                        {canRefund && (
                          <td className="px-3 py-2">
                            {remaining > 0 ? (
                              <input
                                type="number"
                                min={0}
                                max={remaining}
                                value={refundQuantities[item.productId] ?? ""}
                                onChange={(e) => setRefundQuantities((prev) => ({ ...prev, [item.productId]: e.target.value }))}
                                className="w-20 rounded-md border border-gray-300 px-2 py-1 text-sm focus:border-gray-500 focus:outline-none"
                              />
                            ) : (
                              <span className="text-gray-400">Fully refunded</span>
                            )}
                          </td>
                        )}
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            <div className="mb-6 text-right text-sm text-gray-600">
              <p>Subtotal {order.subtotalAmount.toFixed(2)}</p>
              <p>Tax {order.taxAmount.toFixed(2)}</p>
              <p className="text-lg font-semibold text-gray-900">Total {order.totalAmount.toFixed(2)}</p>
            </div>

            {canRefund && (
              <form onSubmit={submitRefund}>
                {refundError && <p className="mb-2 text-sm text-red-600">{refundError}</p>}
                <button
                  type="submit"
                  disabled={isRefunding}
                  className="rounded-md bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-500 disabled:opacity-50"
                >
                  {isRefunding ? "Processing…" : "Process refund"}
                </button>
              </form>
            )}
          </>
        )}
      </main>
    </>
  );
}
