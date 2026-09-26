"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense } from "react";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { NavBar } from "@/components/nav-bar";
import { ProductImage } from "@/components/product-image";
import { Drawer } from "@/components/ui/drawer";
import { BoxIcon, PlusIcon, SearchIcon } from "@/components/ui/icons";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  Field,
  Input,
  PageHeader,
  PageShell,
  Pagination,
  TableSkeleton,
} from "@/components/ui/primitives";
import type { PagedResult, ProductDto } from "@/lib/types";

const PAGE_SIZE = 20;

function ProductsContent() {
  const { accessToken, isLoading, authFetch } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();

  const [result, setResult] = useState<PagedResult<ProductDto> | null>(null);
  const [page, setPage] = useState(1);
  // Seeded from ?search= so the ⌘K palette can deep-link into a filtered list.
  const [search, setSearch] = useState(searchParams.get("search") ?? "");
  const [debouncedSearch, setDebouncedSearch] = useState(search);
  const [listError, setListError] = useState<string | null>(null);

  // Product registration is a P0 speed criterion (docs/PRD.md §7) — only
  // sku/name/price/tax are required, everything else is optional.
  const [formOpen, setFormOpen] = useState(false);
  const [sku, setSku] = useState("");
  const [name, setName] = useState("");
  const [price, setPrice] = useState("");
  const [taxRatePercent, setTaxRatePercent] = useState("0");
  const [barcode, setBarcode] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [uploadingId, setUploadingId] = useState<string | null>(null);
  const [imageError, setImageError] = useState<string | null>(null);

  // Typing no longer fires a request per keystroke.
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1);
    }, 250);
    return () => clearTimeout(timer);
  }, [search]);

  const loadProducts = useCallback(async () => {
    setListError(null);
    try {
      const query = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE) });
      if (debouncedSearch.trim()) query.set("search", debouncedSearch.trim());
      const data = await authFetch<PagedResult<ProductDto>>(`/api/v1/products?${query}`);
      setResult(data);
    } catch (err) {
      setListError(err instanceof ApiError ? err.message : "Could not load products.");
    }
  }, [authFetch, page, debouncedSearch]);

  useEffect(() => {
    if (!isLoading && !accessToken) router.push("/login");
  }, [isLoading, accessToken, router]);

  useEffect(() => {
    // loadProducts is async and only calls setState after its await —
    // this is the standard "fetch when a dependency changes" pattern, not
    // the synchronous-setState-in-effect case react-hooks/set-state-in-effect
    // warns about.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (accessToken) void loadProducts();
  }, [accessToken, loadProducts]);

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    setFormError(null);
    setIsSaving(true);
    try {
      await authFetch("/api/v1/products", {
        method: "POST",
        body: JSON.stringify({
          sku,
          name,
          price: Number(price),
          taxRatePercent: Number(taxRatePercent),
          barcode: barcode || null,
          categoryId: null,
          brandId: null,
          unitId: null,
          lowStockThreshold: 0,
        }),
      });
      setSku("");
      setName("");
      setPrice("");
      setTaxRatePercent("0");
      setBarcode("");
      setPage(1);
      setFormOpen(false);
      await loadProducts();
    } catch (err) {
      setFormError(err instanceof ApiError ? err.message : "Could not save product.");
    } finally {
      setIsSaving(false);
    }
  }

  async function handleImageChange(productId: string, file: File) {
    setImageError(null);
    setUploadingId(productId);
    try {
      const body = new FormData();
      body.append("image", file);
      await authFetch(`/api/v1/products/${productId}/image`, { method: "PUT", body });
      await loadProducts();
    } catch (err) {
      setImageError(err instanceof ApiError ? err.message : "Could not upload image.");
    } finally {
      setUploadingId(null);
    }
  }

  if (isLoading || !accessToken) return null;

  const totalPages = result ? Math.max(1, Math.ceil(result.totalCount / PAGE_SIZE)) : 1;

  return (
    <>
      <NavBar />
      <PageShell>
        <PageHeader
          title="Products"
          description="Your catalog — searchable by name, SKU or barcode."
          action={
            <Button onClick={() => setFormOpen(true)}>
              <PlusIcon className="size-4" />
              New product
            </Button>
          }
        />

        <div className="relative mb-4 max-w-sm">
          <SearchIcon className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-subtle" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search products…"
            aria-label="Search products"
            className="pl-9"
          />
        </div>

        {listError && <div className="mb-4"><Alert>{listError}</Alert></div>}
        {imageError && <div className="mb-4"><Alert>{imageError}</Alert></div>}

        <Card className="overflow-hidden">
          {!result ? (
            <TableSkeleton rows={6} cols={5} />
          ) : result.items.length === 0 ? (
            <EmptyState
              icon={<BoxIcon />}
              title={debouncedSearch ? "No matching products" : "No products yet"}
              description={
                debouncedSearch
                  ? `Nothing matches “${debouncedSearch}”.`
                  : "Register your first product to start selling."
              }
              action={
                !debouncedSearch && (
                  <Button onClick={() => setFormOpen(true)}>
                    <PlusIcon className="size-4" />
                    New product
                  </Button>
                )
              }
            />
          ) : (
            <>
              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm">
                  <thead>
                    <tr className="border-b border-border text-xs text-muted">
                      <th scope="col" className="px-5 py-2.5 font-medium">Product</th>
                      <th scope="col" className="px-5 py-2.5 font-medium">SKU</th>
                      <th scope="col" className="px-5 py-2.5 text-right font-medium">Price</th>
                      <th scope="col" className="px-5 py-2.5 text-right font-medium">Tax</th>
                      <th scope="col" className="px-5 py-2.5 font-medium">Status</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border">
                    {result.items.map((p) => (
                      <tr key={p.id} className="transition-colors hover:bg-surface-hover">
                        <td className="px-5 py-3">
                          <div className="flex items-center gap-3">
                            <span className="relative shrink-0">
                              <ProductImage productId={p.id} hasImage={p.hasImage} />
                              {/* The dominant colour sits as a dot beside the
                                  thumbnail rather than tinting the whole row —
                                  it stays a usable "find it by colour" cue
                                  (docs/PRD.md) without wrecking text contrast. */}
                              {p.dominantColorHex && (
                                <span
                                  aria-hidden="true"
                                  title={p.dominantColorHex}
                                  className="absolute -right-1 -bottom-1 size-3 rounded-full border-2 border-surface"
                                  style={{ backgroundColor: p.dominantColorHex }}
                                />
                              )}
                            </span>
                            <div className="min-w-0">
                              <p className="truncate font-medium">{p.name}</p>
                              <label className="cursor-pointer text-xs text-subtle underline-offset-2 hover:text-muted hover:underline">
                                {uploadingId === p.id ? "Uploading…" : p.hasImage ? "Change image" : "Add image"}
                                <input
                                  type="file"
                                  accept="image/png,image/jpeg,image/webp"
                                  className="hidden"
                                  disabled={uploadingId === p.id}
                                  onChange={(e) => {
                                    const file = e.target.files?.[0];
                                    e.target.value = "";
                                    if (file) void handleImageChange(p.id, file);
                                  }}
                                />
                              </label>
                            </div>
                          </div>
                        </td>
                        <td className="px-5 py-3 font-mono text-xs text-muted">{p.sku}</td>
                        <td className="px-5 py-3 text-right font-medium tabular">{p.price.toFixed(2)}</td>
                        <td className="px-5 py-3 text-right text-muted tabular">{p.taxRatePercent}%</td>
                        <td className="px-5 py-3">
                          <Badge tone={p.isActive ? "success" : "neutral"}>{p.isActive ? "Active" : "Inactive"}</Badge>
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

      <Drawer
        open={formOpen}
        onClose={() => setFormOpen(false)}
        title="New product"
        description="SKU, name and price are all that's required."
        footer={
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setFormOpen(false)}>
              Cancel
            </Button>
            <Button form="new-product" type="submit" disabled={isSaving}>
              {isSaving ? "Saving…" : "Add product"}
            </Button>
          </div>
        }
      >
        <form id="new-product" onSubmit={handleCreate} className="space-y-4 p-5">
          <Field label="SKU" value={sku} onChange={setSku} placeholder="RICE-5KG" />
          <Field label="Name" value={name} onChange={setName} placeholder="Basmati Rice 5kg" />
          <Field label="Barcode" hint="optional" value={barcode} onChange={setBarcode} required={false} />
          <div className="grid grid-cols-2 gap-3">
            <Field label="Price" type="number" step="0.01" min="0" value={price} onChange={setPrice} placeholder="0.00" />
            <Field label="Tax %" type="number" step="0.01" min="0" value={taxRatePercent} onChange={setTaxRatePercent} />
          </div>
          {formError && <Alert>{formError}</Alert>}
        </form>
      </Drawer>
    </>
  );
}

export default function ProductsPage() {
  // useSearchParams needs a Suspense boundary to stay statically rendered.
  return (
    <Suspense fallback={null}>
      <ProductsContent />
    </Suspense>
  );
}
