"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { NavBar } from "@/components/nav-bar";
import { Field } from "@/components/field";
import { StatusBadge } from "@/components/status-badge";
import type { PagedResult, ProductDto, PurchaseOrderDto, SupplierDto } from "@/lib/types";

type DraftLine = { productId: string; quantity: string; unitCost: string };

export default function PurchasingPage() {
  const { accessToken, isLoading, authFetch } = useAuth();
  const router = useRouter();

  const [suppliers, setSuppliers] = useState<SupplierDto[]>([]);
  const [products, setProducts] = useState<ProductDto[]>([]);
  const [orders, setOrders] = useState<PurchaseOrderDto[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [supplierName, setSupplierName] = useState("");
  const [supplierError, setSupplierError] = useState<string | null>(null);

  const [orderSupplierId, setOrderSupplierId] = useState("");
  const [lines, setLines] = useState<DraftLine[]>([{ productId: "", quantity: "", unitCost: "" }]);
  const [orderError, setOrderError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      const [supplierResult, productResult, orderResult] = await Promise.all([
        authFetch<PagedResult<SupplierDto>>("/api/v1/suppliers?pageSize=100"),
        authFetch<PagedResult<ProductDto>>("/api/v1/products?pageSize=100"),
        authFetch<PagedResult<PurchaseOrderDto>>("/api/v1/purchase-orders?pageSize=50"),
      ]);
      setSuppliers(supplierResult.items);
      setProducts(productResult.items);
      setOrders(orderResult.items);
    } catch (err) {
      setLoadError(err instanceof ApiError ? err.message : "Could not load purchasing data.");
    }
  }, [authFetch]);

  useEffect(() => {
    if (!isLoading && !accessToken) router.push("/login");
  }, [isLoading, accessToken, router]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (accessToken) void load();
  }, [accessToken, load]);

  async function createSupplier(e: FormEvent) {
    e.preventDefault();
    setSupplierError(null);
    try {
      await authFetch("/api/v1/suppliers", {
        method: "POST",
        body: JSON.stringify({ name: supplierName, contactInfo: null, paymentTermsDays: null }),
      });
      setSupplierName("");
      await load();
    } catch (err) {
      setSupplierError(err instanceof ApiError ? err.message : "Could not create supplier.");
    }
  }

  function updateLine(index: number, patch: Partial<DraftLine>) {
    setLines((prev) => prev.map((l, i) => (i === index ? { ...l, ...patch } : l)));
  }

  async function createOrder(e: FormEvent) {
    e.preventDefault();
    setOrderError(null);
    setIsSaving(true);
    try {
      await authFetch("/api/v1/purchase-orders", {
        method: "POST",
        body: JSON.stringify({
          supplierId: orderSupplierId,
          lines: lines
            .filter((l) => l.productId && l.quantity)
            .map((l) => ({ productId: l.productId, quantity: Number(l.quantity), unitCost: Number(l.unitCost || 0) })),
        }),
      });
      setOrderSupplierId("");
      setLines([{ productId: "", quantity: "", unitCost: "" }]);
      await load();
    } catch (err) {
      setOrderError(err instanceof ApiError ? err.message : "Could not create purchase order.");
    } finally {
      setIsSaving(false);
    }
  }

  if (isLoading || !accessToken) return null;

  return (
    <>
      <NavBar />
      <main className="mx-auto w-full max-w-4xl flex-1 animate-[--animate-fade-up] px-4 py-6 sm:px-6 sm:py-8">
        <h1 className="mb-6 text-xl font-semibold">Purchasing</h1>
        {loadError && <p className="mb-4 text-sm text-danger">{loadError}</p>}

        <section className="mb-8 rounded-xl border border-border bg-surface shadow-card p-4">
          <h2 className="mb-3 text-sm font-medium text-muted">Suppliers</h2>
          <form onSubmit={createSupplier} className="mb-3 flex items-end gap-3">
            <Field label="New supplier name" value={supplierName} onChange={setSupplierName} />
            <button type="submit" className="rounded-md bg-brand px-4 py-2 text-sm font-medium text-brand-foreground hover:bg-brand-hover">
              Add
            </button>
          </form>
          {supplierError && <p className="mb-2 text-sm text-danger">{supplierError}</p>}
          <ul className="flex flex-wrap gap-2 text-sm">
            {suppliers.map((s) => (
              <li key={s.id} className="rounded-full border border-border px-3 py-1">
                {s.name}
              </li>
            ))}
          </ul>
        </section>

        <section className="mb-8 rounded-xl border border-border bg-surface shadow-card p-4">
          <h2 className="mb-3 text-sm font-medium text-muted">Create purchase order</h2>
          <form onSubmit={createOrder} className="space-y-3">
            <label className="block">
              <span className="mb-1 block text-sm font-medium text-muted">Supplier</span>
              <select
                required
                value={orderSupplierId}
                onChange={(e) => setOrderSupplierId(e.target.value)}
                className="w-full rounded-md border border-border px-3 py-2 text-sm focus:border-brand focus:outline-none"
              >
                <option value="" disabled>
                  Select a supplier…
                </option>
                {suppliers.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
              </select>
            </label>

            {lines.map((line, i) => (
              <div key={i} className="flex flex-wrap items-end gap-3">
                <label className="block flex-1">
                  <span className="mb-1 block text-xs font-medium text-muted">Product</span>
                  <select
                    required
                    value={line.productId}
                    onChange={(e) => updateLine(i, { productId: e.target.value })}
                    className="w-full rounded-md border border-border px-3 py-1.5 text-sm focus:border-brand focus:outline-none"
                  >
                    <option value="" disabled>
                      Select a product…
                    </option>
                    {products.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.sku} — {p.name}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="block">
                  <span className="mb-1 block text-xs font-medium text-muted">Quantity</span>
                  <input
                    required
                    type="number"
                    value={line.quantity}
                    onChange={(e) => updateLine(i, { quantity: e.target.value })}
                    className="w-24 rounded-md border border-border px-3 py-1.5 text-sm focus:border-brand focus:outline-none"
                  />
                </label>
                <label className="block">
                  <span className="mb-1 block text-xs font-medium text-muted">Unit cost</span>
                  <input
                    required
                    type="number"
                    step="0.01"
                    value={line.unitCost}
                    onChange={(e) => updateLine(i, { unitCost: e.target.value })}
                    className="w-24 rounded-md border border-border px-3 py-1.5 text-sm focus:border-brand focus:outline-none"
                  />
                </label>
                {lines.length > 1 && (
                  <button type="button" onClick={() => setLines((prev) => prev.filter((_, idx) => idx !== i))} className="text-sm text-danger underline">
                    Remove
                  </button>
                )}
              </div>
            ))}
            <button
              type="button"
              onClick={() => setLines((prev) => [...prev, { productId: "", quantity: "", unitCost: "" }])}
              className="text-sm text-foreground underline"
            >
              + Add line
            </button>

            {orderError && <p className="text-sm text-danger">{orderError}</p>}
            <div>
              <button
                type="submit"
                disabled={isSaving}
                className="rounded-md bg-brand px-4 py-2 text-sm font-medium text-brand-foreground hover:bg-brand-hover disabled:opacity-50"
              >
                {isSaving ? "Creating…" : "Create purchase order"}
              </button>
            </div>
          </form>
        </section>

        <section>
          <h2 className="mb-3 text-sm font-medium text-muted">Purchase orders</h2>
          {orders.length === 0 ? (
            <p className="text-sm text-muted">No purchase orders yet.</p>
          ) : (
            <div className="overflow-x-auto rounded-xl border border-border bg-surface shadow-card">
              <table className="w-full text-left text-sm">
                <thead className="border-b border-border text-muted">
                  <tr>
                    <th className="px-3 py-2">Supplier</th>
                    <th className="px-3 py-2">Status</th>
                    <th className="px-3 py-2">Lines</th>
                    <th className="px-3 py-2">Created</th>
                    <th className="px-3 py-2" />
                  </tr>
                </thead>
                <tbody>
                  {orders.map((o) => (
                    <tr key={o.id} className="border-b border-border last:border-0">
                      <td className="px-3 py-2">{suppliers.find((s) => s.id === o.supplierId)?.name ?? o.supplierId}</td>
                      <td className="px-3 py-2">
                        <StatusBadge status={o.status} />
                      </td>
                      <td className="px-3 py-2">{o.items.length}</td>
                      <td className="px-3 py-2">{new Date(o.createdAtUtc).toLocaleDateString()}</td>
                      <td className="px-3 py-2">
                        <Link href={`/purchasing/${o.id}`} className="text-foreground underline">
                          View
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      </main>
    </>
  );
}
