"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { NavBar } from "@/components/nav-bar";
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
      <main className="mx-auto w-full max-w-3xl flex-1 p-6">
        <h1 className="mb-1 text-xl font-semibold">Documents</h1>
        <p className="mb-6 text-sm text-gray-500">
          Plain text or Markdown files the AI assistant can search — policies, supplier agreements, anything
          worth grounding an answer in. Only .txt/.md, up to 2MB.
        </p>

        <div className="mb-6 flex items-center gap-3">
          <input
            ref={fileInputRef}
            type="file"
            accept=".txt,.md,text/plain,text/markdown"
            disabled={isUploading}
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) void handleUpload(file);
            }}
            className="text-sm"
          />
          {isUploading && <span className="text-sm text-gray-500">Uploading…</span>}
        </div>

        {error && <p className="mb-3 text-sm text-red-600">{error}</p>}

        {!result ? (
          <p className="text-sm text-gray-500">Loading…</p>
        ) : result.items.length === 0 ? (
          <p className="text-sm text-gray-500">No documents uploaded yet.</p>
        ) : (
          <ul className="divide-y divide-gray-100 rounded-lg border border-gray-200 bg-white">
            {result.items.map((doc) => (
              <li key={doc.id} className="flex items-center justify-between gap-4 px-4 py-3">
                <div>
                  <p className="text-sm font-medium text-gray-900">{doc.fileName}</p>
                  <p className="mt-1 text-xs text-gray-400">
                    {doc.contentType} · uploaded {new Date(doc.uploadedAtUtc).toLocaleString()}
                  </p>
                </div>
                <button
                  onClick={() => handleDelete(doc.id)}
                  disabled={deletingId === doc.id}
                  className="shrink-0 rounded-md border border-gray-300 px-3 py-1 text-sm text-red-600 disabled:opacity-50"
                >
                  {deletingId === doc.id ? "Deleting…" : "Delete"}
                </button>
              </li>
            ))}
          </ul>
        )}
      </main>
    </>
  );
}
