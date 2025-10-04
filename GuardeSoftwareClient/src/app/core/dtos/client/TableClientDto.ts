// Define la estructura de un cliente en la tabla, coincidiendo con GetTableClientsDto
export interface TableClient {
  id: number;
  paymentIdentifier: number | null;
  firstName: string;
  lastName: string;
  email: string | null;
  phone: string | null;
  city: string;
  balance: number;
  preferredPaymentMethodId: number | null;
  document: string | null;
  lockers: string[] | null;
  active: boolean;
}
