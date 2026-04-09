import { useCallback, useEffect, useState } from 'react';
import { toast } from 'sonner';
import {
  Building2,
  CreditCard,
  DollarSign,
  Landmark,
  PiggyBank,
  Plus,
  RefreshCw,
  TrendingDown,
  TrendingUp,
  Wallet,
  X,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { personalFinanceService } from '@/services/personalFinanceService';
import type { CreatePersonalAccountRequest, PersonalAccountResponse } from '@/types';

/* -------------------------------------------------------------------------- */
/*  Constants                                                                  */
/* -------------------------------------------------------------------------- */

const ACCOUNT_TYPES = [
  { value: 'Checking', label: 'Checking', icon: Wallet },
  { value: 'Savings', label: 'Savings', icon: PiggyBank },
  { value: 'CreditCard', label: 'Credit Card', icon: CreditCard },
  { value: 'Investment', label: 'Investment', icon: TrendingUp },
  { value: 'Loan', label: 'Loan', icon: TrendingDown },
  { value: 'Mortgage', label: 'Mortgage', icon: Building2 },
  { value: 'Cash', label: 'Cash', icon: DollarSign },
  { value: 'Other', label: 'Other', icon: Landmark },
] as const;

const CURRENCIES = ['GBP', 'USD', 'EUR', 'NGN', 'GHS', 'KES', 'ZAR', 'UGX'];

const statusConfig: Record<string, { bg: string; text: string }> = {
  Active: { bg: 'bg-[var(--color-success-light)]', text: 'text-[var(--color-success)]' },
  Archived: { bg: 'bg-[var(--color-surface-inset)]', text: 'text-[var(--color-text-tertiary)]' },
  Closed: { bg: 'bg-[var(--color-error-light)]', text: 'text-[var(--color-error)]' },
};

/* -------------------------------------------------------------------------- */
/*  Helpers                                                                    */
/* -------------------------------------------------------------------------- */

function formatBalance(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency,
      maximumFractionDigits: 2,
    }).format(amount);
  } catch {
    return `${amount.toLocaleString()} ${currency}`;
  }
}

function AccountTypeIcon({ type }: { type: string }) {
  const match = ACCOUNT_TYPES.find((t) => t.value === type);
  const Icon = match?.icon ?? Landmark;
  return <Icon className="h-4 w-4" />;
}

/* -------------------------------------------------------------------------- */
/*  Add Account Panel                                                          */
/* -------------------------------------------------------------------------- */

interface AddPanelProps {
  onClose: () => void;
  onCreated: (account: PersonalAccountResponse) => void;
}

