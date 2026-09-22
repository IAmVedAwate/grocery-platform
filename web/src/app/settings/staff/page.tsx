"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { Field } from "@/components/field";
import { NavBar } from "@/components/nav-bar";
import type { StaffUserDto } from "@/lib/types";

export default function StaffSettingsPage() {
  const { accessToken, isLoading, authFetch } = useAuth();
  const router = useRouter();

  const [allPermissions, setAllPermissions] = useState<string[] | null>(null);
  const [staff, setStaff] = useState<StaffUserDto[] | null>(null);
  const [listError, setListError] = useState<string | null>(null);

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [newPermissions, setNewPermissions] = useState<Set<string>>(new Set());
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const load = useCallback(async () => {
    setListError(null);
    try {
      const [permissions, users] = await Promise.all([
        authFetch<string[]>("/api/v1/permissions"),
        authFetch<StaffUserDto[]>("/api/v1/users"),
      ]);
      setAllPermissions(permissions);
      setStaff(users);
    } catch (err) {
      setListError(err instanceof ApiError ? err.message : "Could not load staff.");
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

  function toggleNewPermission(key: string) {
    setNewPermissions((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    setFormError(null);
    setIsSaving(true);
    try {
      await authFetch("/api/v1/users", {
        method: "POST",
        body: JSON.stringify({ email, password, displayName, permissions: Array.from(newPermissions) }),
      });
      setEmail("");
      setPassword("");
      setDisplayName("");
      setNewPermissions(new Set());
      await load();
    } catch (err) {
      setFormError(err instanceof ApiError ? err.message : "Could not create staff account.");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <>
      <NavBar />
      <main className="mx-auto w-full max-w-4xl flex-1 p-6">
        <h1 className="mb-1 text-xl font-semibold">Staff</h1>
        <p className="mb-6 text-sm text-gray-500">
          Access here is a plain permission checklist per person — not a fixed role. Check exactly what each
          person should be able to do; nothing else is assumed.
        </p>

        <form onSubmit={handleCreate} className="mb-8 rounded-lg border border-gray-200 bg-white p-4">
          <h2 className="mb-3 text-sm font-medium text-gray-700">Add a staff account</h2>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <Field label="Name" value={displayName} onChange={setDisplayName} />
            <Field label="Email" type="email" value={email} onChange={setEmail} />
            <Field label="Password" type="password" value={password} onChange={setPassword} placeholder="At least 8 characters" />
          </div>

          <p className="mt-4 mb-2 text-sm font-medium text-gray-700">Permissions</p>
          <PermissionGrid all={allPermissions} checked={newPermissions} onToggle={toggleNewPermission} />

          {formError && <p className="mt-2 text-sm text-red-600">{formError}</p>}
          <button
            type="submit"
            disabled={isSaving}
            className="mt-3 rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white hover:bg-gray-800 disabled:opacity-50"
          >
            {isSaving ? "Creating…" : "Add staff account"}
          </button>
        </form>

        {listError && <p className="mb-3 text-sm text-red-600">{listError}</p>}

        {!staff ? (
          <p className="text-sm text-gray-500">Loading…</p>
        ) : staff.length === 0 ? (
          <p className="text-sm text-gray-500">No staff accounts yet.</p>
        ) : (
          <div className="space-y-3">
            {staff.map((s) => (
              <StaffRow key={s.id} user={s} allPermissions={allPermissions} authFetch={authFetch} onChanged={load} />
            ))}
          </div>
        )}
      </main>
    </>
  );
}

function PermissionGrid({
  all,
  checked,
  onToggle,
}: {
  all: string[] | null;
  checked: Set<string>;
  onToggle: (key: string) => void;
}) {
  if (!all) return <p className="text-sm text-gray-500">Loading permissions…</p>;
  return (
    <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
      {all.map((key) => (
        <label key={key} className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" checked={checked.has(key)} onChange={() => onToggle(key)} />
          {key}
        </label>
      ))}
    </div>
  );
}

function StaffRow({
  user,
  allPermissions,
  authFetch,
  onChanged,
}: {
  user: StaffUserDto;
  allPermissions: string[] | null;
  authFetch: <T>(path: string, options?: RequestInit) => Promise<T>;
  onChanged: () => Promise<void>;
}) {
  const [checked, setChecked] = useState<Set<string>>(new Set(user.permissions));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const dirty =
    checked.size !== user.permissions.length || user.permissions.some((p) => !checked.has(p));

  function toggle(key: string) {
    setChecked((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  async function savePermissions() {
    setError(null);
    setSaving(true);
    try {
      await authFetch(`/api/v1/users/${user.id}/permissions`, {
        method: "PUT",
        body: JSON.stringify({ permissions: Array.from(checked) }),
      });
      await onChanged();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not save permissions.");
    } finally {
      setSaving(false);
    }
  }

  async function toggleActive() {
    setError(null);
    setSaving(true);
    try {
      await authFetch(`/api/v1/users/${user.id}/${user.isActive ? "deactivate" : "activate"}`, { method: "POST" });
      await onChanged();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Could not update account status.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4">
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm font-medium text-gray-900">
            {user.displayName} <span className="font-normal text-gray-500">({user.email})</span>
          </p>
          <p className="text-xs text-gray-400">{user.isActive ? "Active" : "Deactivated"}</p>
        </div>
        <button onClick={toggleActive} disabled={saving} className="rounded-md border border-gray-300 px-3 py-1 text-sm disabled:opacity-50">
          {user.isActive ? "Deactivate" : "Activate"}
        </button>
      </div>

      <div className="mt-3">
        <PermissionGrid all={allPermissions} checked={checked} onToggle={toggle} />
      </div>

      {error && <p className="mt-2 text-sm text-red-600">{error}</p>}
      {dirty && (
        <button
          onClick={savePermissions}
          disabled={saving}
          className="mt-3 rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white hover:bg-gray-800 disabled:opacity-50"
        >
          {saving ? "Saving…" : "Save permission changes"}
        </button>
      )}
    </div>
  );
}
