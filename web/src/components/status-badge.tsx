export function StatusBadge({ status }: { status: string }) {
  const tone =
    status === "Cancelled"
      ? "bg-red-100 text-red-800"
      : status === "Received" || status === "Completed" || status === "Refunded"
        ? "bg-green-100 text-green-800"
        : status === "PartiallyReceived" || status === "PartiallyRefunded"
          ? "bg-amber-100 text-amber-800"
          : "bg-gray-100 text-gray-700";
  return <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${tone}`}>{status}</span>;
}
