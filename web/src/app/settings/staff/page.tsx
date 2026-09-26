"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { Field } from "@/components/field";
import { NavBar } from "@/components/nav-bar";
import { Drawer } from "@/components/ui/drawer";
import { PlusIcon, UsersIcon } from "@/components/ui/icons";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  PageHeader,
  PageShell,
  Skeleton,
  cn,
} from "@/components/ui/primitives";
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
  const [formOpen, setFormOpen] = useState(false);

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
      setFormOpen(false);
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
      <PageShell>
        <PageHeader
          title="Staff"
          description="Access is a plain permission checklist per person - not a fixed role. Check exactly what someone should be able to do; nothing else is assumed."
          action={
            <Button onClick={() => setFormOpen(true)}>
              <PlusIcon className="size-4" />
              Add staff
            </Button>
          }
        />

        {listError && <div className="mb-4"><Alert>{listError}</Alert></div>}

        {!staff ? (
          <div className="space-y-3">
            {[0, 1].map((i) => (
              <Card key={i} className="p-5">
                <Skeleton className="h-4 w-48" />
                <Skeleton className="mt-3 h-3 w-full" />
              </Card>
            ))}
          </div>
        ) : staff.length === 0 ? (
          <Card>
            <EmptyState
              icon={<UsersIcon />}
              title="No staff accounts yet"
              description="Add an account and tick only the permissions that person needs."
              action={
                <Button onClick={() => setFormOpen(true)}>
                  <PlusIcon className="size-4" />
                  Add staff
                </Button>
              }
            />
          </Card>
        ) : (
          <div className="space-y-3">
            {staff.map((s) => (
              <StaffRow key={s.id} user={s} allPermissions={allPermissions} authFetch={authFetch} onChanged={load} />
            ))}
          </div>
        )}
      </PageShell>

      <Drawer
        open={formOpen}
        onClose={() => setFormOpen(false)}
        title="Add staff account"
        description="They can sign in immediately with these permissions."
        width="lg"
        footer={
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setFormOpen(false)}>
              Cancel
            </Button>
            <Button form="new-staff" type="submit" disabled={isSaving}>
              {isSaving ? "Creating..." : "Add account"}
            </Button>
          </div>
        }
      >
        <form id="new-staff" onSubmit={handleCreate} className="space-y-4 p-5">
          <Field label="Name" value={displayName} onChange={setDisplayName} />
          <Field label="Email" type="email" value={email} onChange={setEmail} />
          <Field
            label="Password"
            type="password"
            value={password}
            onChange={setPassword}
            placeholder="At least 8 characters"
          />

          <div>
            <p className="mb-2 text-xs font-medium text-muted">Permissions</p>
            <PermissionGrid all={allPermissions} checked={newPermissions} onToggle={toggleNewPermission} />
          </div>

          {formError && <Alert>{formError}</Alert>}
        </form>
      </Drawer>
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
  if (!all) return <p className="text-sm text-muted">Loading permissions…</p>;
  return (
    <div className="flex flex-wrap gap-1.5">
      {all.map((key) => {
        const on = checked.has(key);
        return (
          <button
            key={key}
            type="button"
            onClick={() => onToggle(key)}
            aria-pressed={on}
            className={cn(
              "rounded-lg border px-2.5 py-1 font-mono text-[11px] transition-colors",
              on
                ? "border-brand bg-brand-soft text-brand-soft-foreground"
                : "border-border text-muted hover:border-border-strong hover:text-foreground",
            )}
          >
            {key}
          </button>
        );
      })}
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
    <Card className="p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex items-center gap-3">
          <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-surface-muted text-xs font-medium text-muted">
            {user.displayName.slice(0, 2).toUpperCase()}
          </span>
          <div className="min-w-0">
            <p className="flex items-center gap-2 text-sm font-medium">
              {user.displayName}
              <Badge tone={user.isActive ? "success" : "neutral"}>
                {user.isActive ? "Active" : "Deactivated"}
              </Badge>
            </p>
            <p className="truncate text-xs text-subtle">{user.email}</p>
          </div>
        </div>
        <Button variant="secondary" size="sm" onClick={toggleActive} disabled={saving}>
          {user.isActive ? "Deactivate" : "Activate"}
        </Button>
      </div>

      <div className="mt-4 border-t border-border pt-4">
        <p className="mb-2 text-xs font-medium text-muted">
          Permissions
          <span className="ml-1.5 font-normal text-subtle tabular">{checked.size} selected</span>
        </p>
        <PermissionGrid all={allPermissions} checked={checked} onToggle={toggle} />
      </div>

      {error && <div className="mt-3"><Alert>{error}</Alert></div>}

      {dirty && (
        <div className="mt-4 flex items-center gap-3">
          <Button onClick={savePermissions} disabled={saving} size="sm">
            {saving ? "Saving…" : "Save changes"}
          </Button>
          <span className="text-xs text-subtle">Takes effect on their next sign-in.</span>
        </div>
      )}
    </Card>
  );
}
