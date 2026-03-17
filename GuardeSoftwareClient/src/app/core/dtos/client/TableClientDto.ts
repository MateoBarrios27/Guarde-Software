// Define la estructura de un cliente en la tabla, coincidiendo con GetTableClientsDto
export interface TableClient {
  id: number;
  paymentIdentifier: number | null;
  fullName: string;
  email: string | null;
  phone: string | null;
  city: string;
  balance: number;
  status: string; // "Al día", "Moroso", "Pendiente", "Baja"
  preferredPaymentMethodId: number | null;
  document: string | null;
  lockers: string[] | null;
  active: boolean;
}
