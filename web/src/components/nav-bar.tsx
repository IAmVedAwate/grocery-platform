"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useCallback, useEffect, useRef, useState, useSyncExternalStore } from "react";
import { useAuth } from "@/lib/auth-context";
import { SearchDrawer } from "@/components/search-drawer";
import { Drawer } from "@/components/ui/drawer";
import {
  BellIcon,
  BoxIcon,
  CartIcon,
  ChartIcon,
  FileIcon,
  LayersIcon,
  LogoutIcon,
  MenuIcon,
  MoonIcon,
  ReceiptIcon,
  SearchIcon,
  SettingsIcon,
  SparkIcon,
  SunIcon,
  TruckIcon,
  UsersIcon,
} from "@/components/ui/icons";
import { Button, cn } from "@/components/ui/primitives";

const PRIMARY = [
  { href: "/checkout", label: "Checkout", icon: CartIcon },
  { href: "/products", label: "Products", icon: BoxIcon },
  { href: "/inventory", label: "Inventory", icon: LayersIcon },
  { href: "/purchasing", label: "Purchasing", icon: TruckIcon },
  { href: "/sales", label: "Sales", icon: ReceiptIcon },
  { href: "/customers", label: "Customers", icon: UsersIcon },
  { href: "/reports", label: "Reports", icon: ChartIcon },
];

const SECONDARY = [
  { href: "/assistant", label: "Assistant", icon: SparkIcon },
  { href: "/settings/documents", label: "Documents", icon: FileIcon },
  { href: "/settings/staff", label: "Staff", icon: SettingsIcon },
];

function useIsActive() {
  const pathname = usePathname();
  return useCallback(
    (href: string) => pathname === href || pathname.startsWith(`${href}/`),
    [pathname],
  );
}

/**
 * Shared chrome for every authenticated page. Every link is shown
 * regardless of the signed-in user's permissions — the API is what
 * enforces authorization (docs/PRD.md §13), and hiding menu items
 * client-side would only be cosmetic.
 */
