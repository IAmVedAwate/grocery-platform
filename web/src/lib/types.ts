// Mirrors the backend's DTO records (backend/src/Api/Controllers/*.cs).
// Kept as one file so every page shares the same shape instead of
// redeclaring it — these are the wire contracts, not domain models.

export type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type ProductDto = {
  id: string;
  sku: string;
  barcode: string | null;
  name: string;
  price: number;
  taxRatePercent: number;
  isActive: boolean;
  lowStockThreshold: number;
};

export type InventoryOverviewRow = {
  productId: string;
  sku: string;
  name: string;
  quantityOnHand: number;
  lowStockThreshold: number;
  isLowStock: boolean;
};

export type StockMovementDto = {
  id: string;
  type: "Receipt" | "Sale" | "Adjustment" | "Transfer";
  quantityDelta: number;
  referenceType: string | null;
  referenceId: string | null;
  reason: string | null;
  resultingQuantity: number;
  createdAtUtc: string;
};

export type SupplierDto = {
  id: string;
  name: string;
  contactInfo: string | null;
  status: "Active" | "Inactive";
  paymentTermsDays: number | null;
};

export type PurchaseOrderItemDto = {
  productId: string;
  quantityOrdered: number;
  unitCost: number;
  quantityReceived: number;
};

export type PurchaseOrderStatus = "Draft" | "Submitted" | "Approved" | "PartiallyReceived" | "Received" | "Cancelled";

export type PurchaseOrderDto = {
  id: string;
  supplierId: string;
  status: PurchaseOrderStatus;
  approvedByUserId: string | null;
  createdAtUtc: string;
  items: PurchaseOrderItemDto[];
};

export type CustomerDto = {
  id: string;
  name: string;
  phone: string | null;
  email: string | null;
};

export type SalesOrderItemDto = {
  productId: string;
  quantity: number;
  unitPrice: number;
  taxAmount: number;
  lineDiscount: number;
  quantityRefunded: number;
};

export type SalesOrderStatus = "Completed" | "PartiallyRefunded" | "Refunded";

export type SalesOrderDto = {
  id: string;
  customerId: string | null;
  status: SalesOrderStatus;
  subtotalAmount: number;
  taxAmount: number;
  discountAmount: number;
  totalAmount: number;
  createdAtUtc: string;
  invoiceNumber: string | null;
  items: SalesOrderItemDto[];
};

export type SalesByDayRow = {
  date: string; // "yyyy-MM-dd"
  orderCount: number;
  revenue: number;
  averageOrderValue: number;
};

export type SalesByProductRow = {
  productId: string;
  sku: string;
  name: string;
  quantitySold: number;
  revenue: number;
};

export type SalesByCategoryRow = {
  categoryId: string | null;
  categoryName: string;
  quantitySold: number;
  revenue: number;
};

export type NotificationDto = {
  id: string;
  type: string;
  referenceId: string | null;
  payload: string;
  isRead: boolean;
  createdAtUtc: string;
};

export type CategoryDto = {
  id: string;
  name: string;
};

export type StaffUserDto = {
  id: string;
  email: string;
  displayName: string;
  isActive: boolean;
  permissions: string[];
};
