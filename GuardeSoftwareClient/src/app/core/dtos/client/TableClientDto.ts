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
  // preferredPaymentMethodId: number | null;
  lockers: string[] | null;
  // email: string | null;
  // phone: string | null;
  active: boolean;
}
