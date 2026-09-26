"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { NavBar } from "@/components/nav-bar";
import { FileIcon, PlusIcon, TrashIcon } from "@/components/ui/icons";
import {
  Alert,
  Button,
  Card,
  EmptyState,
  PageHeader,
  PageShell,
  TableSkeleton,
} from "@/components/ui/primitives";
import type { DocumentDto, PagedResult } from "@/lib/types";

const PAGE_SIZE = 20;

export default function DocumentsSettingsPage() {
  const { accessToken, isLoading, authFetch } = useAuth();
  const router = useRouter();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [result, setResult] = useState<PagedResult<DocumentDto> | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const data = await authFetch<PagedResult<DocumentDto>>(`/api/v1/documents?page=1&pageSize=${PAGE_SIZE}`);
      setResult(data);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not load documents.");
    }
  }, [authFetch]);

  useEffect(() => {
    if (!isLoading && !accessToken) router.push("/login");
  }, [isLoading, accessToken, router]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (accessToken) void load();
  }, [accessToken, load]);

  if (isLoading || !accessToken) return null;

  async function handleUpload(file: File) {
    setError(null);
    setIsUploading(true);
    try {
      const body = new FormData();
      body.append("file", file);
      await authFetch("/api/v1/documents", { method: "POST", body });
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not upload the document.");
    } finally {
      setIsUploading(false);
      if (fileInputRef.current) fileInputRef.current.value = "";
    }
  }

  async function handleDelete(id: string) {
    setError(null);
    setDeletingId(id);
    try {
      await authFetch(`/api/v1/documents/${id}`, { method: "DELETE" });
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not delete the document.");
    } finally {
      setDeletingId(null);
    }
  }

  return (
    <>
      <NavBar />
      <PageShell width="narrow">
        <PageHeader
          title="Documents"
          description="Plain text or Markdown the assistant can search — policies, supplier agreements, anything worth grounding an answer in. .txt/.md, up to 2MB."
          action={
            <>
              {/* The native file input is visually replaced by a real button;
                  the label keeps it keyboard- and screen-reader-accessible. */}
              <input
                ref={fileInputRef}
                id="document-upload"
                type="file"
                accept=".txt,.md,text/plain,text/markdown"
                disabled={isUploading}
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  if (file) void handleUpload(file);
                }}
                className="sr-only"
              />
              <Button onClick={() => fileInputRef.current?.click()} disabled={isUploading}>
                <PlusIcon className="size-4" />
                {isUploading ? "Uploading…" : "Upload document"}
              </Button>
            </>
          }
        />

        {error && <div className="mb-4"><Alert>{error}</Alert></div>}

        <Card className="overflow-hidden">
          {!result ? (
            <TableSkeleton rows={3} cols={2} />
          ) : result.items.length === 0 ? (
            <EmptyState
              icon={<FileIcon />}
              title="No documents yet"
              description="Upload a policy or agreement and the assistant can answer questions from it, with citations."
              action={
                <Button onClick={() => fileInputRef.current?.click()} disabled={isUploading}>
                  <PlusIcon className="size-4" />
                  Upload document
                </Button>
              }
            />
          ) : (
            <ul className="divide-y divide-border">
              {result.items.map((doc) => (
                <li
                  key={doc.id}
                  className="flex items-center gap-3 px-5 py-3.5 transition-colors hover:bg-surface-hover"
                >
                  <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-surface-muted text-subtle">
                    <FileIcon className="size-4" />
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium">{doc.fileName}</p>
                    <p className="mt-0.5 text-xs text-subtle">
                      Uploaded{" "}
                      {new Date(doc.uploadedAtUtc).toLocaleString(undefined, {
                        dateStyle: "medium",
                        timeStyle: "short",
                      })}
                    </p>
                  </div>
                  <Button
                    variant="danger"
                    size="sm"
                    onClick={() => handleDelete(doc.id)}
                    disabled={deletingId === doc.id}
                    className="shrink-0"
                  >
                    <TrashIcon className="size-3.5" />
                    {deletingId === doc.id ? "Deleting…" : "Delete"}
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </PageShell>
    </>
  );
}
