"use client";

import { useEffect, useId, useRef, type ReactNode } from "react";
import { CloseIcon } from "./icons";
import { Button, cn } from "./primitives";

/**
 * Slide-over panel: a right-hand sheet on desktop, a bottom sheet on
 * mobile (where a full-height side panel is awkward to reach one-handed).
 *
 * Handles the things a hand-rolled overlay usually forgets: Escape to
 * close, scroll lock that doesn't shift layout, focus moved in on open and
 * restored on close, and focus kept inside while open.
 */
export function Drawer({
  open,
  onClose,
  title,
  description,
  footer,
  children,
  width = "md",
}: {
  open: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  footer?: ReactNode;
  children: ReactNode;
  width?: "md" | "lg";
}) {
  const panelRef = useRef<HTMLDivElement>(null);
  const restoreFocusRef = useRef<HTMLElement | null>(null);
  const titleId = useId();

  useEffect(() => {
    if (!open) return;

    restoreFocusRef.current = document.activeElement as HTMLElement | null;

    // Compensate for the vanishing scrollbar so the page behind doesn't
    // jump sideways as the drawer opens.
    const scrollbar = window.innerWidth - document.documentElement.clientWidth;
    const { overflow, paddingRight } = document.body.style;
    document.body.style.overflow = "hidden";
    if (scrollbar > 0) document.body.style.paddingRight = `${scrollbar}px`;

    // Focus the first control inside rather than the panel itself, so a
    // search drawer is immediately typeable.
    const focusables = () =>
      panelRef.current?.querySelectorAll<HTMLElement>(
        'a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])',
      ) ?? ([] as unknown as NodeListOf<HTMLElement>);

    const raf = requestAnimationFrame(() => (focusables()[0] ?? panelRef.current)?.focus());

    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") {
        e.preventDefault();
        onClose();
        return;
      }
      if (e.key !== "Tab") return;
      const items = Array.from(focusables());
      if (items.length === 0) return;
      const first = items[0];
      const last = items[items.length - 1];
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    }

    document.addEventListener("keydown", onKeyDown);
    return () => {
      cancelAnimationFrame(raf);
      document.removeEventListener("keydown", onKeyDown);
      document.body.style.overflow = overflow;
      document.body.style.paddingRight = paddingRight;
      restoreFocusRef.current?.focus();
    };
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50">
      <button
        aria-label="Close panel"
        onClick={onClose}
        className="absolute inset-0 animate-[--animate-fade-in] bg-[var(--overlay)] backdrop-blur-[2px]"
      />

      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        className={cn(
          "absolute bg-surface shadow-overlay outline-none",
          // Mobile: bottom sheet. Desktop: right-hand panel.
          "inset-x-0 bottom-0 max-h-[85vh] animate-[--animate-slide-up] rounded-t-2xl",
          "sm:inset-y-0 sm:right-0 sm:left-auto sm:max-h-none sm:animate-[--animate-slide-in-right] sm:rounded-none sm:rounded-l-2xl",
          "flex flex-col",
          width === "lg" ? "sm:w-[34rem]" : "sm:w-[26rem]",
        )}
      >
        {/* Grab handle — reads as a sheet on touch devices. */}
        <div className="mx-auto mt-2 h-1 w-9 shrink-0 rounded-full bg-border-strong sm:hidden" />

        <header className="flex items-start justify-between gap-4 border-b border-border px-5 py-4">
          <div className="min-w-0">
            <h2 id={titleId} className="truncate text-sm font-semibold text-foreground">
              {title}
            </h2>
            {description && <p className="mt-0.5 text-xs text-muted">{description}</p>}
          </div>
          <Button variant="ghost" size="sm" onClick={onClose} aria-label="Close" className="-mr-1 shrink-0 px-2">
            <CloseIcon className="size-4" />
          </Button>
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain">{children}</div>

        {footer && <footer className="border-t border-border bg-surface-muted px-5 py-3">{footer}</footer>}
      </div>
    </div>
  );
}
