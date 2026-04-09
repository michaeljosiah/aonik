import { useCallback, useEffect, useState } from 'react';
import { toast } from 'sonner';
import {
  ArrowDownLeft,
  ArrowUpRight,
  Plus,
  RefreshCw,
  Search,
  X,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { personalFinanceService } from '@/services/personalFinanceService';
import type {
  CreateManualPersonalTransactionRequest,
  PersonalAccountResponse,
  PersonalTransactionResponse,
  TransactionCategoryResponse,
} from '@/types';

/* -------------------------------------------------------------------------- */
/*  Helpers                                                                    */
/* -------------------------------------------------------------------------- */

const CURRENCIES = ['GBP', 'USD', 'EUR', 'NGN', 'GHS', 'KES', 'ZAR', 'UGX'];

function formatAmount(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency,
      maximumFractionDigits: 2,
    }).format(Math.abs(amount));
  } catch {
    return `${Math.abs(amount).toLocaleString()} ${currency}`;
  }
}

function formatDate(dateString: string): string {
  return new Date(dateString).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
}

function isoDate(date: Date): string {
  return date.toISOString().slice(0, 10);
}

/* -------------------------------------------------------------------------- */
/*  Add Transaction Panel                                                      */
/* -------------------------------------------------------------------------- */

interface AddPanelProps {
  accounts: PersonalAccountResponse[];
  categories: TransactionCategoryResponse[];
  onClose: () => void;
  onCreated: (txn: PersonalTransactionResponse) => void;
}

