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
import { catalogService } from '@/services/catalogService';
import type { CreateAccountRequest, CatalogCountryItem, CatalogCurrencyItem } from '@/types';

interface CreateAccountDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess: () => void;
}

const accountTypes = [
  'BankAccount',
  'CreditCard',
  'MobileWallet',
  'Loan',
  'Investment',
  'Other',
] as const;

const createEmptyForm = (): CreateAccountRequest => ({
  name: '',
  accountType: '',
  currency: '',
  country: null,
  institutionName: null,
  last4: null,
  notes: null,
});

const fieldClassName =
  'flex h-10 w-full rounded-none border border-[var(--color-form-field-border)] bg-[var(--color-form-field-bg)] px-3 py-2 text-sm leading-5 text-[var(--color-form-field-text)] placeholder:text-[var(--color-form-field-placeholder)] focus-visible:outline-none focus-visible:ring-0 focus-visible:border-[var(--color-form-field-border-focus)]';

export function CreateAccountDialog({ open, onOpenChange, onSuccess }: CreateAccountDialogProps) {
  const [formData, setFormData] = useState<CreateAccountRequest>(() => createEmptyForm());
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [countries, setCountries] = useState<CatalogCountryItem[]>([]);
  const [currencies, setCurrencies] = useState<CatalogCurrencyItem[]>([]);

  useEffect(() => {
    if (!open) return;
    catalogService.getTenantCountries().then((res) => setCountries(res.countries)).catch(() => {});
    catalogService.getTenantCurrencies().then((res) => setCurrencies(res.currencies)).catch(() => {});
  }, [open]);

  const isValid = useMemo(() => {
    if (!formData.name.trim()) return false;
    if (!formData.accountType) return false;
    if (!formData.currency) return false;
    return true;
  }, [formData.name, formData.accountType, formData.currency]);

  const resetForm = () => {
    setFormData(createEmptyForm());
    setError(null);
  };

  const handleClose = (nextOpen: boolean) => {
    if (!nextOpen) {
      resetForm();
    }
    onOpenChange(nextOpen);
  };

  const handleSave = async () => {
    if (!isValid || saving) return;
    setSaving(true);
    setError(null);
    try {
      const payload: CreateAccountRequest = {
        ...formData,
        currency: formData.currency.trim().toUpperCase(),
        country: formData.country?.trim().toUpperCase() || null,
        institutionName: formData.institutionName?.trim() || null,
        last4: formData.last4?.trim() || null,
        notes: formData.notes?.trim() || null,
      };
      await accountService.createAccount(payload);
      toast.success('Account created successfully.');
      onSuccess();
      handleClose(false);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to create account';
      const userMessage = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(userMessage || message);
      toast.error(userMessage || message);
    } finally {
      setSaving(false);
    }
  };

  const updateField = <K extends keyof CreateAccountRequest>(
    field: K,
    value: CreateAccountRequest[K]
  ) => {
    setFormData((prev) => ({ ...prev, [field]: value }));
  };

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-[500px] max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Add Account</DialogTitle>
          <DialogDescription>
            Manually create an account (not linked via a provider).
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="grid gap-2">
            <label className="text-sm font-medium text-[var(--color-text-primary)]">
              Name <span className="text-[var(--color-error)]">*</span>
            </label>
            <input
              type="text"
              value={formData.name}
              onChange={(e) => updateField('name', e.target.value)}
              className={fieldClassName}
              placeholder="e.g., My Savings Account"
            />
          </div>

          <div className="grid gap-2">
            <label className="text-sm font-medium text-[var(--color-text-primary)]">
              Account Type <span className="text-[var(--color-error)]">*</span>
            </label>
            <Select
              value={formData.accountType}
              onValueChange={(value) => updateField('accountType', value)}
            >
              <SelectTrigger>
                <SelectValue placeholder="Select account type" />
              </SelectTrigger>
              <SelectContent>
                {accountTypes.map((type) => (
                  <SelectItem key={type} value={type}>
                    {type}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="grid gap-2">
              <label className="text-sm font-medium text-[var(--color-text-primary)]">
                Currency <span className="text-[var(--color-error)]">*</span>
              </label>
              <Select
                value={formData.currency}
                onValueChange={(value) => updateField('currency', value)}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select currency" />
                </SelectTrigger>
                <SelectContent>
                  {currencies.map((c) => (
                    <SelectItem key={c.code} value={c.code}>
                      {c.code} - {c.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="grid gap-2">
              <label className="text-sm font-medium text-[var(--color-text-primary)]">
                Country
              </label>
              <Select
                value={formData.country || ''}
                onValueChange={(value) => updateField('country', value === '__none__' ? null : value)}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select country" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__none__">None</SelectItem>
                  {countries.map((c) => (
                    <SelectItem key={c.countryCode} value={c.countryCode}>
                      {c.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid gap-2">
            <label className="text-sm font-medium text-[var(--color-text-primary)]">
              Institution Name
            </label>
            <input
              type="text"
              value={formData.institutionName || ''}
              onChange={(e) => updateField('institutionName', e.target.value || null)}
              className={fieldClassName}
              placeholder="e.g., Chase, Wells Fargo"
            />
          </div>

          <div className="grid gap-2">
            <label className="text-sm font-medium text-[var(--color-text-primary)]">
              Last 4 Digits
            </label>
            <input
              type="text"
              value={formData.last4 || ''}
              onChange={(e) => updateField('last4', e.target.value.replace(/\D/g, '').slice(0, 4) || null)}
              className={fieldClassName}
              placeholder="e.g., 1234"
              maxLength={4}
            />
          </div>

          <div className="grid gap-2">
            <label className="text-sm font-medium text-[var(--color-text-primary)]">
              Notes
            </label>
            <Textarea
              value={formData.notes || ''}
              onChange={(e) => updateField('notes', e.target.value || null)}
              placeholder="Optional notes about this account"
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
            {saving ? 'Creating...' : 'Create Account'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
