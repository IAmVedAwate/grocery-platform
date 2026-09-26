"use client";

import { useEffect, useState } from "react";
import { useAuth } from "@/lib/auth-context";

/**
 * The image endpoint requires a Bearer token like every other resource
 * (docs/architecture/authentication-flow.md — tenant-scoped, permission-
 * gated), so a plain <img src="..."> can't be used directly: the browser
 * never attaches a custom Authorization header to an <img> request. This
 * fetches the bytes through authFetchBlob instead and renders them as an
 * object URL, revoked on unmount/change to avoid leaking blob URLs.
 */
export function ProductImage({ productId, hasImage, size = 40 }: { productId: string; hasImage: boolean; size?: number }) {
  const { authFetchBlob } = useAuth();
  const [url, setUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!hasImage) {
      // Resetting to null here, not deriving it at render time, is
      // deliberate: hasImage flipping false→true→false (upload, then
      // remove) must drop the *stale* blob URL from the previous image,
      // not just stop showing it — same "sync local state to a changing
      // external resource" pattern already accepted elsewhere in this
      // codebase (see the loadProducts()/loadStaff() effects), just
      // synchronous here instead of after an await.
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setUrl(null);
      return;
    }
    let objectUrl: string | null = null;
    let cancelled = false;
    authFetchBlob(`/api/v1/products/${productId}/image`)
      .then((blob) => {
        if (cancelled) return;
        objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
      })
      .catch(() => setUrl(null));
    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [productId, hasImage, authFetchBlob]);

  if (!url) {
    return (
      <div
        style={{ width: size, height: size }}
        className="flex shrink-0 items-center justify-center rounded-md border border-dashed border-border text-[10px] text-subtle"
      >
        No image
      </div>
    );
  }

  // eslint-disable-next-line @next/next/no-img-element -- object URL, not a static asset next/image can optimize
  return <img src={url} alt="" style={{ width: size, height: size }} className="shrink-0 rounded-md border border-border object-cover" />;
}