function AddTransactionPanel({ accounts, categories, onClose, onCreated }: AddPanelProps) {
  const [form, setForm] = useState<CreateManualPersonalTransactionRequest>({
    occurredAt: isoDate(new Date()),
    amount: 0,
    currency: 'GBP',
    merchant: '',
    description: '',
    category: '',
    notes: '',
    personalAccountId: '',
  });
  const [saving, setSaving] = useState(false);

  const set = <K extends keyof CreateManualPersonalTransactionRequest>(
    key: K,
    value: CreateManualPersonalTransactionRequest[K],
  ) => setForm((prev) => ({ ...prev, [key]: value }));

  const handleSave = async () => {
    if (!form.amount || form.amount === 0) {
      toast.error('Amount is required.');
      return;
    }
    setSaving(true);
    try {
      const created = await personalFinanceService.createTransaction({
        ...form,
        merchant: form.merchant?.trim() || undefined,
        description: form.description?.trim() || undefined,
        category: form.category?.trim() || undefined,
        notes: form.notes?.trim() || undefined,
        personalAccountId: form.personalAccountId?.trim() || undefined,
      });
      toast.success('Transaction added.');
      onCreated(created);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      toast.error(message || 'Failed to add transaction.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <div className="fixed inset-0 z-40 bg-black/20" onClick={onClose} />
      <div className="fixed right-0 top-0 bottom-0 z-50 w-[22rem] bg-[var(--color-surface)] shadow-2xl flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-[var(--color-border-light)] px-5 py-4">
          <h3 className="text-sm font-semibold text-[var(--color-text-primary)]">
            Add Transaction
          </h3>
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
          {/* Amount + currency row */}
          <div className="space-y-1.5">
            <Label>Amount</Label>
            <div className="flex gap-2">
              <Input
                type="number"
                step="0.01"
                value={form.amount || ''}
                onChange={(e) => set('amount', Number(e.target.value))}
                placeholder="0.00"
                className="flex-1"
              />
              <Select value={form.currency} onValueChange={(v) => set('currency', v)}>
                <SelectTrigger className="w-24">
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
            <p className="text-xs text-[var(--color-text-tertiary)]">
              Use a negative value for debits (money out).
            </p>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="txn-date">Date</Label>
            <Input
              id="txn-date"
              type="date"
              value={form.occurredAt.slice(0, 10)}
              onChange={(e) => set('occurredAt', e.target.value)}
            />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="txn-merchant">
              Merchant <span className="text-[var(--color-text-tertiary)] font-normal">— optional</span>
            </Label>
            <Input
              id="txn-merchant"
              value={form.merchant ?? ''}
              onChange={(e) => set('merchant', e.target.value)}
              placeholder="e.g. Tesco, Amazon"
            />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="txn-description">
              Description <span className="text-[var(--color-text-tertiary)] font-normal">— optional</span>
            </Label>
            <Input
              id="txn-description"
              value={form.description ?? ''}
              onChange={(e) => set('description', e.target.value)}
              placeholder="Short description"
            />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="txn-category">
              Category <span className="text-[var(--color-text-tertiary)] font-normal">— optional</span>
            </Label>
            <Select
              value={form.category ?? ''}
              onValueChange={(v) => set('category', v === '__none__' ? '' : v)}
            >
              <SelectTrigger id="txn-category">
                <SelectValue placeholder="Select category..." />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__none__">Uncategorised</SelectItem>
                {categories.map((cat) => (
                  <SelectItem key={cat.code} value={cat.code}>
                    {cat.displayName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {accounts.length > 0 && (
            <div className="space-y-1.5">
              <Label htmlFor="txn-account">
                Account <span className="text-[var(--color-text-tertiary)] font-normal">— optional</span>
              </Label>
              <Select
                value={form.personalAccountId ?? ''}
                onValueChange={(v) => set('personalAccountId', v === '__none__' ? '' : v)}
              >
                <SelectTrigger id="txn-account">
                  <SelectValue placeholder="Select account..." />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__none__">No account</SelectItem>
                  {accounts
                    .filter((a) => !a.isArchived)
                    .map((a) => (
                      <SelectItem key={a.personalAccountId} value={a.personalAccountId}>
                        {a.name}
                        {a.last4 ? ` ·· ${a.last4}` : ''}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            </div>
          )}

          <div className="space-y-1.5">
            <Label htmlFor="txn-notes">
              Notes <span className="text-[var(--color-text-tertiary)] font-normal">— optional</span>
            </Label>
            <Textarea
              id="txn-notes"
              value={form.notes ?? ''}
              onChange={(e) => set('notes', e.target.value)}
              placeholder="Any additional notes"
              rows={3}
            />
          </div>
        </div>

        {/* Footer */}
        <div className="border-t border-[var(--color-border-light)] px-5 py-4 flex items-center gap-3">
          <Button onClick={handleSave} disabled={saving} className="flex-1">
            {saving ? 'Saving...' : 'Add Transaction'}
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
/*  Transaction Row                                                            */
/* -------------------------------------------------------------------------- */

function TransactionRow({
  txn,
  accounts,
}: {
  txn: PersonalTransactionResponse;
  accounts: PersonalAccountResponse[];
}) {
  const isDebit = txn.amount < 0;
  const account = accounts.find((a) => a.personalAccountId === txn.personalAccountId);

  return (
    <div className="flex items-center gap-3 border-b border-[var(--color-border-light)] py-3 last:border-b-0">
      {/* Direction icon */}
      <div
        className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-full ${
          isDebit
            ? 'bg-[var(--color-error-light)] text-[var(--color-error)]'
            : 'bg-[var(--color-success-light)] text-[var(--color-success)]'
        }`}
      >
        {isDebit ? (
          <ArrowUpRight className="h-3.5 w-3.5" />
        ) : (
          <ArrowDownLeft className="h-3.5 w-3.5" />
        )}
      </div>

      {/* Details */}
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium text-[var(--color-text-primary)]">
          {txn.merchant || txn.description || 'Manual transaction'}
        </p>
        <div className="flex items-center gap-2 mt-0.5">
          <span className="text-xs text-[var(--color-text-tertiary)]">
            {formatDate(txn.occurredAt)}
          </span>
          {txn.category && (
            <Badge variant="secondary" className="rounded-full text-[10px] px-1.5 py-0">
              {txn.category}
            </Badge>
          )}
          {account && (
            <span className="text-xs text-[var(--color-text-tertiary)]">
              {account.name}
              {account.last4 ? ` ·· ${account.last4}` : ''}
            </span>
          )}
        </div>
      </div>

      {/* Amount */}
      <p
        className={`shrink-0 text-sm font-semibold tabular-nums ${
          isDebit ? 'text-[var(--color-error)]' : 'text-[var(--color-success)]'
        }`}
      >
        {isDebit ? '-' : '+'}{formatAmount(txn.amount, txn.currency)}
      </p>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/*  Main Component                                                             */
/* -------------------------------------------------------------------------- */

export function TransactionsSubTab() {
  const [transactions, setTransactions] = useState<PersonalTransactionResponse[]>([]);
  const [accounts, setAccounts] = useState<PersonalAccountResponse[]>([]);
  const [categories, setCategories] = useState<TransactionCategoryResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showAdd, setShowAdd] = useState(false);

  // Filters
  const [search, setSearch] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('');
  const [accountFilter, setAccountFilter] = useState('');
  const [fromFilter, setFromFilter] = useState('');
  const [toFilter, setToFilter] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [txns, accs, cats] = await Promise.all([
        personalFinanceService.listTransactions({
          search: search || undefined,
          category: categoryFilter || undefined,
          personalAccountId: accountFilter || undefined,
          from: fromFilter || undefined,
          to: toFilter || undefined,
          pageSize: 100,
        }),
        personalFinanceService.listAccounts(),
        personalFinanceService.listCategories(),
      ]);
      setTransactions(txns);
      setAccounts(accs);
      setCategories(cats.categories);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load transactions.');
    } finally {
      setLoading(false);
    }
  }, [search, categoryFilter, accountFilter, fromFilter, toFilter]);

  useEffect(() => {
    load();
  }, [load]);

  const handleCreated = (txn: PersonalTransactionResponse) => {
    setTransactions((prev) => [txn, ...prev]);
    setShowAdd(false);
  };

  const clearFilters = () => {
    setSearch('');
    setCategoryFilter('');
    setAccountFilter('');
    setFromFilter('');
    setToFilter('');
  };

  const hasFilters = search || categoryFilter || accountFilter || fromFilter || toFilter;

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <p className="text-sm font-medium text-[var(--color-text-primary)]">
          {loading ? 'Loading...' : `${transactions.length} transaction${transactions.length !== 1 ? 's' : ''}`}
        </p>
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="icon-sm" onClick={load} disabled={loading} title="Refresh">
            <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
          </Button>
          <Button size="sm" onClick={() => setShowAdd(true)}>
            <Plus className="mr-1.5 h-3.5 w-3.5" />
            Add Transaction
          </Button>
        </div>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap items-center gap-2">
        <div className="relative w-52">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-[var(--color-text-tertiary)]" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search merchant, description..."
            className="pl-8 h-8 text-xs"
          />
        </div>

        <Select
          value={categoryFilter || undefined}
          onValueChange={(v) => setCategoryFilter(v === '__all__' ? '' : v)}
        >
          <SelectTrigger className="h-8 w-40 text-xs">
            <SelectValue placeholder="All categories" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__all__">All categories</SelectItem>
            {categories.map((cat) => (
              <SelectItem key={cat.code} value={cat.code}>
                {cat.displayName}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        {accounts.length > 0 && (
          <Select
            value={accountFilter || undefined}
            onValueChange={(v) => setAccountFilter(v === '__all__' ? '' : v)}
          >
            <SelectTrigger className="h-8 w-40 text-xs">
              <SelectValue placeholder="All accounts" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="__all__">All accounts</SelectItem>
              {accounts
                .filter((a) => !a.isArchived)
                .map((a) => (
                  <SelectItem key={a.personalAccountId} value={a.personalAccountId}>
                    {a.name}
                  </SelectItem>
                ))}
            </SelectContent>
          </Select>
        )}

        <Input
          type="date"
          value={fromFilter}
          onChange={(e) => setFromFilter(e.target.value)}
          className="h-8 w-36 text-xs"
          title="From date"
        />
        <Input
          type="date"
          value={toFilter}
          onChange={(e) => setToFilter(e.target.value)}
          className="h-8 w-36 text-xs"
          title="To date"
        />

        {hasFilters && (
          <button
            type="button"
            onClick={clearFilters}
            className="text-xs text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)] transition-colors flex items-center gap-1"
          >
            <X className="h-3 w-3" />
            Clear
          </button>
        )}
      </div>

      {/* Error */}
      {error && (
        <div className="rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-4 py-3 text-sm text-[var(--color-error)]">
          {error}
        </div>
      )}

      {/* List */}
      {loading ? (
        <div className="flex items-center justify-center py-12">
          <div className="h-6 w-6 animate-spin rounded-full border-2 border-[var(--color-brand-primary)] border-t-transparent" />
        </div>
      ) : transactions.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-12 text-center">
          <div className="mb-3 flex h-14 w-14 items-center justify-center rounded-2xl bg-[var(--color-surface-inset)]">
            <ArrowUpRight className="h-7 w-7 text-[var(--color-text-tertiary)]" />
          </div>
          <p className="mb-0.5 text-sm font-medium text-[var(--color-text-secondary)]">
            {hasFilters ? 'No transactions match your filters' : 'No transactions yet'}
          </p>
          {!hasFilters && (
            <p className="mb-4 text-xs text-[var(--color-text-tertiary)]">
              Add a manual transaction or import a bank statement.
            </p>
          )}
          {!hasFilters && (
            <Button size="sm" onClick={() => setShowAdd(true)}>
              <Plus className="mr-1.5 h-3.5 w-3.5" />
              Add Transaction
            </Button>
          )}
        </div>
      ) : (
        <div className="divide-y divide-transparent">
          {transactions.map((txn) => (
            <TransactionRow
              key={txn.personalTransactionId}
              txn={txn}
              accounts={accounts}
            />
          ))}
        </div>
      )}

      {/* Add panel */}
      {showAdd && (
        <AddTransactionPanel
          accounts={accounts}
          categories={categories}
          onClose={() => setShowAdd(false)}
          onCreated={handleCreated}
        />
      )}
    </div>
  );
}
