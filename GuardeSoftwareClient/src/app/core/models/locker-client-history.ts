export interface LockerClientHistory {
  id: number;
  clientId: number;
  clientName: string;
  paymentIdentifier: number | null;
  startDate: string;
  endDate: string | null;
  notes: string | null;
}
