"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { Drawer } from "@/components/ui/drawer";
import { ProductImage } from "@/components/product-image";
import {
  BoxIcon,
  CartIcon,
  ChartIcon,
  LayersIcon,
  SearchIcon,
  SparkIcon,
  TruckIcon,
  UsersIcon,
} from "@/components/ui/icons";
import { Badge, EmptyState, Input, Skeleton, cn } from "@/components/ui/primitives";
import type { PagedResult, ProductDto } from "@/lib/types";

/** Jump targets, so ⌘K doubles as a command palette instead of search-only. */
const DESTINATIONS = [
  { href: "/checkout", label: "New sale", hint: "Checkout", icon: CartIcon },
  { href: "/products", label: "Products", hint: "Catalog", icon: BoxIcon },
  { href: "/inventory", label: "Inventory", hint: "Stock levels", icon: LayersIcon },
  { href: "/purchasing", label: "Purchasing", hint: "Suppliers & orders", icon: TruckIcon },
  { href: "/customers", label: "Customers", hint: "Directory", icon: UsersIcon },
  { href: "/reports", label: "Reports", hint: "Sales analytics", icon: ChartIcon },
  { href: "/assistant", label: "Assistant", hint: "Ask about your store", icon: SparkIcon },
];

/**
 * Mounted with a key tied to its open state (see NavBar), so closing it
 * discards all local state — no reset-on-close effect needed.
 */