function AddAccountPanel({ onClose, onCreated }: AddPanelProps) {
  const [form, setForm] = useState<CreatePersonalAccountRequest>({
    name: '',
    accountType: 'Checking',
    currency: 'GBP',
    institutionName: '',
    last4: '',
    startingBalance: undefined,
  });
  const [saving, setSaving] = useState(false);

  const set = <K extends keyof CreatePersonalAccountRequest>(
    key: K,
    value: CreatePersonalAccountRequest[K],
  ) => setForm((prev) => ({ ...prev, [key]: value }));

  const handleSave = async () => {
    if (!form.name.trim()) {
      toast.error('Account name is required.');
      return;
    }
    setSaving(true);
    try {
      const created = await personalFinanceService.createAccount({
        ...form,
        name: form.name.trim(),
        institutionName: form.institutionName?.trim() || undefined,
        last4: form.last4?.trim() || undefined,
      });
      toast.success('Account added.');
      onCreated(created);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      toast.error(message || 'Failed to create account.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      {/* Backdrop */}
      <div className="fixed inset-0 z-40 bg-black/20" onClick={onClose} />

      {/* Panel */}
      <div className="fixed right-0 top-0 bottom-0 z-50 w-[22rem] bg-[var(--color-surface)] shadow-2xl flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-[var(--color-border-light)] px-5 py-4">
          <h3 className="text-sm font-semibold text-[var(--color-text-primary)]">Add Account</h3>
          <button
            type="button"
            onClick={onClose}
            className="rounded-md p-1 text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)] transition-colors"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* Form */}
        <div className="flex-1 overflow-y-auto px-5 py-4 space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="acc-name">Account Name</Label>
            <Input
              id="acc-name"
              value={form.name}
              onChange={(e) => set('name', e.target.value)}
              placeholder="e.g. Barclays Current Account"
            />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="acc-type">Account Type</Label>
            <Select value={form.accountType} onValueChange={(v) => set('accountType', v)}>
              <SelectTrigger id="acc-type">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {ACCOUNT_TYPES.map((t) => (
                  <SelectItem key={t.value} value={t.value}>
                    {t.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="acc-currency">Currency</Label>
            <Select value={form.currency} onValueChange={(v) => set('currency', v)}>
              <SelectTrigger id="acc-currency">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {CURRENCIES.map((c) => (
                  <SelectItem key={c} value={c}>
                    {c}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="acc-institution">Institution <span className="text-[var(--color-text-tertiary)] font-normal">— optional</span></Label>
            <Input
              id="acc-institution"
              value={form.institutionName ?? ''}
              onChange={(e) => set('institutionName', e.target.value)}
              placeholder="e.g. Barclays"
            />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="acc-last4">Last 4 digits <span className="text-[var(--color-text-tertiary)] font-normal">— optional</span></Label>
            <Input
              id="acc-last4"
              value={form.last4 ?? ''}
              onChange={(e) => set('last4', e.target.value)}
              placeholder="e.g. 4242"
              maxLength={4}
            />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="acc-balance">Starting Balance <span className="text-[var(--color-text-tertiary)] font-normal">— optional</span></Label>
            <Input
              id="acc-balance"
              type="number"
              value={form.startingBalance?.toString() ?? ''}
              onChange={(e) =>
                set('startingBalance', e.target.value ? Number(e.target.value) : undefined)
              }
              placeholder="0.00"
            />
          </div>
        </div>

        {/* Footer */}
        <div className="border-t border-[var(--color-border-light)] px-5 py-4 flex items-center gap-3">
          <Button onClick={handleSave} disabled={saving} className="flex-1">
            {saving ? 'Saving...' : 'Add Account'}
          </Button>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
        </div>
      </div>
    </>
  );
}

/* -------------------------------------------------------------------------- */
/*  Account Card                                                               */
/* -------------------------------------------------------------------------- */

function AccountCard({ account }: { account: PersonalAccountResponse }) {
  const status = statusConfig[account.status] ?? {
    bg: 'bg-[var(--color-surface-inset)]',
    text: 'text-[var(--color-text-secondary)]',
  };
  const typeLabel =
    ACCOUNT_TYPES.find((t) => t.value === account.accountType)?.label ?? account.accountType;

  return (
    <Card>
      <CardContent className="p-4">
        <div className="flex items-start justify-between gap-3">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)]">
              <AccountTypeIcon type={account.accountType} />
            </div>
            <div>
              <p className="text-sm font-semibold text-[var(--color-text-primary)]">
                {account.name}
                {account.last4 && (
                  <span className="ml-1.5 font-normal text-[var(--color-text-tertiary)]">
                    ·· {account.last4}
                  </span>
                )}
              </p>
              <p className="text-xs text-[var(--color-text-tertiary)]">
                {account.institutionName ? `${account.institutionName} · ` : ''}
                {typeLabel}
              </p>
            </div>
          </div>

          <div className="flex flex-col items-end gap-1.5 shrink-0">
            <p className="text-base font-bold text-[var(--color-text-primary)]">
              {formatBalance(account.currentBalance, account.currency)}
            </p>
            <Badge className={`rounded-full text-xs ${status.bg} ${status.text}`}>
              {account.status}
            </Badge>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

/* -------------------------------------------------------------------------- */
/*  Main Component                                                             */
/* -------------------------------------------------------------------------- */

export function AccountsSubTab() {
  const [accounts, setAccounts] = useState<PersonalAccountResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showAdd, setShowAdd] = useState(false);
  const [includeArchived, setIncludeArchived] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await personalFinanceService.listAccounts(includeArchived);
      setAccounts(data);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load accounts.');
    } finally {
      setLoading(false);
    }
  }, [includeArchived]);

  useEffect(() => {
    load();
  }, [load]);

  const handleCreated = (account: PersonalAccountResponse) => {
    setAccounts((prev) => [account, ...prev]);
    setShowAdd(false);
  };

  const active = accounts.filter((a) => !a.isArchived);
  const archived = accounts.filter((a) => a.isArchived);
  const totalBalance = active.reduce((sum, a) => sum + a.currentBalance, 0);

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm font-medium text-[var(--color-text-primary)]">
            {active.length} account{active.length !== 1 ? 's' : ''}
          </p>
          {active.length > 0 && (
            <p className="text-xs text-[var(--color-text-tertiary)]">
              Combined balance across all active accounts
            </p>
          )}
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="ghost"
            size="icon-sm"
            onClick={load}
            disabled={loading}
            title="Refresh"
          >
            <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
          </Button>
          <Button size="sm" onClick={() => setShowAdd(true)}>
            <Plus className="mr-1.5 h-3.5 w-3.5" />
            Add Account
          </Button>
        </div>
      </div>

      {/* Error */}
      {error && (
        <div className="rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-4 py-3 text-sm text-[var(--color-error)]">
          {error}
        </div>
      )}

      {/* Loading */}
      {loading ? (
        <div className="flex items-center justify-center py-12">
          <div className="h-6 w-6 animate-spin rounded-full border-2 border-[var(--color-brand-primary)] border-t-transparent" />
        </div>
      ) : accounts.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-12 text-center">
          <div className="mb-3 flex h-14 w-14 items-center justify-center rounded-2xl bg-[var(--color-surface-inset)]">
            <Wallet className="h-7 w-7 text-[var(--color-text-tertiary)]" />
          </div>
          <p className="mb-0.5 text-sm font-medium text-[var(--color-text-secondary)]">
            No accounts yet
          </p>
          <p className="mb-4 text-xs text-[var(--color-text-tertiary)]">
            Add a manual account or connect via open banking.
          </p>
          <Button size="sm" onClick={() => setShowAdd(true)}>
            <Plus className="mr-1.5 h-3.5 w-3.5" />
            Add Account
          </Button>
        </div>
      ) : (
        <>
          {/* Active accounts */}
          <div className="space-y-2">
            {active.map((account) => (
              <AccountCard key={account.personalAccountId} account={account} />
            ))}
          </div>

          {/* Archived toggle */}
          {archived.length > 0 || includeArchived ? (
            <button
              type="button"
              onClick={() => setIncludeArchived((v) => !v)}
              className="text-xs text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)] transition-colors"
            >
              {includeArchived
                ? `Hide ${archived.length} archived`
                : `Show ${archived.length} archived account${archived.length !== 1 ? 's' : ''}`}
            </button>
          ) : null}

          {includeArchived && archived.length > 0 && (
            <div className="space-y-2 opacity-60">
              {archived.map((account) => (
                <AccountCard key={account.personalAccountId} account={account} />
              ))}
            </div>
          )}
        </>
      )}

      {/* Add panel */}
      {showAdd && (
        <AddAccountPanel onClose={() => setShowAdd(false)} onCreated={handleCreated} />
      )}
    </div>
  );
}
