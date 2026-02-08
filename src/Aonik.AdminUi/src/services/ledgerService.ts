import { api } from '@/lib/api';
import type {
  AddJournalEntryRequest,
  CreateLedgerAccountRequest,
  CreateLedgerRequest,
  JournalEntryResponse,
  LedgerAccountSummary,
  LedgerSummary,
} from '@/types';

export const ledgerService = {
  async listLedgers() {
    return api.get<LedgerSummary[]>('/ledger');
  },

  async createLedger(request: CreateLedgerRequest) {
    return api.post<LedgerSummary>('/ledger', request);
  },

  async listAccounts(ledgerId?: string) {
    const query = ledgerId ? `?ledgerId=${ledgerId}` : '';
    return api.get<LedgerAccountSummary[]>(`/ledger/accounts${query}`);
  },

  async createAccount(request: CreateLedgerAccountRequest) {
    return api.post<LedgerAccountSummary>('/ledger/accounts', request);
  },

  async listJournalEntries(ledgerId?: string) {
    const query = ledgerId ? `?ledgerId=${ledgerId}` : '';
    return api.get<JournalEntryResponse[]>(`/ledger/journal-entries${query}`);
  },

  async addJournalEntry(request: AddJournalEntryRequest) {
    return api.post<JournalEntryResponse>('/ledger/journal-entries', request);
  },
};
