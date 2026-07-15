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
  advancedFilter?: string;
  advancedFilters?: string[];
  ivaConditions?: string[];
  billingTypeIds?: number[];
  preferredPaymentMethodIds?: number[];
}