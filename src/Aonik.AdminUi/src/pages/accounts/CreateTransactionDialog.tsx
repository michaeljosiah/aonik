import { useState, useEffect, useMemo } from 'react';
import { toast } from 'sonner';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { accountService } from '@/services/accountService';
import type {
  CreateAccountTransactionRequest,
  AccountResponse,
} from '@/types';

interface CreateTransactionDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess: () => void;
  preselectedAccountId?: string;
}

const todayString = () => new Date().toISOString().slice(0, 10);

interface TransactionFormData {
  accountId: string;
  occurredAt: string;
  amount: string;
  currency: string;
  counterparty: string;
  description: string;
  reference: string;
  category: string;
  notes: string;
}

const createEmptyForm = (preselectedAccountId?: string): TransactionFormData => ({
  accountId: preselectedAccountId || '',
  occurredAt: todayString(),
  amount: '',
  currency: '',
  counterparty: '',
  description: '',
  reference: '',
  category: '',
  notes: '',
});

const fieldClassName =
  'flex h-10 w-full rounded-none border border-[var(--color-form-field-border)] bg-[var(--color-form-field-bg)] px-3 py-2 text-sm leading-5 text-[var(--color-form-field-text)] placeholder:text-[var(--color-form-field-placeholder)] focus-visible:outline-none focus-visible:ring-0 focus-visible:border-[var(--color-form-field-border-focus)]';

