export interface GetClientsRequest {
  pageNumber?: number;
  pageSize?: number;
  sortField?: string;
  sortDirection?: 'asc' | 'desc';
  searchTerm?: string;
  statusFilter?: string;
  active?: boolean;
}