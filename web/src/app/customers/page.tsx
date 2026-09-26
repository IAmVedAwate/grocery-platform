"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { ApiError } from "@/lib/api-client";
import { NavBar } from "@/components/nav-bar";
import { Drawer } from "@/components/ui/drawer";
import { PlusIcon, ReceiptIcon, SearchIcon, UsersIcon } from "@/components/ui/icons";
import {
  Alert,
  Button,
  Card,
  EmptyState,
  Field,
  Input,
  PageHeader,
  PageShell,
  TableSkeleton,
} from "@/components/ui/primitives";
import type { CustomerDto, PagedResult } from "@/lib/types";

export default function CustomersPage() {
  const { accessToken, isLoading, authFetch } = useAuth();
  const router = useRouter();

  const [result, setResult] = useState<PagedResult<CustomerDto> | null>(null);
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [listError, setListError] = useState<string | null>(null);

  const [formOpen, setFormOpen] = useState(false);
  const [name, setName] = useState("");
  const [phone, setPhone] = useState("");
  const [email, setEmail] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(search), 250);
    return () => clearTimeout(timer);
  }, [search]);

  const load = useCallback(async () => {
    setListError(null);
    try {
      const query = new URLSearchParams({ pageSize: "50" });
      if (debouncedSearch.trim()) query.set("search", debouncedSearch.trim());
      const data = await authFetch<PagedResult<CustomerDto>>(`/api/v1/customers?${query}`);
      setResult(data);
    } catch (err) {
      setListError(err instanceof ApiError ? err.message : "Could not load customers.");
    }
  }, [authFetch, debouncedSearch]);

  useEffect(() => {
    if (!isLoading && !accessToken) router.push("/login");
  }, [isLoading, accessToken, router]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (accessToken) void load();
  }, [accessToken, load]);

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    setFormError(null);
    setIsSaving(true);
    try {
      await authFetch("/api/v1/customers", {
        method: "POST",
        body: JSON.stringify({ name, phone: phone || null, email: email || null }),
      });
      setName("");
      setPhone("");
      setEmail("");
      setFormOpen(false);
      await load();
    } catch (err) {
      setFormError(err instanceof ApiError ? err.message : "Could not save customer.");
    } finally {
      setIsSaving(false);
    }
  }

  if (isLoading || !accessToken) return null;

  return (
    <>
      <NavBar />
      <PageShell>
        <PageHeader
          title="Customers"
          description="Everyone you've sold to, and what they bought."
          action={
            <Button onClick={() => setFormOpen(true)}>
              <PlusIcon className="size-4" />
              New customer
            </Button>
          }
        />

        <div className="relative mb-4 max-w-sm">
          <SearchIcon className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-subtle" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search by name or phone…"
            aria-label="Search customers"
            className="pl-9"
          />
        </div>

        {listError && <div className="mb-4"><Alert>{listError}</Alert></div>}

        <Card className="overflow-hidden">
          {!result ? (
            <TableSkeleton rows={5} cols={4} />
          ) : result.items.length === 0 ? (
            <EmptyState
              icon={<UsersIcon />}
              title={debouncedSearch ? "No matching customers" : "No customers yet"}
              description={
                debouncedSearch
                  ? `Nothing matches “${debouncedSearch}”.`
                  : "Add a customer to start tracking their purchase history."
              }
              action={
                !debouncedSearch && (
                  <Button onClick={() => setFormOpen(true)}>
                    <PlusIcon className="size-4" />
                    New customer
                  </Button>
                )
              }
            />
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm">
                <thead>
                  <tr className="border-b border-border text-xs text-muted">
                    <th scope="col" className="px-5 py-2.5 font-medium">Name</th>
                    <th scope="col" className="px-5 py-2.5 font-medium">Phone</th>
                    <th scope="col" className="px-5 py-2.5 font-medium">Email</th>
                    <th scope="col" className="px-5 py-2.5 text-right font-medium">
                      <span className="sr-only">Actions</span>
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {result.items.map((c) => (
                    <tr key={c.id} className="transition-colors hover:bg-surface-hover">
                      <td className="px-5 py-3">
                        <div className="flex items-center gap-2.5">
                          {/* Initials avatar — gives the row an anchor point
                              without needing an uploaded photo. */}
                          <span className="flex size-7 shrink-0 items-center justify-center rounded-full bg-brand-soft text-[11px] font-medium text-brand-soft-foreground">
                            {c.name.slice(0, 2).toUpperCase()}
                          </span>
                          <span className="font-medium">{c.name}</span>
                        </div>
                      </td>
                      <td className="px-5 py-3 text-muted tabular">{c.phone ?? "—"}</td>
                      <td className="px-5 py-3 text-muted">{c.email ?? "—"}</td>
                      <td className="px-5 py-3 text-right">
                        <Link href={`/sales?customerId=${c.id}`}>
                          <Button variant="secondary" size="sm">
                            <ReceiptIcon className="size-3.5" />
                            History
                          </Button>
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      </PageShell>

      <Drawer
        open={formOpen}
        onClose={() => setFormOpen(false)}
        title="New customer"
        description="Only a name is required."
        footer={
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setFormOpen(false)}>
              Cancel
            </Button>
            <Button form="new-customer" type="submit" disabled={isSaving}>
              {isSaving ? "Saving…" : "Add customer"}
            </Button>
          </div>
        }
      >
        <form id="new-customer" onSubmit={handleCreate} className="space-y-4 p-5">
          <Field label="Name" value={name} onChange={setName} placeholder="Priya Sharma" />
          <Field label="Phone" hint="optional" value={phone} onChange={setPhone} required={false} />
          <Field label="Email" hint="optional" type="email" value={email} onChange={setEmail} required={false} />
          {formError && <Alert>{formError}</Alert>}
        </form>
      </Drawer>
    </>
  );
}
