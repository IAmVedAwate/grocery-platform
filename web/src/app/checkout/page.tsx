"use client";

import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { NavBar } from "@/components/nav-bar";
import { ProductImage } from "@/components/product-image";
import { Drawer } from "@/components/ui/drawer";
import { CartIcon, CheckIcon, PlusIcon, ScanIcon, TrashIcon } from "@/components/ui/icons";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  Input,
  Label,
  PageShell,
  Select,
  cn,
} from "@/components/ui/primitives";
import type { CustomerDto, PagedResult, ProductDto, SalesOrderDto } from "@/lib/types";

type CartLine = { product: ProductDto; quantity: number };

export default function CheckoutPage() {
  const { accessToken, isLoading, authFetch } = useAuth();
  const router = useRouter();
  const searchInputRef = useRef<HTMLInputElement>(null);

  const [search, setSearch] = useState("");
  const [matches, setMatches] = useState<ProductDto[]>([]);
  const [resultsOpen, setResultsOpen] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [isSearching, setIsSearching] = useState(false);

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

  const addToCart = useCallback((product: ProductDto) => {
    setCart((prev) => {
      const existing = prev.find((l) => l.product.id === product.id);
      if (existing) {
        return prev.map((l) => (l.product.id === product.id ? { ...l, quantity: l.quantity + 1 } : l));
      }
      return [...prev, { product, quantity: 1 }];
    });
    setSearch("");
    setMatches([]);
    setResultsOpen(false);
    searchInputRef.current?.focus();
  }, []);

  async function runSearch(e: FormEvent) {
    e.preventDefault();
    setSearchError(null);
    if (!search.trim()) return;
    setIsSearching(true);
    try {
      const result = await authFetch<PagedResult<ProductDto>>(
        `/api/v1/products?search=${encodeURIComponent(search.trim())}&isActive=true&pageSize=20`,
      );
      // A barcode scan (or an exact SKU) that resolves to exactly one
      // product adds it straight to the cart — this is the fast path
      // the acceptance criteria in docs/PRD.md §7 actually care about,
      // so it must NOT be interrupted by opening the results drawer.
      if (result.items.length === 1) {
        addToCart(result.items[0]);
      } else {
        setMatches(result.items);
        setResultsOpen(true);
      }
    } catch (err) {
      setSearchError(err instanceof ApiError ? err.message : "Search failed.");
    } finally {
      setIsSearching(false);
      searchInputRef.current?.focus();
    }
  }

  function updateQuantity(productId: string, quantity: number) {
    setCart((prev) =>
      prev.map((l) => (l.product.id === productId ? { ...l, quantity } : l)).filter((l) => l.quantity > 0),
    );
  }

  function removeLine(productId: string) {
    setCart((prev) => prev.filter((l) => l.product.id !== productId));
  }

  const subtotal = cart.reduce((sum, l) => sum + l.product.price * l.quantity, 0);
  const tax = cart.reduce((sum, l) => sum + (l.product.price * l.quantity * l.product.taxRatePercent) / 100, 0);
  const total = subtotal + tax;
  const itemCount = cart.reduce((sum, l) => sum + l.quantity, 0);

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
        <PageShell width="narrow">
          <Card className="overflow-hidden">
            <div className="flex flex-col items-center px-6 py-10 text-center">
              <span className="mb-4 flex size-14 items-center justify-center rounded-full bg-success-soft text-success-soft-foreground">
                <CheckIcon className="size-7" />
              </span>
              <h1 className="text-lg font-semibold">Sale complete</h1>
              <p className="mt-1 font-mono text-xs text-muted">{completedOrder.invoiceNumber}</p>
              <p className="mt-5 text-4xl font-semibold tracking-tight tabular">
                {completedOrder.totalAmount.toFixed(2)}
              </p>
              <p className="mt-1 text-xs text-muted">
                {completedOrder.items.length} {completedOrder.items.length === 1 ? "line" : "lines"} ·{" "}
                {new Date(completedOrder.createdAtUtc).toLocaleTimeString()}
              </p>
              <div className="mt-7 flex w-full flex-col gap-2 sm:flex-row">
                <Button
                  className="flex-1"
                  onClick={() => {
                    setCompletedOrder(null);
                    requestAnimationFrame(() => searchInputRef.current?.focus());
                  }}
                >
                  <PlusIcon className="size-4" />
                  New sale
                </Button>
                <Button
                  variant="secondary"
                  className="flex-1"
                  onClick={() => router.push(`/sales/${completedOrder.id}`)}
                >
                  View receipt
                </Button>
              </div>
            </div>
          </Card>
        </PageShell>
      </>
    );
  }

  return (
    <>
      <NavBar />
      <PageShell>
        {/* Scanner field is the whole point of this screen — it gets the
            top slot, full width, and keeps focus after every action. */}
        <Card className="mb-5 overflow-hidden">
          <form onSubmit={runSearch} className="flex flex-col gap-2 p-3 sm:flex-row sm:items-center">
            <div className="relative flex-1">
              <ScanIcon className="pointer-events-none absolute top-1/2 left-3 size-5 -translate-y-1/2 text-subtle" />
              <Input
                ref={searchInputRef}
                autoFocus
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Scan barcode or search by name / SKU…"
                aria-label="Scan or search for a product"
                className="h-12 pl-11 text-base"
              />
            </div>
            <Button type="submit" size="lg" disabled={isSearching || !search.trim()} className="sm:w-32">
              {isSearching ? "Searching…" : "Add"}
            </Button>
          </form>
        </Card>

        {searchError && (
          <div className="mb-4">
            <Alert>{searchError}</Alert>
          </div>
        )}

        <div className="grid gap-5 lg:grid-cols-[1fr_20rem] lg:items-start">
          <Card className="overflow-hidden">
            <div className="flex items-center justify-between border-b border-border px-5 py-3">
              <h2 className="text-sm font-semibold">Cart</h2>
              {itemCount > 0 && (
                <Badge tone="brand">
                  {itemCount} {itemCount === 1 ? "item" : "items"}
                </Badge>
              )}
            </div>

            {cart.length === 0 ? (
              <EmptyState
                icon={<CartIcon />}
                title="Cart is empty"
                description="Scan a barcode or search for a product to start a sale."
              />
            ) : (
              <ul className="divide-y divide-border">
                {cart.map((line) => (
                  <li key={line.product.id} className="flex items-center gap-3 px-4 py-3 sm:px-5">
                    <ProductImage productId={line.product.id} hasImage={line.product.hasImage} size={40} />
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium">{line.product.name}</p>
                      <p className="truncate font-mono text-xs text-subtle">
                        {line.product.sku} · {line.product.price.toFixed(2)}
                        {line.product.taxRatePercent > 0 && ` +${line.product.taxRatePercent}% tax`}
                      </p>
                    </div>

                    <div className="flex shrink-0 items-center rounded-lg border border-border">
                      <Stepper
                        label="Decrease quantity"
                        onClick={() => updateQuantity(line.product.id, line.quantity - 1)}
                      >
                        −
                      </Stepper>
                      <input
                        type="number"
                        min={1}
                        value={line.quantity}
                        onChange={(e) => updateQuantity(line.product.id, Number(e.target.value))}
                        aria-label={`Quantity for ${line.product.name}`}
                        className="h-8 w-11 border-x border-border bg-transparent text-center text-sm tabular focus:outline-none"
                      />
                      <Stepper
                        label="Increase quantity"
                        onClick={() => updateQuantity(line.product.id, line.quantity + 1)}
                      >
                        +
                      </Stepper>
                    </div>

                    <p className="w-20 shrink-0 text-right text-sm font-medium tabular">
                      {(line.product.price * line.quantity).toFixed(2)}
                    </p>

                    <button
                      onClick={() => removeLine(line.product.id)}
                      aria-label={`Remove ${line.product.name}`}
                      className="shrink-0 rounded-lg p-1.5 text-subtle transition-colors hover:bg-danger-soft hover:text-danger-soft-foreground"
                    >
                      <TrashIcon className="size-4" />
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </Card>

          {/* Sticky on desktop so the total stays visible down a long cart. */}
          <Card className="overflow-hidden lg:sticky lg:top-20">
            <div className="space-y-3 p-5">
              <label className="block">
                <Label>Customer</Label>
                <Select value={customerId} onChange={(e) => setCustomerId(e.target.value)}>
                  <option value="">Walk-in</option>
                  {customers.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
                </Select>
              </label>

              <div>
                <Label>Payment method</Label>
                <div className="grid grid-cols-2 gap-2">
                  {(["Cash", "Card"] as const).map((method) => (
                    <button
                      key={method}
                      type="button"
                      onClick={() => setPaymentMethod(method)}
                      aria-pressed={paymentMethod === method}
                      className={cn(
                        "h-10 rounded-lg border text-sm font-medium transition-colors",
                        paymentMethod === method
                          ? "border-brand bg-brand-soft text-brand-soft-foreground"
                          : "border-border text-muted hover:border-border-strong hover:text-foreground",
                      )}
                    >
                      {method}
                    </button>
                  ))}
                </div>
              </div>
            </div>

            <dl className="space-y-1.5 border-t border-border px-5 py-4 text-sm">
              <Row label="Subtotal" value={subtotal} />
              <Row label="Tax" value={tax} />
              <div className="flex items-baseline justify-between border-t border-border pt-2.5">
                <dt className="text-sm font-medium">Total</dt>
                <dd className="text-2xl font-semibold tracking-tight tabular">{total.toFixed(2)}</dd>
              </div>
            </dl>

            <div className="space-y-2 border-t border-border bg-surface-muted p-4">
              {checkoutError && <Alert>{checkoutError}</Alert>}
              <Button onClick={checkout} disabled={cart.length === 0 || isCharging} size="lg" className="w-full">
                {isCharging ? "Processing…" : `Charge ${total.toFixed(2)}`}
              </Button>
            </div>
          </Card>
        </div>
      </PageShell>

      {/* Multiple matches land here rather than pushing the cart down the
          page — the cashier picks one and the drawer closes. */}
      <Drawer
        open={resultsOpen}
        onClose={() => setResultsOpen(false)}
        title={`${matches.length} matches`}
        description={`for “${search.trim()}”`}
        width="lg"
      >
        {matches.length === 0 ? (
          <EmptyState icon={<ScanIcon />} title="No products found" description="Try a different name, SKU or barcode." />
        ) : (
          <ul className="divide-y divide-border">
            {matches.map((p) => (
              <li key={p.id}>
                <button
                  onClick={() => addToCart(p)}
                  className="flex w-full items-center gap-3 px-5 py-3 text-left transition-colors hover:bg-surface-muted"
                >
                  <ProductImage productId={p.id} hasImage={p.hasImage} size={40} />
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm font-medium">{p.name}</span>
                    <span className="block truncate font-mono text-xs text-subtle">{p.sku}</span>
                  </span>
                  <span className="shrink-0 text-sm font-medium tabular">{p.price.toFixed(2)}</span>
                  <PlusIcon className="size-4 shrink-0 text-subtle" />
                </button>
              </li>
            ))}
          </ul>
        )}
      </Drawer>
    </>
  );
}

function Stepper({ label, onClick, children }: { label: string; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={label}
      className="flex size-8 items-center justify-center text-muted transition-colors hover:bg-surface-muted hover:text-foreground"
    >
      {children}
    </button>
  );
}

function Row({ label, value }: { label: string; value: number }) {
  return (
    <div className="flex items-baseline justify-between">
      <dt className="text-xs text-muted">{label}</dt>
      <dd className="text-sm tabular">{value.toFixed(2)}</dd>
    </div>
  );
}