export function SearchDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { authFetch } = useAuth();
  const router = useRouter();

  const [query, setQuery] = useState("");
  // Results carry the query they belong to, which makes "still loading"
  // and "stale results from the previous keystroke" derivable instead of
  // needing their own state to keep in sync.
  const [result, setResult] = useState<{ query: string; items: ProductDto[] } | null>(null);
  const [activeIndex, setActiveIndex] = useState(0);
  const listRef = useRef<HTMLDivElement>(null);

  const trimmed = query.trim();
  const products = result && result.query === trimmed ? result.items : null;
  const isSearching = trimmed.length > 0 && products === null;

  const pages = useMemo(() => {
    if (!trimmed) return DESTINATIONS;
    const q = trimmed.toLowerCase();
    return DESTINATIONS.filter((d) => d.label.toLowerCase().includes(q) || d.hint.toLowerCase().includes(q));
  }, [trimmed]);

  // Debounced lookup — a request per keystroke would hammer the API on a
  // barcode scan, which arrives as a fast burst of characters.
  useEffect(() => {
    if (!open || !trimmed) return;
    const timer = setTimeout(async () => {
      try {
        const data = await authFetch<PagedResult<ProductDto>>(
          `/api/v1/products?search=${encodeURIComponent(trimmed)}&pageSize=6`,
        );
        setResult({ query: trimmed, items: data.items });
      } catch {
        setResult({ query: trimmed, items: [] });
      }
    }, 220);
    return () => clearTimeout(timer);
  }, [trimmed, open, authFetch]);

  const go = useCallback(
    (href: string) => {
      onClose();
      router.push(href);
    },
    [onClose, router],
  );

  // Pages render first, then products — so a product's position in the
  // flat keyboard list is simply pages.length + its own index.
  const productOffset = pages.length;
  const itemCount = pages.length + (products?.length ?? 0);

  const hrefAt = useCallback(
    (index: number) => {
      if (index < productOffset) return pages[index]?.href;
      const product = products?.[index - productOffset];
      return product && `/products?search=${encodeURIComponent(product.sku)}`;
    },
    [pages, products, productOffset],
  );

  function onKeyDown(e: React.KeyboardEvent) {
    if (e.key === "ArrowDown" || e.key === "ArrowUp") {
      e.preventDefault();
      if (itemCount === 0) return;
      setActiveIndex((i) => (i + (e.key === "ArrowDown" ? 1 : -1) + itemCount) % itemCount);
    } else if (e.key === "Enter") {
      const href = hrefAt(activeIndex);
      if (href) {
        e.preventDefault();
        go(href);
      }
    }
  }

  useEffect(() => {
    listRef.current?.querySelector('[data-active="true"]')?.scrollIntoView({ block: "nearest" });
  }, [activeIndex]);

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title="Search"
      description="Products, pages and actions"
      width="lg"
      footer={
        <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-[11px] text-subtle">
          <Hint keys="↑↓">navigate</Hint>
          <Hint keys="↵">open</Hint>
          <Hint keys="Esc">close</Hint>
        </div>
      }
    >
      <div className="sticky top-0 z-10 border-b border-border bg-surface px-5 py-3">
        <div className="relative">
          <SearchIcon className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-subtle" />
          <Input
            value={query}
            onChange={(e) => {
              setQuery(e.target.value);
              setActiveIndex(0);
            }}
            onKeyDown={onKeyDown}
            placeholder="Search products, or jump to a page…"
            className="pl-9"
            aria-label="Search"
          />
        </div>
      </div>

      <div ref={listRef} className="px-2 py-2">
        {pages.length > 0 && (
          <Section label="Go to">
            {pages.map((d, i) => {
              const IconComponent = d.icon;
              return (
                <Row
                  key={d.href}
                  active={i === activeIndex}
                  onMouseEnter={() => setActiveIndex(i)}
                  onClick={() => go(d.href)}
                >
                  <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-surface-muted text-muted">
                    <IconComponent className="size-4" />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm text-foreground">{d.label}</span>
                    <span className="block truncate text-xs text-subtle">{d.hint}</span>
                  </span>
                </Row>
              );
            })}
          </Section>
        )}

        {trimmed && (
          <Section label="Products">
            {isSearching ? (
              <div className="space-y-1 px-2 py-1">
                {[0, 1, 2].map((i) => (
                  <div key={i} className="flex items-center gap-3 px-1 py-2">
                    <Skeleton className="size-8 rounded-lg" />
                    <Skeleton className="h-3.5 w-1/2" />
                  </div>
                ))}
              </div>
            ) : products && products.length > 0 ? (
              products.map((p, i) => {
                const index = productOffset + i;
                return (
                  <Row
                    key={p.id}
                    active={index === activeIndex}
                    onMouseEnter={() => setActiveIndex(index)}
                    onClick={() => go(`/products?search=${encodeURIComponent(p.sku)}`)}
                  >
                    <ProductImage productId={p.id} hasImage={p.hasImage} size={32} />
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm text-foreground">{p.name}</span>
                      <span className="block truncate font-mono text-xs text-subtle">{p.sku}</span>
                    </span>
                    <span className="flex shrink-0 items-center gap-2">
                      {!p.isActive && <Badge tone="neutral">Inactive</Badge>}
                      <span className="text-sm font-medium text-foreground tabular">{p.price.toFixed(2)}</span>
                    </span>
                  </Row>
                );
              })
            ) : (
              <EmptyState
                icon={<SearchIcon />}
                title="No products match"
                description={`Nothing found for “${trimmed}”. Try a different name, SKU or barcode.`}
              />
            )}
          </Section>
        )}

        {!trimmed && (
          <p className="px-4 py-3 text-xs text-subtle">
            Start typing to search the catalog by name, SKU or barcode.
          </p>
        )}
      </div>
    </Drawer>
  );
}

function Section({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="mb-1">
      <p className="px-3 pt-2 pb-1 text-[11px] font-medium tracking-wide text-subtle uppercase">{label}</p>
      {children}
    </div>
  );
}

function Row({ active, children, ...props }: { active: boolean } & React.ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      type="button"
      data-active={active}
      className={cn(
        "flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left transition-colors",
        active ? "bg-surface-muted" : "hover:bg-surface-muted",
      )}
      {...props}
    >
      {children}
    </button>
  );
}

function Hint({ keys, children }: { keys: string; children: React.ReactNode }) {
  return (
    <span className="flex items-center gap-1.5">
      <kbd className="rounded border border-border bg-surface px-1.5 py-0.5 font-sans text-[10px] text-muted">{keys}</kbd>
      {children}
    </span>
  );
}