export function NavBar() {
  const { logout } = useAuth();
  const isActive = useIsActive();

  const [searchOpen, setSearchOpen] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const [accountOpen, setAccountOpen] = useState(false);
  const [scrolled, setScrolled] = useState(false);
  const accountRef = useRef<HTMLDivElement>(null);

  // ⌘K / Ctrl-K anywhere, the way every tool people already use behaves.
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setSearchOpen((v) => !v);
      }
    }
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, []);

  // Border only appears once content scrolls under the bar.
  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 4);
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  useEffect(() => {
    if (!accountOpen) return;
    function onPointerDown(e: PointerEvent) {
      if (!accountRef.current?.contains(e.target as Node)) setAccountOpen(false);
    }
    function onKey(e: KeyboardEvent) {
      if (e.key === "Escape") setAccountOpen(false);
    }
    document.addEventListener("pointerdown", onPointerDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [accountOpen]);

  return (
    <>
      <header
        className={cn(
          "sticky top-0 z-40 bg-[var(--surface)]/80 backdrop-blur-xl transition-shadow duration-200",
          scrolled ? "border-b border-border shadow-card" : "border-b border-transparent",
        )}
      >
        <div className="mx-auto flex h-14 w-full max-w-screen-2xl items-center gap-2 px-4 sm:px-6">
          <Link href="/" className="flex shrink-0 items-center gap-2" aria-label="QuickStock home">
            <span className="flex size-7 items-center justify-center rounded-lg bg-brand text-brand-foreground shadow-card">
              <BoxIcon className="size-4" />
            </span>
            <span className="hidden text-sm font-semibold tracking-tight sm:block">QuickStock</span>
          </Link>

          <nav aria-label="Main" className="mx-1 hidden min-w-0 flex-1 items-center gap-0.5 lg:flex">
            {PRIMARY.map((link) => (
              <NavLink key={link.href} {...link} active={isActive(link.href)} />
            ))}
          </nav>

          <div className="flex flex-1 items-center justify-end gap-1 lg:flex-none">
            <button
              onClick={() => setSearchOpen(true)}
              className={cn(
                "group flex h-9 items-center gap-2 rounded-lg border border-border bg-surface px-2.5 text-sm",
                "text-subtle transition-colors hover:border-border-strong hover:text-muted sm:w-56 sm:justify-between",
              )}
              aria-label="Search"
            >
              <span className="flex items-center gap-2">
                <SearchIcon className="size-4" />
                <span className="hidden sm:inline">Search…</span>
              </span>
              <kbd className="hidden rounded border border-border bg-surface-muted px-1.5 py-0.5 font-sans text-[10px] sm:inline">
                ⌘K
              </kbd>
            </button>

            <IconLink href="/notifications" label="Notifications" active={isActive("/notifications")}>
              <BellIcon className="size-[18px]" />
            </IconLink>

            <ThemeToggle />

            <div ref={accountRef} className="relative hidden lg:block">
              <button
                onClick={() => setAccountOpen((v) => !v)}
                aria-haspopup="menu"
                aria-expanded={accountOpen}
                className="flex size-9 items-center justify-center rounded-lg text-muted transition-colors hover:bg-surface-muted hover:text-foreground"
                aria-label="Account menu"
              >
                <SettingsIcon className="size-[18px]" />
              </button>
              {accountOpen && (
                <div
                  role="menu"
                  className="absolute right-0 mt-1.5 w-52 animate-[--animate-slide-up] overflow-hidden rounded-xl border border-border bg-surface p-1 shadow-overlay"
                >
                  {SECONDARY.map(({ href, label, icon: IconComponent }) => (
                    <Link
                      key={href}
                      href={href}
                      role="menuitem"
                      onClick={() => setAccountOpen(false)}
                      className="flex items-center gap-2.5 rounded-lg px-2.5 py-2 text-sm text-muted transition-colors hover:bg-surface-muted hover:text-foreground"
                    >
                      <IconComponent className="size-4" />
                      {label}
                    </Link>
                  ))}
                  <div className="my-1 h-px bg-border" />
                  <button
                    role="menuitem"
                    onClick={logout}
                    className="flex w-full items-center gap-2.5 rounded-lg px-2.5 py-2 text-sm text-muted transition-colors hover:bg-danger-soft hover:text-danger-soft-foreground"
                  >
                    <LogoutIcon className="size-4" />
                    Sign out
                  </button>
                </div>
              )}
            </div>

            <Button
              variant="ghost"
              size="sm"
              className="px-2 lg:hidden"
              onClick={() => setMenuOpen(true)}
              aria-label="Open menu"
            >
              <MenuIcon className="size-5" />
            </Button>
          </div>
        </div>
      </header>

      <SearchDrawer key={searchOpen ? "open" : "closed"} open={searchOpen} onClose={() => setSearchOpen(false)} />

      <Drawer open={menuOpen} onClose={() => setMenuOpen(false)} title="Menu">
        <nav aria-label="Mobile" className="p-2">
          {[...PRIMARY, ...SECONDARY].map(({ href, label, icon: IconComponent }) => (
            <Link
              key={href}
              href={href}
              onClick={() => setMenuOpen(false)}
              className={cn(
                "flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm transition-colors",
                isActive(href)
                  ? "bg-brand-soft font-medium text-brand-soft-foreground"
                  : "text-muted hover:bg-surface-muted hover:text-foreground",
              )}
            >
              <IconComponent className="size-[18px]" />
              {label}
            </Link>
          ))}
          <div className="my-2 h-px bg-border" />
          <button
            onClick={logout}
            className="flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-sm text-muted transition-colors hover:bg-danger-soft hover:text-danger-soft-foreground"
          >
            <LogoutIcon className="size-[18px]" />
            Sign out
          </button>
        </nav>
      </Drawer>
    </>
  );
}

function NavLink({
  href,
  label,
  icon: IconComponent,
  active,
}: {
  href: string;
  label: string;
  icon: (p: React.SVGProps<SVGSVGElement>) => React.ReactElement;
  active: boolean;
}) {
  return (
    <Link
      href={href}
      aria-current={active ? "page" : undefined}
      className={cn(
        "relative flex h-9 items-center gap-1.5 rounded-lg px-2.5 text-sm transition-colors",
        active ? "text-foreground" : "text-muted hover:bg-surface-muted hover:text-foreground",
      )}
    >
      {active && <span className="absolute inset-0 -z-10 rounded-lg bg-surface-muted" />}
      <IconComponent className="size-4 shrink-0" />
      <span className="hidden xl:inline">{label}</span>
      {active && (
        <span className="absolute inset-x-2.5 -bottom-[9px] h-0.5 rounded-full bg-brand" aria-hidden="true" />
      )}
    </Link>
  );
}

function IconLink({
  href,
  label,
  active,
  children,
}: {
  href: string;
  label: string;
  active: boolean;
  children: React.ReactNode;
}) {
  return (
    <Link
      href={href}
      aria-label={label}
      className={cn(
        "flex size-9 items-center justify-center rounded-lg transition-colors",
        active ? "bg-surface-muted text-foreground" : "text-muted hover:bg-surface-muted hover:text-foreground",
      )}
    >
      {children}
    </Link>
  );
}

/** The theme lives on <html>, put there by the pre-paint script in layout.
 *  That's external mutable state, so it's subscribed to rather than mirrored
 *  into React state via an effect — no cascading render, and no chance of the
 *  icon drifting out of sync with the actual class. */
function subscribeToTheme(onChange: () => void) {
  const observer = new MutationObserver(onChange);
  observer.observe(document.documentElement, { attributes: true, attributeFilter: ["class"] });
  return () => observer.disconnect();
}

function ThemeToggle() {
  const isDark = useSyncExternalStore(
    subscribeToTheme,
    () => document.documentElement.classList.contains("dark"),
    () => false, // server render: assume light, the inline script corrects it
  );

  function toggle() {
    const next = !document.documentElement.classList.contains("dark");
    document.documentElement.classList.toggle("dark", next);
    try {
      localStorage.setItem("quickstock-theme", next ? "dark" : "light");
    } catch {
      // Private mode / storage disabled — the toggle still works for this
      // session, it just won't be remembered.
    }
  }

  return (
    <button
      onClick={toggle}
      aria-label={isDark ? "Switch to light theme" : "Switch to dark theme"}
      className="flex size-9 items-center justify-center rounded-lg text-muted transition-colors hover:bg-surface-muted hover:text-foreground"
    >
      {isDark ? <SunIcon className="size-[18px]" /> : <MoonIcon className="size-[18px]" />}
    </button>
  );
}
