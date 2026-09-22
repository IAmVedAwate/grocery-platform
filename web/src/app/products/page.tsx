"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { Field } from "@/components/field";
import { NavBar } from "@/components/nav-bar";
import { ProductImage } from "@/components/product-image";
import type { PagedResult, ProductDto } from "@/lib/types";

const PAGE_SIZE = 20;

export default function ProductsPage() {
  const { accessToken, isLoading, authFetch } = useAuth();
  const router = useRouter();

  const [result, setResult] = useState<PagedResult<ProductDto> | null>(null);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [listError, setListError] = useState<string | null>(null);

  // Product registration is a P0 speed criterion (docs/PRD.md §7) — only
  // sku/name/price/tax are required, everything else is optional.
  const [sku, setSku] = useState("");
  const [name, setName] = useState("");
  const [price, setPrice] = useState("");
  const [taxRatePercent, setTaxRatePercent] = useState("0");
  const [barcode, setBarcode] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [uploadingId, setUploadingId] = useState<string | null>(null);
  const [imageError, setImageError] = useState<string | null>(null);

  const loadProducts = useCallback(async () => {
    setListError(null);
    try {
      const query = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE) });
      if (search.trim()) query.set("search", search.trim());
      const data = await authFetch<PagedResult<ProductDto>>(`/api/v1/products?${query}`);
      setResult(data);
    } catch (err) {
      setListError(err instanceof ApiError ? err.message : "Could not load products.");
    }
  }, [authFetch, page, search]);

  useEffect(() => {
    if (!isLoading && !accessToken) router.push("/login");
  }, [isLoading, accessToken, router]);

  useEffect(() => {
    // loadProducts is async and only calls setState after its await —
    // this is the standard "fetch when a dependency changes" pattern, not
    // the synchronous-setState-in-effect case react-hooks/set-state-in-effect
    // warns about. TanStack Query replaces this manual fetch effect when
    // real UI work continues past this scaffold (see web/README.md).
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
      <main className="mx-auto w-full max-w-4xl flex-1 p-6">
      <h1 className="mb-6 text-xl font-semibold">Products</h1>

      <form onSubmit={handleCreate} className="mb-8 rounded-lg border border-gray-200 bg-white p-4">
        <h2 className="mb-3 text-sm font-medium text-gray-700">Register a product</h2>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          <Field label="SKU" value={sku} onChange={setSku} />
          <Field label="Name" value={name} onChange={setName} />
          <Field label="Barcode" value={barcode} onChange={setBarcode} required={false} />
          <Field label="Price" type="number" value={price} onChange={setPrice} />
          <Field label="Tax %" type="number" value={taxRatePercent} onChange={setTaxRatePercent} />
        </div>
        {formError && <p className="mt-2 text-sm text-red-600">{formError}</p>}
        <button
          type="submit"
          disabled={isSaving}
          className="mt-3 rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white hover:bg-gray-800 disabled:opacity-50"
        >
          {isSaving ? "Saving…" : "Add product"}
        </button>
      </form>

      <div className="mb-3 flex items-center gap-2">
        <input
          value={search}
          onChange={(e) => {
            setPage(1);
            setSearch(e.target.value);
          }}
          placeholder="Search by name, SKU, or barcode…"
          className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-500 focus:outline-none"
        />
      </div>

      {listError && <p className="mb-3 text-sm text-red-600">{listError}</p>}
      {imageError && <p className="mb-3 text-sm text-red-600">{imageError}</p>}

      {!result ? (
        <p className="text-sm text-gray-500">Loading…</p>
      ) : result.items.length === 0 ? (
        <p className="text-sm text-gray-500">No products yet.</p>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-gray-200 text-gray-500">
              <tr>
                <th className="px-3 py-2">Image</th>
                <th className="px-3 py-2">SKU</th>
                <th className="px-3 py-2">Name</th>
                <th className="px-3 py-2">Price</th>
                <th className="px-3 py-2">Tax %</th>
                <th className="px-3 py-2">Status</th>
              </tr>
            </thead>
            <tbody>
              {result.items.map((p) => (
                <tr
                  key={p.id}
                  className="border-b border-gray-100 last:border-0"
                  // 50% opacity so text stays readable over any color
                  // (docs/PRD.md — "find a product by color" browsing:
                  // people often recall a product's color before its name).
                  style={p.dominantColorHex ? { backgroundColor: `${p.dominantColorHex}80` } : undefined}
                >
                  <td className="px-3 py-2">
                    <div className="flex items-center gap-2">
                      <ProductImage productId={p.id} hasImage={p.hasImage} />
                      <label className="cursor-pointer text-xs text-gray-500 underline">
                        {uploadingId === p.id ? "Uploading…" : p.hasImage ? "Change" : "Upload"}
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
                  </td>
                  <td className="px-3 py-2">{p.sku}</td>
                  <td className="px-3 py-2">{p.name}</td>
                  <td className="px-3 py-2">{p.price.toFixed(2)}</td>
                  <td className="px-3 py-2">{p.taxRatePercent}</td>
                  <td className="px-3 py-2">{p.isActive ? "Active" : "Inactive"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {result && result.totalCount > 0 && (
        <div className="mt-3 flex items-center justify-between text-sm text-gray-500">
          <span>
            Page {result.page} of {totalPages} ({result.totalCount} total)
          </span>
          <div className="flex gap-2">
            <button
              disabled={page <= 1}
              onClick={() => setPage((p) => p - 1)}
              className="rounded-md border border-gray-300 px-3 py-1 disabled:opacity-50"
            >
              Previous
            </button>
            <button
              disabled={page >= totalPages}
              onClick={() => setPage((p) => p + 1)}
              className="rounded-md border border-gray-300 px-3 py-1 disabled:opacity-50"
            >
              Next
            </button>
          </div>
        </div>
      )}
      </main>
    </>
  );
}
