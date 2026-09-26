/**
 * The shared visual vocabulary. Every page composes these rather than
 * re-deriving padding/border/hover states inline, so a change to (say)
 * card elevation lands everywhere at once.
 */
import type { ComponentPropsWithRef, ReactNode } from "react";

// React 19 treats `ref` as an ordinary prop on function components, so
// ComponentPropsWithRef is all that's needed here — no forwardRef wrapper.
type ButtonProps = ComponentPropsWithRef<"button">;
type InputProps = ComponentPropsWithRef<"input">;
type SelectProps = ComponentPropsWithRef<"select">;

export function cn(...parts: (string | false | null | undefined)[]) {
  return parts.filter(Boolean).join(" ");
}

/* ------------------------------- Button ------------------------------- */

type ButtonVariant = "primary" | "secondary" | "ghost" | "danger";
type ButtonSize = "sm" | "md" | "lg";

const BUTTON_BASE =
  "inline-flex items-center justify-center gap-2 rounded-lg font-medium whitespace-nowrap " +
  "transition-[background-color,border-color,color,box-shadow,transform] duration-150 " +
  "active:scale-[0.98] disabled:pointer-events-none disabled:opacity-50";

const BUTTON_VARIANTS: Record<ButtonVariant, string> = {
  primary: "bg-brand text-brand-foreground shadow-card hover:bg-brand-hover",
  secondary: "border border-border bg-surface text-foreground shadow-card hover:bg-surface-hover hover:border-border-strong",
  ghost: "text-muted hover:bg-surface-muted hover:text-foreground",
  danger: "border border-transparent bg-danger-soft text-danger-soft-foreground hover:bg-danger hover:text-white",
};

const BUTTON_SIZES: Record<ButtonSize, string> = {
  sm: "h-8 px-3 text-xs",
  md: "h-10 px-4 text-sm",
  lg: "h-12 px-6 text-sm",
};

export function Button({
  variant = "primary",
  size = "md",
  className,
  children,
  ...props
}: ButtonProps & { variant?: ButtonVariant; size?: ButtonSize }) {
  return (
    <button className={cn(BUTTON_BASE, BUTTON_VARIANTS[variant], BUTTON_SIZES[size], className)} {...props}>
      {children}
    </button>
  );
}

/* -------------------------------- Input ------------------------------- */

const CONTROL_BASE =
  "w-full rounded-lg border border-border bg-surface px-3 text-sm text-foreground " +
  "placeholder:text-subtle transition-[border-color,box-shadow] duration-150 " +
  "hover:border-border-strong focus:border-brand focus:outline-none focus:ring-4 focus:ring-[var(--brand-ring)] " +
  "disabled:cursor-not-allowed disabled:opacity-60";

export function Input({ className, ...props }: InputProps) {
  return <input className={cn(CONTROL_BASE, "h-10", className)} {...props} />;
}

export function Select({ className, children, ...props }: SelectProps) {
  return (
    <select className={cn(CONTROL_BASE, "h-10 cursor-pointer pr-8", className)} {...props}>
      {children}
    </select>
  );
}

export function Label({ children, hint }: { children: ReactNode; hint?: string }) {
  return (
    <span className="mb-1.5 flex items-baseline gap-2">
      <span className="text-xs font-medium text-muted">{children}</span>
      {hint && <span className="text-[11px] text-subtle">{hint}</span>}
    </span>
  );
}

/** Labelled text input — the form workhorse. */
export function Field({
  label,
  value,
  onChange,
  type = "text",
  placeholder,
  required = true,
  hint,
  ...rest
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  hint?: string;
} & Omit<InputProps, "value" | "onChange">) {
  return (
    <label className="block">
      <Label hint={hint}>{label}</Label>
      <Input
        required={required}
        type={type}
        value={value}
        placeholder={placeholder}
        onChange={(e) => onChange(e.target.value)}
        {...rest}
      />
    </label>
  );
}

/* -------------------------------- Card -------------------------------- */

export function Card({ className, children }: { className?: string; children: ReactNode }) {
  return (
    <div className={cn("rounded-xl border border-border bg-surface shadow-card", className)}>{children}</div>
  );
}

export function CardHeader({ title, description, action }: { title: string; description?: string; action?: ReactNode }) {
  return (
    <div className="flex items-start justify-between gap-4 border-b border-border px-5 py-4">
      <div className="min-w-0">
        <h2 className="text-sm font-semibold text-foreground">{title}</h2>
        {description && <p className="mt-0.5 text-xs text-muted">{description}</p>}
      </div>
      {action}
    </div>
  );
}

