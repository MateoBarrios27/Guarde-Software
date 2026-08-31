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
  pendingSurcharge?: number;
  status: string; 
  departureStatus?: string | null;
  lockers: string[] | null;
  warehouseLockers?: WarehouseLockerItem[];
  nextPaymentDay?: Date | string | null;
  deactivationDate?: Date | string | null;
  active: boolean;
  color?: string;
  comment?: string;
  commentUpdatedAt?: Date | string | null;
  ivaCondition?: string;
  billingTypeId?: number;
  billingType?: string;
  preferredPaymentMethodId?: number;
  preferredPaymentMethod?: string;

  // Precomputed properties for rendering performance
  _isFutureMonth?: boolean;
  _bgColor?: string;
  _colorLight?: string | null;
}

export interface WarehouseLockerItem {
  warehouse: string;
  lockers: string;
}


