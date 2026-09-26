import { Badge } from "@/components/ui/primitives";

const TONE_BY_STATUS: Record<string, "success" | "warning" | "danger" | "neutral" | "brand"> = {
  Completed: "success",
  Received: "success",
  Approved: "brand",
  Active: "success",
  Submitted: "brand",
  PartiallyReceived: "warning",
  PartiallyRefunded: "warning",
  Draft: "neutral",
  Inactive: "neutral",
  Cancelled: "danger",
  Refunded: "danger",
};

/** Splits PascalCase statuses so "PartiallyReceived" reads as words. */
function humanize(status: string) {
  return status.replace(/([a-z])([A-Z])/g, "$1 $2");
}

export function StatusBadge({ status }: { status: string }) {
  return <Badge tone={TONE_BY_STATUS[status] ?? "neutral"}>{humanize(status)}</Badge>;
}
