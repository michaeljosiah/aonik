// Journal Entries — visual port of ScreenJournal in
// templates/aonik-admin-starterkit/screens/journal-partners.jsx, wired to
// the existing /ledger/entries endpoints.
//
// Differences from the template, called out so they don't read as gaps:
//   • Status filter tabs include only the values the backend actually
//     emits today — Posted, Reversed (and Proposed/Draft when the
//     ledger emits them later).
//   • Inline agent-proposal row on a Proposed entry is not rendered:
//     proposals on journal entries aren't yet wired through the Agents
//     pipeline. When they are, slot Wave 4c's ProposalCard inside the
//     posted entry card.
//   • Per-entry document upload moves out — manage via /compliance/documents.
//   • Trial-balance check in the subtitle is a real client-side sum:
//     debits and credits across the visible page are reconciled.

import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import { AlertCircle, Plus, RefreshCw } from 'lucide-react';

import {
  Card as AonikCard,
  FilterBar,
  type FilterBarTab,
  PageHeader,
  Pill,
  type PillTone,
} from '@/components/layout/aonik';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { ledgerService } from '@/services/ledgerService';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import type {
  AddJournalEntryRequest,
  JournalEntryResponse,
  LedgerAccountSummary,
  LedgerSummary,
} from '@/types';

// ─── Helpers ─────────────────────────────────────────────────────────────

const STATUS_TONE: Record<string, PillTone> = {
  Posted: 'success',
  Proposed: 'pending',
  Draft: 'muted',
  Reversed: 'danger',
};

const FILTER_TABS: FilterBarTab[] = [
  { value: '', label: 'All' },
  { value: 'Posted', label: 'Posted' },
  { value: 'Proposed', label: 'Proposed' },
  { value: 'Draft', label: 'Draft' },
  { value: 'Reversed', label: 'Reversed' },
];

function shortEntryId(id: string): string {
  return `JE-${id.replace(/-/g, '').slice(0, 8).toUpperCase()}`;
}

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

function formatMoney(amount: number, currency: string): string {
  if (!currency) return amount.toLocaleString();
  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(amount);
  } catch {
    return `${currency} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}`;
  }
}

interface EntryTotals {
  debit: number;
  credit: number;
  currency: string;
}

function totalsFor(entry: JournalEntryResponse): EntryTotals {
  let debit = 0;
  let credit = 0;
  let currency = '';
  for (const line of entry.lines) {
    if (!currency) currency = line.currency;
    if (line.direction.toLowerCase() === 'debit') debit += line.amount;
    else if (line.direction.toLowerCase() === 'credit') credit += line.amount;
  }
  return { debit, credit, currency };
}

// ─── Page ────────────────────────────────────────────────────────────────

