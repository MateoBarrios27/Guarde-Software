// Define la estructura de un cliente en la tabla, coincidiendo con GetTableClientsDto
export interface TableClient {
  id: number;
  paymentIdentifier: number | null;
  fullName: string;
  city: string;
  balance: number;
  previousBalance: number; 
  interestAmount: number;   
  currentRent: number;    
  status: string; 
  lockers: string[] | null;
  warehouseLockers?: WarehouseLockerItem[];
  nextPaymentDay?: Date | string | null;
  deactivationDate?: Date | string | null;
  active: boolean;
  color?: string;
  comment?: string;
  commentUpdatedAt?: Date | string | null;
}

export interface WarehouseLockerItem {
  warehouse: string;
  lockers: string;
}


