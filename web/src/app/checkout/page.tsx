"use client";

import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { NavBar } from "@/components/nav-bar";
import type { CustomerDto, PagedResult, ProductDto, SalesOrderDto } from "@/lib/types";

type CartLine = { product: ProductDto; quantity: number };

export default function CheckoutPage() {
  const { accessToken, isLoading, authFetch } = useAuth();
  const router = useRouter();
  const searchInputRef = useRef<HTMLInputElement>(null);

  const [search, setSearch] = useState("");
  const [matches, setMatches] = useState<ProductDto[]>([]);
  const [searchError, setSearchError] = useState<string | null>(null);

  const [cart, setCart] = useState<CartLine[]>([]);
  const [customers, setCustomers] = useState<CustomerDto[]>([]);
  const [customerId, setCustomerId] = useState("");
  const [paymentMethod, setPaymentMethod] = useState<"Cash" | "Card">("Cash");
  const [checkoutError, setCheckoutError] = useState<string | null>(null);
  const [isCharging, setIsCharging] = useState(false);
  const [completedOrder, setCompletedOrder] = useState<SalesOrderDto | null>(null);

  useEffect(() => {
    if (!isLoading && !accessToken) router.push("/login");
  }, [isLoading, accessToken, router]);

  const loadCustomers = useCallback(async () => {
    try {
      const result = await authFetch<PagedResult<CustomerDto>>("/api/v1/customers?pageSize=100");
      setCustomers(result.items);
    } catch {
      // Customer selection is optional — a failed load here shouldn't
      // block checkout itself.
    }
  }, [authFetch]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (accessToken) void loadCustomers();
  }, [accessToken, loadCustomers]);

  async function runSearch(e: FormEvent) {
    e.preventDefault();
    setSearchError(null);
    if (!search.trim()) return;
    try {
      const result = await authFetch<PagedResult<ProductDto>>(
        `/api/v1/products?search=${encodeURIComponent(search.trim())}&isActive=true&pageSize=5`,
      );
      setMatches(result.items);
      // A barcode scan (or an exact SKU) that resolves to exactly one
      // product adds it straight to the cart — this is the fast path
      // the acceptance criteria in docs/PRD.md §7 actually care about.
      if (result.items.length === 1) {
        addToCart(result.items[0]);
        setSearch("");
        setMatches([]);
      }
    } catch (err) {
      setSearchError(err instanceof ApiError ? err.message : "Search failed.");
    } finally {
      searchInputRef.current?.focus();
    }
  }

  function addToCart(product: ProductDto) {
    setCart((prev) => {
      const existing = prev.find((l) => l.product.id === product.id);
      if (existing) return prev.map((l) => (l.product.id === product.id ? { ...l, quantity: l.quantity + 1 } : l));
      return [...prev, { product, quantity: 1 }];
    });
    setSearch("");
    setMatches([]);
    searchInputRef.current?.focus();
  }

  function updateQuantity(productId: string, quantity: number) {
    setCart((prev) => prev.map((l) => (l.product.id === productId ? { ...l, quantity } : l)).filter((l) => l.quantity > 0));
  }

  function removeLine(productId: string) {
    setCart((prev) => prev.filter((l) => l.product.id !== productId));
  }

  const subtotal = cart.reduce((sum, l) => sum + l.product.price * l.quantity, 0);
  const tax = cart.reduce((sum, l) => sum + (l.product.price * l.quantity * l.product.taxRatePercent) / 100, 0);
  const total = subtotal + tax;

  async function checkout() {
    setCheckoutError(null);
    setIsCharging(true);
    try {
      const order = await authFetch<SalesOrderDto>("/api/v1/sales-orders", {
        method: "POST",
        headers: { "Idempotency-Key": crypto.randomUUID() },
        body: JSON.stringify({
          customerId: customerId || null,
          paymentMethod,
          lines: cart.map((l) => ({ productId: l.product.id, quantity: l.quantity, lineDiscount: null })),
        }),
      });
      setCompletedOrder(order);
      setCart([]);
      setCustomerId("");
    } catch (err) {
      setCheckoutError(err instanceof ApiError ? err.message : "Checkout failed.");
    } finally {
      setIsCharging(false);
    }
  }

  if (isLoading || !accessToken) return null;

  if (completedOrder) {
    return (
      <>
        <NavBar />
        <main className="mx-auto w-full max-w-md flex-1 p-6">
          <div className="rounded-lg border border-green-200 bg-green-50 p-6 text-center">
            <h1 className="mb-2 text-xl font-semibold text-green-900">Sale complete</h1>
            <p className="mb-1 text-sm text-green-800">Invoice {completedOrder.invoiceNumber}</p>
            <p className="mb-4 text-2xl font-semibold text-green-900">{completedOrder.totalAmount.toFixed(2)}</p>
            <button
              onClick={() => setCompletedOrder(null)}
              className="rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white hover:bg-gray-800"
            >
              New sale
            </button>
          </div>
        </main>
      </>
    );
  }

  return (
    <>
      <NavBar />
      <main className="mx-auto w-full max-w-3xl flex-1 p-6">
        <h1 className="mb-6 text-xl font-semibold">Checkout</h1>

        <form onSubmit={runSearch} className="mb-4 flex gap-2">
          <input
            ref={searchInputRef}
            autoFocus
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Scan barcode or search by name / SKU…"
            className="flex-1 rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-500 focus:outline-none"
          />
          <button type="submit" className="rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white hover:bg-gray-800">
            Add
          </button>
        </form>
        {searchError && <p className="mb-3 text-sm text-red-600">{searchError}</p>}
        {matches.length > 1 && (
          <div className="mb-4 rounded-lg border border-gray-200 bg-white p-2">
            {matches.map((p) => (
              <button
                key={p.id}
                onClick={() => addToCart(p)}
                className="block w-full rounded px-2 py-1.5 text-left text-sm hover:bg-gray-100"
              >
                {p.sku} — {p.name} ({p.price.toFixed(2)})
              </button>
            ))}
          </div>
        )}

        {cart.length === 0 ? (
          <p className="mb-6 text-sm text-gray-500">Cart is empty — scan or search for a product to begin.</p>
        ) : (
          <div className="mb-6 overflow-x-auto rounded-lg border border-gray-200 bg-white">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-gray-200 text-gray-500">
                <tr>
                  <th className="px-3 py-2">Product</th>
                  <th className="px-3 py-2">Qty</th>
                  <th className="px-3 py-2">Price</th>
                  <th className="px-3 py-2">Line total</th>
                  <th className="px-3 py-2" />
                </tr>
              </thead>
              <tbody>
                {cart.map((line) => (
                  <tr key={line.product.id} className="border-b border-gray-100 last:border-0">
                    <td className="px-3 py-2">{line.product.name}</td>
                    <td className="px-3 py-2">
                      <input
                        type="number"
                        min={1}
                        value={line.quantity}
                        onChange={(e) => updateQuantity(line.product.id, Number(e.target.value))}
                        className="w-16 rounded-md border border-gray-300 px-2 py-1 text-sm focus:border-gray-500 focus:outline-none"
                      />
                    </td>
                    <td className="px-3 py-2">{line.product.price.toFixed(2)}</td>
                    <td className="px-3 py-2">{(line.product.price * line.quantity).toFixed(2)}</td>
                    <td className="px-3 py-2">
                      <button onClick={() => removeLine(line.product.id)} className="text-sm text-red-600 underline">
                        Remove
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="mb-6 flex flex-wrap items-end gap-4 rounded-lg border border-gray-200 bg-white p-4">
          <label className="block">
            <span className="mb-1 block text-xs font-medium text-gray-700">Customer (optional)</span>
            <select
              value={customerId}
              onChange={(e) => setCustomerId(e.target.value)}
              className="w-56 rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-500 focus:outline-none"
            >
              <option value="">Walk-in</option>
              {customers.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </label>
          <label className="block">
            <span className="mb-1 block text-xs font-medium text-gray-700">Payment method</span>
            <select
              value={paymentMethod}
              onChange={(e) => setPaymentMethod(e.target.value as "Cash" | "Card")}
              className="w-32 rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-500 focus:outline-none"
            >
              <option value="Cash">Cash</option>
              <option value="Card">Card</option>
            </select>
          </label>

          <div className="ml-auto text-right">
            <p className="text-xs text-gray-500">Subtotal {subtotal.toFixed(2)} · Tax {tax.toFixed(2)}</p>
            <p className="text-lg font-semibold">{total.toFixed(2)}</p>
          </div>
        </div>

        {checkoutError && <p className="mb-3 text-sm text-red-600">{checkoutError}</p>}
        <button
          onClick={checkout}
          disabled={cart.length === 0 || isCharging}
          className="w-full rounded-md bg-gray-900 px-4 py-3 text-sm font-medium text-white hover:bg-gray-800 disabled:opacity-50"
        >
          {isCharging ? "Processing…" : `Charge ${total.toFixed(2)}`}
        </button>
      </main>
    </>
  );
}
