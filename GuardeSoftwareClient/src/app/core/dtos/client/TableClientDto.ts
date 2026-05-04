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
  active: boolean;
}

export interface WarehouseLockerItem {
  warehouse: string;
  lockers: string;
}


