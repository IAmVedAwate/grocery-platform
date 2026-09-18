"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { NavBar } from "@/components/nav-bar";
import type { NotificationDto, PagedResult } from "@/lib/types";

const PAGE_SIZE = 20;

function describe(notification: NotificationDto): string {
  if (notification.type !== "LowStock") return notification.payload;
  try {
    const data = JSON.parse(notification.payload) as { sku: string; name: string; quantityOnHand: number; lowStockThreshold: number };
    return `${data.name} (${data.sku}) is low on stock: ${data.quantityOnHand} on hand, threshold ${data.lowStockThreshold}.`;
  } catch {
    return notification.payload;
  }
}

export default function NotificationsPage() {
  const { accessToken, isLoading, authFetch } = useAuth();
  const router = useRouter();

  const [result, setResult] = useState<PagedResult<NotificationDto> | null>(null);
  const [page, setPage] = useState(1);
  const [unreadOnly, setUnreadOnly] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const data = await authFetch<PagedResult<NotificationDto>>(
        `/api/v1/notifications?unreadOnly=${unreadOnly}&page=${page}&pageSize=${PAGE_SIZE}`,
      );
      setResult(data);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not load notifications.");
    }
  }, [authFetch, unreadOnly, page]);

  useEffect(() => {
    if (!isLoading && !accessToken) router.push("/login");
  }, [isLoading, accessToken, router]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (accessToken) void load();
  }, [accessToken, load]);

  if (isLoading || !accessToken) return null;

  const totalPages = result ? Math.max(1, Math.ceil(result.totalCount / PAGE_SIZE)) : 1;

  async function markRead(id: string) {
    setError(null);
    try {
      await authFetch(`/api/v1/notifications/${id}/read`, { method: "POST" });
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not mark the notification read.");
    }
  }

  return (
    <>
      <NavBar />
      <main className="mx-auto w-full max-w-4xl flex-1 p-6">
        <h1 className="mb-6 text-xl font-semibold">Notifications</h1>

        <label className="mb-4 flex items-center gap-2 text-sm text-gray-600">
          <input
            type="checkbox"
            checked={unreadOnly}
            onChange={(e) => {
              setUnreadOnly(e.target.checked);
              setPage(1);
            }}
          />
          Unread only
        </label>

        {error && <p className="mb-3 text-sm text-red-600">{error}</p>}

        {!result ? (
          <p className="text-sm text-gray-500">Loading…</p>
        ) : result.items.length === 0 ? (
          <p className="text-sm text-gray-500">{unreadOnly ? "No unread notifications." : "No notifications yet."}</p>
        ) : (
          <ul className="divide-y divide-gray-100 rounded-lg border border-gray-200 bg-white">
            {result.items.map((n) => (
              <li key={n.id} className="flex items-start justify-between gap-4 px-4 py-3">
                <div>
                  <p className={n.isRead ? "text-sm text-gray-500" : "text-sm font-medium text-gray-900"}>{describe(n)}</p>
                  <p className="mt-1 text-xs text-gray-400">{new Date(n.createdAtUtc).toLocaleString()}</p>
                </div>
                {!n.isRead && (
                  <button onClick={() => markRead(n.id)} className="shrink-0 rounded-md border border-gray-300 px-3 py-1 text-sm">
                    Mark read
                  </button>
                )}
              </li>
            ))}
          </ul>
        )}

        {result && result.totalCount > 0 && (
          <div className="mt-3 flex items-center justify-between text-sm text-gray-500">
            <span>
              Page {result.page} of {totalPages} ({result.totalCount} total)
            </span>
            <div className="flex gap-2">
              <button disabled={page <= 1} onClick={() => setPage((p) => p - 1)} className="rounded-md border border-gray-300 px-3 py-1 disabled:opacity-50">
                Previous
              </button>
              <button disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)} className="rounded-md border border-gray-300 px-3 py-1 disabled:opacity-50">
                Next
              </button>
            </div>
          </div>
        )}
      </main>
    </>
  );
}
