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
        className="flex shrink-0 items-center justify-center rounded-md border border-dashed border-gray-300 text-[10px] text-gray-400"
      >
        No image
      </div>
    );
  }

  // eslint-disable-next-line @next/next/no-img-element -- object URL, not a static asset next/image can optimize
  return <img src={url} alt="" style={{ width: size, height: size }} className="shrink-0 rounded-md border border-gray-200 object-cover" />;
}