export function CreateTransactionDialog({
  open,
  onOpenChange,
  onSuccess,
  preselectedAccountId,
}: CreateTransactionDialogProps) {
  const [formData, setFormData] = useState<TransactionFormData>(() =>
    createEmptyForm(preselectedAccountId)
  );
  const [accounts, setAccounts] = useState<AccountResponse[]>([]);
  const [accountsLoading, setAccountsLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setAccountsLoading(true);
    accountService
      .listAccounts()
      .then((result) => setAccounts(result))
      .catch((err) => {
        console.error('Failed to load accounts:', err);
        setAccounts([]);
      })
      .finally(() => setAccountsLoading(false));
  }, [open]);

  useEffect(() => {
    if (open) {
      setFormData(createEmptyForm(preselectedAccountId));
      setError(null);
    }
  }, [open, preselectedAccountId]);

  const isValid = useMemo(() => {
    if (!formData.accountId) return false;
    if (!formData.occurredAt) return false;
    const amt = parseFloat(formData.amount);
    if (isNaN(amt) || amt === 0) return false;
    if (!formData.currency.trim() || formData.currency.trim().length !== 3) return false;
    return true;
  }, [formData.accountId, formData.occurredAt, formData.amount, formData.currency]);

  const handleClose = (nextOpen: boolean) => {
    if (!nextOpen) {
      setFormData(createEmptyForm(preselectedAccountId));
      setError(null);
    }
    onOpenChange(nextOpen);
  };

  const handleSave = async () => {
    if (!isValid || saving) return;
    setSaving(true);
    setError(null);
    try {
      const payload: CreateAccountTransactionRequest = {
        accountId: formData.accountId,
        occurredAt: new Date(formData.occurredAt).toISOString(),
        amount: parseFloat(formData.amount),
        currency: formData.currency.trim().toUpperCase(),
        counterparty: formData.counterparty.trim() || null,
        description: formData.description.trim() || null,
        reference: formData.reference.trim() || null,
        category: formData.category.trim() || null,
        notes: formData.notes.trim() || null,
      };
      await accountService.createTransaction(payload);
      toast.success('Transaction created successfully.');
      onSuccess();
      handleClose(false);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to create transaction';
      const userMessage =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(userMessage || message);
      toast.error(userMessage || message);
    } finally {
      setSaving(false);
    }
  };

  const updateField = <K extends keyof TransactionFormData>(
    field: K,
    value: TransactionFormData[K]
  ) => {
    setFormData((prev) => ({ ...prev, [field]: value }));
  };

  const formatAccountLabel = (account: AccountResponse) => {
    const parts = [account.maskedIdentifier, account.accountType];
    return parts.filter(Boolean).join(' - ');
  };

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-[550px] max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Add Transaction</DialogTitle>
          <DialogDescription>
            Manually create a transaction. Use negative amounts for debits, positive for credits.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 max-h-[60vh] overflow-y-auto pr-2">
          <div className="grid gap-2">
            <label className="text-sm font-medium text-[var(--color-text-primary)]">
              Account <span className="text-[var(--color-error)]">*</span>
            </label>
            {accountsLoading ? (
              <p className="text-sm text-[var(--color-text-tertiary)]">Loading accounts...</p>
            ) : (
              <Select
                value={formData.accountId}
                onValueChange={(value) => updateField('accountId', value)}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select an account" />
                </SelectTrigger>
                <SelectContent>
                  {accounts.map((account) => (
                    <SelectItem key={account.accountId} value={account.accountId}>
                      {formatAccountLabel(account)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="grid gap-2">
              <label className="text-sm font-medium text-[var(--color-text-primary)]">
                Date <span className="text-[var(--color-error)]">*</span>
              </label>
              <input
                type="date"
                value={formData.occurredAt}
                onChange={(e) => updateField('occurredAt', e.target.value)}
                className={fieldClassName}
              />
            </div>

            <div className="grid gap-2">
              <label className="text-sm font-medium text-[var(--color-text-primary)]">
                Currency <span className="text-[var(--color-error)]">*</span>
              </label>
              <input
                type="text"
                value={formData.currency}
                onChange={(e) => updateField('currency', e.target.value.toUpperCase().slice(0, 3))}
                className={fieldClassName}
                placeholder="e.g., USD"
                maxLength={3}
              />
            </div>
          </div>

          <div className="grid gap-2">
            <label className="text-sm font-medium text-[var(--color-text-primary)]">
              Amount <span className="text-[var(--color-error)]">*</span>
            </label>
            <input
              type="number"
              step="0.01"
              value={formData.amount}
              onChange={(e) => updateField('amount', e.target.value)}
              className={fieldClassName}
              placeholder="Negative = debit, Positive = credit"
            />
            <p className="text-xs text-[var(--color-text-tertiary)]">
              Negative values represent debits (money out), positive values represent credits (money in).
            </p>
          </div>

          <div className="grid gap-2">
            <label className="text-sm font-medium text-[var(--color-text-primary)]">
              Counterparty
            </label>
            <input
              type="text"
              value={formData.counterparty}
              onChange={(e) => updateField('counterparty', e.target.value)}
              className={fieldClassName}
              placeholder="Merchant or payer name"
            />
          </div>

          <div className="grid gap-2">
            <label className="text-sm font-medium text-[var(--color-text-primary)]">
              Description
            </label>
            <input
              type="text"
              value={formData.description}
              onChange={(e) => updateField('description', e.target.value)}
              className={fieldClassName}
              placeholder="Transaction description"
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="grid gap-2">
              <label className="text-sm font-medium text-[var(--color-text-primary)]">
                Reference
              </label>
              <input
                type="text"
                value={formData.reference}
                onChange={(e) => updateField('reference', e.target.value)}
                className={fieldClassName}
                placeholder="Payment reference"
              />
            </div>

            <div className="grid gap-2">
              <label className="text-sm font-medium text-[var(--color-text-primary)]">
                Category
              </label>
              <input
                type="text"
                value={formData.category}
                onChange={(e) => updateField('category', e.target.value)}
                className={fieldClassName}
                placeholder="e.g., Groceries"
              />
            </div>
          </div>

          <div className="grid gap-2">
            <label className="text-sm font-medium text-[var(--color-text-primary)]">
              Notes
            </label>
            <Textarea
              value={formData.notes}
              onChange={(e) => updateField('notes', e.target.value)}
              placeholder="Optional notes"
              rows={3}
            />
          </div>
        </div>

        {error && (
          <div className="rounded-md bg-[var(--color-error-light)] p-3 text-sm text-[var(--color-error)]">
            {error}
          </div>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => handleClose(false)} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={handleSave} disabled={saving || !isValid}>
            {saving ? 'Creating...' : 'Create Transaction'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