export function LedgerJournalEntriesPage() {
  const [ledgers, setLedgers] = useState<LedgerSummary[]>([]);
  const [accounts, setAccounts] = useState<LedgerAccountSummary[]>([]);
  const [entries, setEntries] = useState<JournalEntryResponse[]>([]);
  const [ledgerFilter, setLedgerFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [searchQuery, setSearchQuery] = useState('');
  const [loading, setLoading] = useState(true);
  const [initialLoad, setInitialLoad] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [createOpen, setCreateOpen] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [formState, setFormState] = useState({
    ledgerId: '',
    reference: '',
    description: '',
    debitAccountId: '',
    creditAccountId: '',
    amount: '',
    currency: '',
  });

  const loadLedgers = useCallback(async () => {
    try {
      const response = await ledgerService.listLedgers();
      setLedgers(response);
      if (!ledgerFilter && response.length > 0) {
        setLedgerFilter(response[0].id);
      }
      if (!formState.ledgerId && response.length > 0) {
        const first = response[0];
        setFormState((prev) => ({
          ...prev,
          ledgerId: first.id,
          currency: first.baseCurrency,
        }));
      }
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load ledgers.');
    }
  }, [formState.ledgerId, ledgerFilter]);

  const loadAccounts = useCallback(async (ledgerId: string) => {
    try {
      const response = await ledgerService.listAccounts(ledgerId);
      setAccounts(response);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load accounts.');
    }
  }, []);

  const loadEntries = useCallback(async (ledgerId: string) => {
    setLoading(true);
    setError(null);
    try {
      const response = await ledgerService.listJournalEntries(ledgerId);
      setEntries(response);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load journal entries.');
    } finally {
      setLoading(false);
      setInitialLoad(false);
    }
  }, []);

  useEffect(() => {
    void loadLedgers();
  }, [loadLedgers]);

  useEffect(() => {
    if (ledgerFilter) {
      void loadAccounts(ledgerFilter);
      void loadEntries(ledgerFilter);
      const ledger = ledgers.find((item) => item.id === ledgerFilter);
      if (ledger) {
        setFormState((prev) => ({
          ...prev,
          ledgerId: ledgerFilter,
          currency: ledger.baseCurrency,
        }));
      }
    }
  }, [ledgerFilter, ledgers, loadAccounts, loadEntries]);

  // ─── Filtering / metrics ──────────────────────────────────────────────

  const filteredEntries = useMemo(() => {
    let result = entries;
    if (statusFilter) {
      result = result.filter((e) => e.status === statusFilter);
    }
    const q = searchQuery.trim().toLowerCase();
    if (q) {
      result = result.filter(
        (e) =>
          (e.description ?? '').toLowerCase().includes(q) ||
          (e.reference ?? '').toLowerCase().includes(q) ||
          e.id.toLowerCase().includes(q),
      );
    }
    return result;
  }, [entries, statusFilter, searchQuery]);

  const trialBalance = useMemo(() => {
    let totalDebit = 0;
    let totalCredit = 0;
    for (const e of entries) {
      const t = totalsFor(e);
      totalDebit += t.debit;
      totalCredit += t.credit;
    }
    const balanced = Math.abs(totalDebit - totalCredit) < 0.005;
    return { totalDebit, totalCredit, balanced };
  }, [entries]);

  const proposedCount = useMemo(
    () => entries.filter((e) => e.status === 'Proposed').length,
    [entries],
  );

  const subtitle = (() => {
    if (entries.length === 0) {
      return 'Double-entry posts across this ledger';
    }
    const balancedFragment = trialBalance.balanced ? 'trial balance: balanced ✓' : 'trial balance: out of balance ✗';
    const proposedFragment = proposedCount > 0 ? ` · ${proposedCount} proposed` : '';
    return `${entries.length.toLocaleString()} entries${proposedFragment} · ${balancedFragment}`;
  })();

  // ─── Create ───────────────────────────────────────────────────────────

  const handleCreate = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      if (
        !formState.ledgerId ||
        !formState.debitAccountId ||
        !formState.creditAccountId ||
        !formState.amount
      ) {
        setFormError('Ledger, debit account, credit account, and amount are required.');
        return;
      }
      const amount = Number(formState.amount);
      if (Number.isNaN(amount) || amount <= 0) {
        setFormError('Amount must be greater than zero.');
        return;
      }

      setIsSaving(true);
      setFormError(null);
      try {
        const payload: AddJournalEntryRequest = {
          ledgerId: formState.ledgerId,
          reference: formState.reference || null,
          description: formState.description || null,
          lines: [
            {
              accountId: formState.debitAccountId,
              direction: 'Debit',
              amount,
              currency: formState.currency || 'USD',
              narration: 'Debit line',
            },
            {
              accountId: formState.creditAccountId,
              direction: 'Credit',
              amount,
              currency: formState.currency || 'USD',
              narration: 'Credit line',
            },
          ],
        };
        await ledgerService.addJournalEntry(payload);
        await loadEntries(formState.ledgerId);
        setFormState((prev) => ({
          ...prev,
          reference: '',
          description: '',
          debitAccountId: '',
          creditAccountId: '',
          amount: '',
        }));
        setCreateOpen(false);
      } catch (err: unknown) {
        const message =
          err && typeof err === 'object' && 'userMessage' in err
            ? String((err as { userMessage?: string }).userMessage ?? '')
            : '';
        setFormError(message || 'Unable to add journal entry.');
      } finally {
        setIsSaving(false);
      }
    },
    [formState, loadEntries],
  );

  const accountById = useMemo(() => {
    const map = new Map<string, LedgerAccountSummary>();
    for (const account of accounts) map.set(account.id, account);
    return map;
  }, [accounts]);

  const ledgerLabel = (id: string) => {
    const l = ledgers.find((x) => x.id === id);
    return l ? `${l.baseCurrency} · ${id.slice(0, 8)}` : id.slice(0, 8);
  };

  if (initialLoad) {
    return <PageLoadingScreen message="Loading journal entries" />;
  }

  return (
    <div className="flex flex-col gap-5 p-6 md:px-8">
      <PageHeader
        eyebrow="Finance · Ledger"
        title="Journal entries"
        subtitle={subtitle}
        actions={
          <>
            <div className="w-[180px]">
              <Select value={ledgerFilter} onValueChange={setLedgerFilter}>
                <SelectTrigger className="h-8 rounded-sm text-xs">
                  <SelectValue placeholder="Select ledger" />
                </SelectTrigger>
                <SelectContent>
                  {ledgers.map((l) => (
                    <SelectItem key={l.id} value={l.id}>
                      {ledgerLabel(l.id)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <Button
              variant="outline"
              size="sm"
              onClick={() => ledgerFilter && void loadEntries(ledgerFilter)}
              disabled={loading || !ledgerFilter}
            >
              <RefreshCw className={'h-3 w-3 ' + (loading ? 'animate-spin' : '')} />
              Refresh
            </Button>
            <Dialog open={createOpen} onOpenChange={setCreateOpen}>
              <DialogTrigger asChild>
                <Button size="sm" disabled={!ledgerFilter}>
                  <Plus className="h-3 w-3" />
                  New entry
                </Button>
              </DialogTrigger>
              <DialogContent>
                <DialogHeader>
                  <DialogTitle>New journal entry</DialogTitle>
                  <DialogDescription>
                    Posts a balanced debit/credit pair. Use multi-line entries via the
                    journal API for splits beyond one debit and one credit.
                  </DialogDescription>
                </DialogHeader>
                <form onSubmit={handleCreate} className="flex flex-col gap-3.5">
                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="je-ledger">Ledger</Label>
                    <Select
                      value={formState.ledgerId}
                      onValueChange={(v) =>
                        setFormState((prev) => ({ ...prev, ledgerId: v }))
                      }
                    >
                      <SelectTrigger id="je-ledger" className="h-9">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {ledgers.map((l) => (
                          <SelectItem key={l.id} value={l.id}>
                            {ledgerLabel(l.id)}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="grid grid-cols-2 gap-2.5">
                    <div className="flex flex-col gap-1.5">
                      <Label htmlFor="je-amount">Amount</Label>
                      <Input
                        id="je-amount"
                        value={formState.amount}
                        onChange={(e) =>
                          setFormState((prev) => ({ ...prev, amount: e.target.value }))
                        }
                        placeholder="0.00"
                        inputMode="decimal"
                      />
                    </div>
                    <div className="flex flex-col gap-1.5">
                      <Label htmlFor="je-currency">Currency</Label>
                      <Input
                        id="je-currency"
                        value={formState.currency}
                        onChange={(e) =>
                          setFormState((prev) => ({ ...prev, currency: e.target.value }))
                        }
                        placeholder="USD"
                      />
                    </div>
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="je-debit">Debit account</Label>
                    <Select
                      value={formState.debitAccountId}
                      onValueChange={(v) =>
                        setFormState((prev) => ({ ...prev, debitAccountId: v }))
                      }
                    >
                      <SelectTrigger id="je-debit" className="h-9">
                        <SelectValue placeholder="Select account" />
                      </SelectTrigger>
                      <SelectContent>
                        {accounts.map((a) => (
                          <SelectItem key={a.id} value={a.id}>
                            {a.code} · {a.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="je-credit">Credit account</Label>
                    <Select
                      value={formState.creditAccountId}
                      onValueChange={(v) =>
                        setFormState((prev) => ({ ...prev, creditAccountId: v }))
                      }
                    >
                      <SelectTrigger id="je-credit" className="h-9">
                        <SelectValue placeholder="Select account" />
                      </SelectTrigger>
                      <SelectContent>
                        {accounts.map((a) => (
                          <SelectItem key={a.id} value={a.id}>
                            {a.code} · {a.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="je-ref">Reference</Label>
                    <Input
                      id="je-ref"
                      value={formState.reference}
                      onChange={(e) =>
                        setFormState((prev) => ({ ...prev, reference: e.target.value }))
                      }
                      placeholder="Invoice / external ref"
                    />
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="je-desc">Memo</Label>
                    <Textarea
                      id="je-desc"
                      value={formState.description}
                      onChange={(e) =>
                        setFormState((prev) => ({ ...prev, description: e.target.value }))
                      }
                      placeholder="Short description"
                      rows={2}
                    />
                  </div>
                  {formError && (
                    <div className="rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
                      {formError}
                    </div>
                  )}
                  <DialogFooter>
                    <Button
                      type="button"
                      variant="outline"
                      onClick={() => setCreateOpen(false)}
                      disabled={isSaving}
                    >
                      Cancel
                    </Button>
                    <Button type="submit" disabled={isSaving}>
                      {isSaving ? 'Posting…' : 'Post entry'}
                    </Button>
                  </DialogFooter>
                </form>
              </DialogContent>
            </Dialog>
          </>
        }
      />

      {error && (
        <div className="flex items-center gap-3 rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] p-3 text-sm text-[var(--color-error)]">
          <AlertCircle className="h-4 w-4 flex-none" />
          <span className="flex-1">{error}</span>
          <Button
            variant="outline"
            size="sm"
            onClick={() => ledgerFilter && void loadEntries(ledgerFilter)}
          >
            <RefreshCw className="h-3 w-3" />
            Retry
          </Button>
        </div>
      )}

      <FilterBar
        tabs={FILTER_TABS}
        active={statusFilter}
        onTabChange={setStatusFilter}
        search={searchQuery}
        onSearchChange={setSearchQuery}
        searchPlaceholder="Filter by entry, memo, reference…"
        hideFilterButton
      />

      <div className="flex flex-col gap-3">
        {loading && entries.length === 0 ? (
          <AonikCard>
            <div className="flex items-center justify-center py-10">
              <RefreshCw className="h-5 w-5 animate-spin text-[var(--color-brand-primary)]" />
            </div>
          </AonikCard>
        ) : filteredEntries.length === 0 ? (
          <AonikCard>
            <div className="flex flex-col items-center justify-center py-10 text-center">
              <p className="text-sm font-medium text-[var(--color-text-primary)]">
                No journal entries
              </p>
              <p className="mt-1 text-xs text-[var(--color-text-tertiary)]">
                {searchQuery || statusFilter
                  ? 'Try adjusting the active tab or search.'
                  : 'Post the first entry into this ledger.'}
              </p>
            </div>
          </AonikCard>
        ) : (
          filteredEntries.map((entry) => (
            <EntryCard key={entry.id} entry={entry} accountById={accountById} />
          ))
        )}
      </div>
    </div>
  );
}

// ─── Entry card ──────────────────────────────────────────────────────────

function EntryCard({
  entry,
  accountById,
}: {
  entry: JournalEntryResponse;
  accountById: Map<string, LedgerAccountSummary>;
}) {
  const totals = totalsFor(entry);
  const tone = STATUS_TONE[entry.status] ?? 'default';

  return (
    <div className="overflow-hidden rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)]">
      <div className="flex items-center gap-3.5 border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-4 py-3">
        <span className="font-[family-name:var(--font-mono)] text-[12px] font-semibold text-[var(--color-brand-primary)]">
          {shortEntryId(entry.id)}
        </span>
        <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-secondary)]">
          {formatDate(entry.entryUtc)}
        </span>
        <span className="flex-1 truncate text-[13px] font-medium text-[var(--color-text-primary)]">
          {entry.description ?? entry.reference ?? '—'}
        </span>
        <Pill tone={tone} dot>
          {entry.status}
        </Pill>
      </div>

      <div>
        <div
          className="grid items-center gap-3.5 border-b border-[var(--color-border-light)] bg-[var(--color-surface)] px-4 py-2 text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]"
          style={{ gridTemplateColumns: '1fr 140px 140px' }}
        >
          <div>Account</div>
          <div className="text-right">Debit</div>
          <div className="text-right">Credit</div>
        </div>
        {entry.lines.map((line, i) => {
          const account = accountById.get(line.accountId);
          const accountLabel = account
            ? `${account.code} · ${account.name}`
            : `${line.accountId.slice(0, 8)} · (account not in ledger)`;
          const isDebit = line.direction.toLowerCase() === 'debit';
          return (
            <div
              key={line.id}
              className={
                'grid items-center gap-3.5 px-4 py-2.5 ' +
                (i < entry.lines.length - 1 ? 'border-b border-[var(--color-border-light)]' : '')
              }
              style={{ gridTemplateColumns: '1fr 140px 140px' }}
            >
              <div className="font-[family-name:var(--font-mono)] text-[12px] text-[var(--color-text-primary)]">
                {accountLabel}
                {line.narration && (
                  <span className="ml-2 font-[family-name:var(--font-sans)] text-[11px] text-[var(--color-text-tertiary)]">
                    · {line.narration}
                  </span>
                )}
              </div>
              <div
                className={
                  'text-right font-[family-name:var(--font-mono)] text-[12px] ' +
                  (isDebit
                    ? 'text-[var(--color-text-primary)]'
                    : 'text-[var(--color-text-tertiary)]')
                }
              >
                {isDebit ? formatMoney(line.amount, line.currency) : '—'}
              </div>
              <div
                className={
                  'text-right font-[family-name:var(--font-mono)] text-[12px] ' +
                  (isDebit
                    ? 'text-[var(--color-text-tertiary)]'
                    : 'text-[var(--color-text-primary)]')
                }
              >
                {isDebit ? '—' : formatMoney(line.amount, line.currency)}
              </div>
            </div>
          );
        })}
        <div
          className="grid items-center gap-3.5 border-t border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-4 py-2 text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]"
          style={{ gridTemplateColumns: '1fr 140px 140px' }}
        >
          <div>Totals</div>
          <div className="text-right font-[family-name:var(--font-mono)] text-[12px] text-[var(--color-text-primary)]">
            {formatMoney(totals.debit, totals.currency)}
          </div>
          <div className="text-right font-[family-name:var(--font-mono)] text-[12px] text-[var(--color-text-primary)]">
            {formatMoney(totals.credit, totals.currency)}
          </div>
        </div>
      </div>
    </div>
  );
}
