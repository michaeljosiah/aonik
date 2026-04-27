// Ledger Accounts list — visual port of ScreenAccounts in
// templates/aonik-admin-starterkit/screens/invoices-accounts.jsx, wired to
// the existing /ledger endpoints.
//
// Differences from the template, called out so they don't read as gaps:
//   • Balance / Δ vs prior columns are dropped — the LedgerAccountSummary
//     DTO does not carry running balances. Surfacing those would require
//     summing journal entry lines per account, an aggregation that
//     belongs on the API side.
//   • Bank column is dropped — there is no link from a LedgerAccount to
//     an external bank account in the current model.
//   • Hierarchy (Assets → Cash & equivalents → Operating · Chase USD) is
//     shown via account-type grouping rather than the template's
//     code-prefix-driven indentation. Same effect for the typical chart
//     of accounts.
//   • Account-level document upload (previously on this page) moved
//     out — documents are managed centrally via /compliance/documents.

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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
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
import type {
  CreateLedgerAccountRequest,
  LedgerAccountSummary,
  LedgerSummary,
} from '@/types';

// ─── Helpers ─────────────────────────────────────────────────────────────

const ACCOUNT_TYPES = ['Asset', 'Liability', 'Equity', 'Income', 'Expense'] as const;

const TYPE_TONE: Record<string, PillTone> = {
  Asset: 'info',
  Liability: 'warning',
  Equity: 'pending',
  Income: 'success',
  Expense: 'danger',
};

const FILTER_TABS: FilterBarTab[] = [
  { value: '', label: 'All' },
  ...ACCOUNT_TYPES.map((t) => ({ value: t, label: t })),
];

function formatDate(value?: string | null): string {
  if (!value) return '—';
  return new Date(value).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

function formatBalance(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency,
      maximumFractionDigits: 2,
      minimumFractionDigits: 0,
    }).format(amount);
  } catch {
    return `${currency} ${amount.toLocaleString(undefined, {
      maximumFractionDigits: 2,
      minimumFractionDigits: 0,
    })}`;
  }
}

function summariseBalance(account: LedgerAccountSummary): {
  primary: string;
  secondary: string | null;
} {
  const balances = account.balancesByCurrency ?? [];
  if (balances.length === 0) {
    return { primary: '—', secondary: null };
  }
  if (balances.length === 1) {
    const entry = balances[0];
    return { primary: formatBalance(entry.balance, entry.currency), secondary: null };
  }
  return {
    primary: formatBalance(balances[0].balance, balances[0].currency),
    secondary: `+${balances.length - 1} ${balances.length === 2 ? 'currency' : 'currencies'}`,
  };
}

// Group flat account list by accountType, preserving entry order within
// each group. Mirrors the template's hierarchy by surfacing the type as
// a section header instead of code-prefix indentation.
function groupByType(accounts: LedgerAccountSummary[]): Array<[string, LedgerAccountSummary[]]> {
  const groups = new Map<string, LedgerAccountSummary[]>();
  for (const t of ACCOUNT_TYPES) groups.set(t, []);
  for (const acct of accounts) {
    const list = groups.get(acct.accountType) ?? [];
    list.push(acct);
    groups.set(acct.accountType, list);
  }
  return Array.from(groups.entries()).filter(([, list]) => list.length > 0);
}

// ─── Page ────────────────────────────────────────────────────────────────

