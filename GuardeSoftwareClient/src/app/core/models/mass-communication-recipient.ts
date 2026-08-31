export interface MassCommunicationRecipient {
  id: number;
  name: string | null;
  email: string | null;
  phone: string | null;
  type: string | null;
  active: boolean;
  createdAt?: string;
  updatedAt?: string | null;
}

export interface UpsertMassCommunicationRecipient {
  name: string;
  email: string;
  phone: string;
  type: string;
}
