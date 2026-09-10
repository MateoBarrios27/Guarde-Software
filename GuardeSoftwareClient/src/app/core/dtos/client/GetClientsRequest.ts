export interface GetClientsRequest {
  pageNumber?: number;
  pageSize?: number;
  sortField?: string;
  sortDirection?: 'asc' | 'desc';
  searchTerm?: string;
  statusFilter?: string;
  active?: boolean;
  warehouseId?: number;
  warehouseIds?: number[];
  excludedWarehouseIds?: number[];
  advancedFilter?: string;
  advancedFilters?: string[];
  excludedAdvancedFilters?: string[];
  ivaConditions?: string[];
  excludedIvaConditions?: string[];
  billingTypeIds?: number[];
  excludedBillingTypeIds?: number[];
  preferredPaymentMethodIds?: number[];
  excludedPreferredPaymentMethodIds?: number[];
  lockerTypeIds?: number[];
  excludedLockerTypeIds?: number[];
  paymentDays?: number[];
  excludedPaymentDays?: number[];
}
