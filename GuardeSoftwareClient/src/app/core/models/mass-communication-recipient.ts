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

export interface MassCommunicationRecipientImportIssue {
  rowNumber: number;
  name: string | null;
  email: string | null;
  reason: string;
}

export interface MassCommunicationRecipientImportResult {
  dryRun: boolean;
  type: string;
  totalRows: number;
  validRows: number;
  newCount: number;
  existingActiveCount: number;
  existingInactiveCount: number;
  reactivatedCount: number;
  updatedCount: number;
  skippedInactiveCount: number;
  duplicateCount: number;
  invalidCount: number;
  missingEmailCount: number;
  importedCount: number;
  hasMoreIssues: boolean;
  issues: MassCommunicationRecipientImportIssue[];
}