/* ------------------------------- Badge -------------------------------- */

type Tone = "neutral" | "success" | "warning" | "danger" | "brand";

const TONES: Record<Tone, string> = {
  neutral: "bg-surface-muted text-muted ring-border",
  success: "bg-success-soft text-success-soft-foreground ring-transparent",
  warning: "bg-warning-soft text-warning-soft-foreground ring-transparent",
  danger: "bg-danger-soft text-danger-soft-foreground ring-transparent",
  brand: "bg-brand-soft text-brand-soft-foreground ring-transparent",
};

export function Badge({ tone = "neutral", children }: { tone?: Tone; children: ReactNode }) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-medium ring-1 ring-inset",
        TONES[tone],
      )}
    >
      {children}
    </span>
  );
}

/* ------------------------------ Feedback ------------------------------ */

export function Skeleton({ className }: { className?: string }) {
  return <div className={cn("animate-[--animate-shimmer] rounded-md bg-surface-muted", className)} />;
}

/** Table-shaped loading state — avoids the layout jump a bare "Loading…" causes. */
export function TableSkeleton({ rows = 5, cols = 4 }: { rows?: number; cols?: number }) {
  return (
    <div className="divide-y divide-border">
      {Array.from({ length: rows }).map((_, r) => (
        <div key={r} className="flex items-center gap-4 px-5 py-3.5">
          {Array.from({ length: cols }).map((_, c) => (
            <Skeleton key={c} className={cn("h-4", c === 0 ? "w-1/3" : "flex-1")} />
          ))}
        </div>
      ))}
    </div>
  );
}

export function EmptyState({
  icon,
  title,
  description,
  action,
}: {
  icon?: ReactNode;
  title: string;
  description?: string;
  action?: ReactNode;
}) {
  return (
    <div className="flex flex-col items-center justify-center px-6 py-14 text-center">
      {icon && (
        <div className="mb-3 flex size-11 items-center justify-center rounded-xl bg-surface-muted text-subtle [&>svg]:size-5">
          {icon}
        </div>
      )}
      <p className="text-sm font-medium text-foreground">{title}</p>
      {description && <p className="mt-1 max-w-sm text-xs text-muted">{description}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}

export function Alert({ tone = "danger", children }: { tone?: "danger" | "warning" | "success"; children: ReactNode }) {
  const tones = {
    danger: "bg-danger-soft text-danger-soft-foreground",
    warning: "bg-warning-soft text-warning-soft-foreground",
    success: "bg-success-soft text-success-soft-foreground",
  };
  return (
    <div role="alert" className={cn("flex items-start gap-2 rounded-lg px-3.5 py-2.5 text-xs font-medium", tones[tone])}>
      {children}
    </div>
  );
}

/* ------------------------------- Layout ------------------------------- */

export function PageHeader({ title, description, action }: { title: string; description?: string; action?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 className="text-xl font-semibold tracking-tight text-foreground sm:text-2xl">{title}</h1>
        {description && <p className="mt-1 text-sm text-muted">{description}</p>}
      </div>
      {action}
    </div>
  );
}

/** Consistent page shell: centered, responsive gutters, entrance animation. */
export function PageShell({ children, width = "wide" }: { children: ReactNode; width?: "narrow" | "wide" | "full" }) {
  const widths = { narrow: "max-w-2xl", wide: "max-w-6xl", full: "max-w-screen-2xl" };
  return (
    <main className={cn("mx-auto w-full flex-1 animate-[--animate-fade-up] px-4 py-6 sm:px-6 sm:py-8", widths[width])}>
      {children}
    </main>
  );
}

export function Pagination({
  page,
  totalPages,
  totalCount,
  onPrev,
  onNext,
}: {
  page: number;
  totalPages: number;
  totalCount: number;
  onPrev: () => void;
  onNext: () => void;
}) {
  return (
    <div className="flex items-center justify-between gap-4 border-t border-border px-5 py-3">
      <p className="text-xs text-muted tabular">
        Page {page} of {totalPages} · {totalCount} total
      </p>
      <div className="flex gap-2">
        <Button variant="secondary" size="sm" disabled={page <= 1} onClick={onPrev}>
          Previous
        </Button>
        <Button variant="secondary" size="sm" disabled={page >= totalPages} onClick={onNext}>
          Next
        </Button>
      </div>
    </div>
  );
}
