"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { NavBar } from "@/components/nav-bar";
import { AlertIcon, BellIcon, CheckIcon } from "@/components/ui/icons";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  PageHeader,
  PageShell,
  Pagination,
  cn,
} from "@/components/ui/primitives";
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

  const unreadCount = result?.items.filter((n) => !n.isRead).length ?? 0;

  return (
    <>
      <NavBar />
      <PageShell>
        <PageHeader
          title="Notifications"
          description="Low-stock alerts raised by the background sweep."
          action={
            /* Segmented filter instead of a bare checkbox — larger target
               and the active state is legible at a glance. */
            <div className="flex rounded-lg border border-border p-0.5">
              {[
                { label: "Unread", value: true },
                { label: "All", value: false },
              ].map((option) => (
                <button
                  key={option.label}
                  onClick={() => {
                    setUnreadOnly(option.value);
                    setPage(1);
                  }}
                  aria-pressed={unreadOnly === option.value}
                  className={cn(
                    "h-8 rounded-md px-3 text-xs font-medium transition-colors",
                    unreadOnly === option.value
                      ? "bg-surface-muted text-foreground"
                      : "text-muted hover:text-foreground",
                  )}
                >
                  {option.label}
                  {option.value && unreadCount > 0 && (
                    <span className="ml-1.5 tabular">{unreadCount}</span>
                  )}
                </button>
              ))}
            </div>
          }
        />

        {error && <div className="mb-4"><Alert>{error}</Alert></div>}

        <Card className="overflow-hidden">
          {!result ? (
            <div className="divide-y divide-border">
              {[0, 1, 2].map((i) => (
                <div key={i} className="flex items-center gap-4 px-5 py-4">
                  <div className="size-8 animate-[--animate-shimmer] rounded-lg bg-surface-muted" />
                  <div className="h-3.5 flex-1 animate-[--animate-shimmer] rounded bg-surface-muted" />
                </div>
              ))}
            </div>
          ) : result.items.length === 0 ? (
            <EmptyState
              icon={<BellIcon />}
              title={unreadOnly ? "Nothing unread" : "No notifications yet"}
              description={
                unreadOnly
                  ? "You're all caught up."
                  : "Low-stock alerts will appear here as the background sweep finds them."
              }
            />
          ) : (
            <>
              <ul className="divide-y divide-border">
                {result.items.map((n) => (
                  <li
                    key={n.id}
                    className={cn(
                      "flex items-start gap-3 px-5 py-3.5 transition-colors hover:bg-surface-hover",
                      !n.isRead && "bg-warning-soft/30",
                    )}
                  >
                    <span
                      className={cn(
                        "mt-0.5 flex size-8 shrink-0 items-center justify-center rounded-lg",
                        n.isRead ? "bg-surface-muted text-subtle" : "bg-warning-soft text-warning-soft-foreground",
                      )}
                    >
                      <AlertIcon className="size-4" />
                    </span>

                    <div className="min-w-0 flex-1">
                      <p className={cn("text-sm", n.isRead ? "text-muted" : "font-medium text-foreground")}>
                        {describe(n)}
                      </p>
                      <p className="mt-1 flex items-center gap-2 text-xs text-subtle">
                        <time dateTime={n.createdAtUtc}>
                          {new Date(n.createdAtUtc).toLocaleString(undefined, {
                            dateStyle: "medium",
                            timeStyle: "short",
                          })}
                        </time>
                        {!n.isRead && <Badge tone="warning">New</Badge>}
                      </p>
                    </div>

                    {!n.isRead && (
                      <Button variant="secondary" size="sm" onClick={() => markRead(n.id)} className="shrink-0">
                        <CheckIcon className="size-3.5" />
                        Mark read
                      </Button>
                    )}
                  </li>
                ))}
              </ul>
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
    </>
  );
}