export function LedgerAccountsPage() {
  const [ledgers, setLedgers] = useState<LedgerSummary[]>([]);
  const [accounts, setAccounts] = useState<LedgerAccountSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [ledgerFilter, setLedgerFilter] = useState<string>('');
  const [typeFilter, setTypeFilter] = useState<string>('');
  const [searchQuery, setSearchQuery] = useState<string>('');

  const [createOpen, setCreateOpen] = useState(false);
  const [formState, setFormState] = useState<CreateLedgerAccountRequest>({
    ledgerId: '',
    name: '',
    code: '',
    accountType: 'Asset',
  });
  const [isSaving, setIsSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const loadLedgers = useCallback(async () => {
    try {
      const response = await ledgerService.listLedgers();
      setLedgers(response);
      if (!ledgerFilter && response.length > 0) {
        setLedgerFilter(response[0].id);
      }
      if (!formState.ledgerId && response.length > 0) {
        setFormState((prev) => ({ ...prev, ledgerId: response[0].id }));
      }
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load ledgers.');
    }
  }, [formState.ledgerId, ledgerFilter]);

  const loadAccounts = useCallback(async (ledgerId?: string) => {
    setLoading(true);
    setError(null);
    try {
      const response = await ledgerService.listAccounts(ledgerId);
      setAccounts(response);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load ledger accounts.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadLedgers();
  }, [loadLedgers]);

  useEffect(() => {
    if (ledgerFilter) {
      void loadAccounts(ledgerFilter);
    }
  }, [ledgerFilter, loadAccounts]);

  const handleCreate = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      if (!formState.ledgerId || !formState.name.trim() || !formState.code.trim()) {
        setFormError('Ledger, name, and code are required.');
        return;
      }
      setIsSaving(true);
      setFormError(null);
      try {
        await ledgerService.createAccount({
          ledgerId: formState.ledgerId,
          name: formState.name.trim(),
          code: formState.code.trim(),
          accountType: formState.accountType,
        });
        await loadAccounts(ledgerFilter || formState.ledgerId);
        setFormState((prev) => ({ ...prev, name: '', code: '' }));
        setCreateOpen(false);
      } catch (err: unknown) {
        const message =
          err && typeof err === 'object' && 'userMessage' in err
            ? String((err as { userMessage?: string }).userMessage ?? '')
            : '';
        setFormError(message || 'Unable to create ledger account.');
      } finally {
        setIsSaving(false);
      }
    },
    [formState, ledgerFilter, loadAccounts],
  );

  // Filter to active type filter + search
  const filteredAccounts = useMemo(() => {
    let result = accounts;
    if (typeFilter) {
      result = result.filter((a) => a.accountType === typeFilter);
    }
    const q = searchQuery.trim().toLowerCase();
    if (q) {
      result = result.filter(
        (a) =>
          a.name.toLowerCase().includes(q) ||
          a.code.toLowerCase().includes(q) ||
          a.accountType.toLowerCase().includes(q),
      );
    }
    return result;
  }, [accounts, typeFilter, searchQuery]);

  const grouped = useMemo(() => groupByType(filteredAccounts), [filteredAccounts]);

  const ledgerLabel = (id: string) => {
    const ledger = ledgers.find((l) => l.id === id);
    return ledger ? `${ledger.baseCurrency} · ${id.slice(0, 8)}` : id.slice(0, 8);
  };

  const subtitle = ledgers.length === 0
    ? 'Chart of accounts'
    : `${accounts.length.toLocaleString()} account${
        accounts.length === 1 ? '' : 's'
      } · ${ledgers.length} ledger${ledgers.length === 1 ? '' : 's'}`;

  return (
    <div className="flex flex-col gap-5 p-6 md:px-8">
      <PageHeader
        eyebrow="Finance · Ledger"
        title="Accounts"
        subtitle={subtitle}
        actions={
          <>
            <div className="w-[180px]">
              <Select value={ledgerFilter} onValueChange={setLedgerFilter}>
                <SelectTrigger className="h-8 rounded-sm text-xs">
                  <SelectValue placeholder="Select ledger" />
                </SelectTrigger>
                <SelectContent>
                  {ledgers.map((ledger) => (
                    <SelectItem key={ledger.id} value={ledger.id}>
                      {ledgerLabel(ledger.id)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <Button
              variant="outline"
              size="sm"
              onClick={() => ledgerFilter && void loadAccounts(ledgerFilter)}
              disabled={loading || !ledgerFilter}
            >
              <RefreshCw className={'h-3 w-3 ' + (loading ? 'animate-spin' : '')} />
              Refresh
            </Button>
            <Dialog open={createOpen} onOpenChange={setCreateOpen}>
              <DialogTrigger asChild>
                <Button size="sm" disabled={!ledgerFilter}>
                  <Plus className="h-3 w-3" />
                  New account
                </Button>
              </DialogTrigger>
              <DialogContent>
                <DialogHeader>
                  <DialogTitle>New ledger account</DialogTitle>
                  <DialogDescription>
                    Create an entry in the chart of accounts. Codes follow your
                    ledger's existing convention.
                  </DialogDescription>
                </DialogHeader>
                <form onSubmit={handleCreate} className="flex flex-col gap-3.5">
                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="account-ledger">Ledger</Label>
                    <Select
                      value={formState.ledgerId}
                      onValueChange={(value) =>
                        setFormState((prev) => ({ ...prev, ledgerId: value }))
                      }
                    >
                      <SelectTrigger id="account-ledger" className="h-9">
                        <SelectValue placeholder="Select ledger" />
                      </SelectTrigger>
                      <SelectContent>
                        {ledgers.map((ledger) => (
                          <SelectItem key={ledger.id} value={ledger.id}>
                            {ledgerLabel(ledger.id)}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="account-name">Name</Label>
                    <Input
                      id="account-name"
                      value={formState.name}
                      onChange={(e) =>
                        setFormState((prev) => ({ ...prev, name: e.target.value }))
                      }
                      placeholder="Cash on hand"
                    />
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="account-code">Code</Label>
                    <Input
                      id="account-code"
                      value={formState.code}
                      onChange={(e) =>
                        setFormState((prev) => ({ ...prev, code: e.target.value }))
                      }
                      placeholder="1000"
                    />
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="account-type">Type</Label>
                    <Select
                      value={formState.accountType}
                      onValueChange={(value) =>
                        setFormState((prev) => ({ ...prev, accountType: value }))
                      }
                    >
                      <SelectTrigger id="account-type" className="h-9">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {ACCOUNT_TYPES.map((t) => (
                          <SelectItem key={t} value={t}>
                            {t}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
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
                      {isSaving ? 'Saving…' : 'Create'}
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
            onClick={() => ledgerFilter && void loadAccounts(ledgerFilter)}
          >
            <RefreshCw className="h-3 w-3" />
            Retry
          </Button>
        </div>
      )}

      <FilterBar
        tabs={FILTER_TABS}
        active={typeFilter}
        onTabChange={setTypeFilter}
        search={searchQuery}
        onSearchChange={setSearchQuery}
        searchPlaceholder="Filter accounts by name, code, type…"
        hideFilterButton
      />

      <AonikCard padding={0}>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)] text-left text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                <th className="px-4 py-3 w-[120px]">Code</th>
                <th className="px-4 py-3">Account</th>
                <th className="px-4 py-3 w-[140px]">Type</th>
                <th className="px-4 py-3 w-[100px]">Currency</th>
                <th className="px-4 py-3 w-[160px] text-right">Balance</th>
                <th className="px-4 py-3 w-[120px]">Created</th>
              </tr>
            </thead>
            <tbody>
              {loading && accounts.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-4 py-12 text-center">
                    <RefreshCw className="mx-auto mb-2 h-5 w-5 animate-spin text-[var(--color-brand-primary)]" />
                    <p className="text-sm text-[var(--color-text-secondary)]">
                      Loading accounts…
                    </p>
                  </td>
                </tr>
              ) : filteredAccounts.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-4 py-12 text-center">
                    <p className="text-sm font-medium text-[var(--color-text-primary)]">
                      No accounts found
                    </p>
                    <p className="mt-1 text-xs text-[var(--color-text-tertiary)]">
                      {searchQuery || typeFilter
                        ? 'Try adjusting the active tab or search.'
                        : 'Create the first account in this ledger.'}
                    </p>
                  </td>
                </tr>
              ) : (
                grouped.map(([type, list]) => (
                  <RenderTypeGroup key={type} type={type} list={list} />
                ))
              )}
            </tbody>
          </table>
        </div>
      </AonikCard>
    </div>
  );
}

// ─── Type group ──────────────────────────────────────────────────────────

function RenderTypeGroup({
  type,
  list,
}: {
  type: string;
  list: LedgerAccountSummary[];
}) {
  // Group total: sum balances per currency across the type's accounts.
  const groupTotalsByCurrency = new Map<string, number>();
  for (const account of list) {
    for (const b of account.balancesByCurrency ?? []) {
      groupTotalsByCurrency.set(b.currency, (groupTotalsByCurrency.get(b.currency) ?? 0) + b.balance);
    }
  }
  const groupTotalDisplay = (() => {
    const entries = Array.from(groupTotalsByCurrency.entries());
    if (entries.length === 0) return '';
    if (entries.length === 1) return formatBalance(entries[0][1], entries[0][0]);
    return `${entries.length} currencies`;
  })();

  return (
    <>
      <tr className="border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)]/40">
        <td className="px-4 py-3 font-[family-name:var(--font-mono)] text-[11px] font-bold text-[var(--color-brand-primary)]">
          {type === 'Asset'
            ? '1000'
            : type === 'Liability'
              ? '2000'
              : type === 'Equity'
                ? '3000'
                : type === 'Income'
                  ? '4000'
                  : '5000'}
        </td>
        <td colSpan={3} className="px-4 py-3 text-[13px] font-bold text-[var(--color-text-primary)]">
          {type === 'Income' ? 'Revenue' : type === 'Expense' ? 'Expenses' : `${type}s`}
          <span className="ml-2 text-[11px] font-normal text-[var(--color-text-tertiary)]">
            {list.length} {list.length === 1 ? 'account' : 'accounts'}
          </span>
        </td>
        <td className="px-4 py-3 text-right font-[family-name:var(--font-mono)] text-[12px] font-bold text-[var(--color-text-primary)]">
          {groupTotalDisplay}
        </td>
        <td className="px-4 py-3" />
      </tr>
      {list.map((account) => {
        const balanceSummary = summariseBalance(account);
        return (
          <tr
            key={account.id}
            className="border-b border-[var(--color-border-light)] transition-colors hover:bg-[var(--color-surface-inset)]"
          >
            <td className="px-4 py-3 font-[family-name:var(--font-mono)] text-[11px] font-medium text-[var(--color-text-tertiary)]">
              {account.code}
            </td>
            <td className="px-4 py-3 pl-8">
              <span className="text-[13px] text-[var(--color-text-primary)]">
                {account.name}
              </span>
            </td>
            <td className="px-4 py-3">
              <Pill tone={TYPE_TONE[account.accountType] ?? 'default'} size="sm">
                {account.accountType}
              </Pill>
            </td>
            <td className="px-4 py-3 font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-secondary)]">
              {account.currency || '—'}
            </td>
            <td className="px-4 py-3 text-right">
              <div className="font-[family-name:var(--font-mono)] text-[12px] font-medium text-[var(--color-text-primary)]">
                {balanceSummary.primary}
              </div>
              {balanceSummary.secondary && (
                <div className="font-[family-name:var(--font-mono)] text-[10px] text-[var(--color-text-tertiary)]">
                  {balanceSummary.secondary}
                </div>
              )}
            </td>
            <td className="px-4 py-3 font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-secondary)]">
              {formatDate(account.createdUtc)}
            </td>
          </tr>
        );
      })}
    </>
  );
}
