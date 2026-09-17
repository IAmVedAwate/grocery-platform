"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "@/lib/auth-context";

const LINKS = [
  { href: "/products", label: "Products" },
  { href: "/inventory", label: "Inventory" },
  { href: "/purchasing", label: "Purchasing" },
  { href: "/checkout", label: "Checkout" },
  { href: "/sales", label: "Sales" },
  { href: "/customers", label: "Customers" },
];

/**
 * Shared nav for every authenticated page. Deliberately shows every link
 * regardless of the signed-in user's actual permissions — the API
 * enforces authorization either way, and decoding the JWT client-side
 * just to hide menu items is a real but low-value refinement left for
 * later (the backend is what's authoritative; see docs/PRD.md §13).
 */
export function NavBar() {
  const { logout } = useAuth();
  const pathname = usePathname();

  return (
    <header className="border-b border-gray-200 bg-white">
      <div className="mx-auto flex max-w-5xl items-center justify-between px-6 py-3">
        <nav className="flex flex-wrap gap-4 text-sm">
          {LINKS.map((link) => {
            const isActive = pathname === link.href || pathname.startsWith(`${link.href}/`);
            return (
              <Link
                key={link.href}
                href={link.href}
                className={isActive ? "font-semibold text-gray-900" : "text-gray-500 hover:text-gray-900"}
              >
                {link.label}
              </Link>
            );
          })}
        </nav>
        <button onClick={logout} className="text-sm text-gray-500 underline">
          Sign out
        </button>
      </div>
    </header>
  );
}
