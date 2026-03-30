import { useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { fxRateService } from '@/services/fxRateService';
import type { CreateFxQuoteRequest, UpdateFxQuoteRequest, FxQuoteDetailResponse } from '@/types';

interface FxQuoteDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  quote?: FxQuoteDetailResponse;
  onSuccess: () => void;
}

export function FxQuoteDialog({ open, onOpenChange, quote, onSuccess }: FxQuoteDialogProps) {
  const isEdit = !!quote;
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [formData, setFormData] = useState({
    baseCurrency: quote?.baseCurrency || '',
    targetCurrency: quote?.targetCurrency || '',
    rate: quote?.rate.toString() || '',
    expiresAt: quote?.expiresAt ? new Date(quote.expiresAt).toISOString().slice(0, 16) : '',
    provider: quote?.provider || '',
    metadataJson: quote?.metadataJson || '',
  });

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const expiresAt = new Date(formData.expiresAt).toISOString();

      if (isEdit && quote) {
        const request: UpdateFxQuoteRequest = {
          rate: parseFloat(formData.rate),
          expiresAt,
          provider: formData.provider || null,
          metadataJson: formData.metadataJson || null,
        };
        await fxRateService.update(quote.id, request);
      } else {
        const request: CreateFxQuoteRequest = {
          baseCurrency: formData.baseCurrency,
          targetCurrency: formData.targetCurrency,
          rate: parseFloat(formData.rate),
          expiresAt,
          provider: formData.provider || null,
          metadataJson: formData.metadataJson || null,
        };
        await fxRateService.create(request);
      }

      onSuccess();
      onOpenChange(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save FX quote');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-[500px]">
        <form onSubmit={handleSubmit}>
          <DialogHeader>
            <DialogTitle>{isEdit ? 'Edit FX Quote' : 'Create FX Quote'}</DialogTitle>
            <DialogDescription>
              {isEdit ? 'Update the exchange rate quote details.' : 'Add a new exchange rate quote to the system.'}
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-4">
            {error && (
              <div className="p-3 rounded-lg bg-[color-mix(in_srgb,var(--color-danger)_8%,transparent)] border border-[color-mix(in_srgb,var(--color-danger)_25%,transparent)] text-[var(--color-danger)] text-sm">{error}</div>
            )}

            <div className="grid grid-cols-2 gap-4">
              <div className="grid gap-2">
                <Label htmlFor="baseCurrency">Base Currency</Label>
                <Input
                  id="baseCurrency"
                  placeholder="USD"
                  value={formData.baseCurrency}
                  onChange={(e) => setFormData({ ...formData, baseCurrency: e.target.value.toUpperCase() })}
                  disabled={isEdit}
                  required
                  maxLength={3}
                />
              </div>

              <div className="grid gap-2">
                <Label htmlFor="targetCurrency">Target Currency</Label>
                <Input
                  id="targetCurrency"
                  placeholder="NGN"
                  value={formData.targetCurrency}
                  onChange={(e) => setFormData({ ...formData, targetCurrency: e.target.value.toUpperCase() })}
                  disabled={isEdit}
                  required
                  maxLength={3}
                />
              </div>
            </div>

            <div className="grid gap-2">
              <Label htmlFor="rate">Exchange Rate</Label>
              <Input
                id="rate"
                type="number"
                step="0.000001"
                placeholder="1500.000000"
                value={formData.rate}
                onChange={(e) => setFormData({ ...formData, rate: e.target.value })}
                required
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="expiresAt">Expires At</Label>
              <Input
                id="expiresAt"
                type="datetime-local"
                value={formData.expiresAt}
                onChange={(e) => setFormData({ ...formData, expiresAt: e.target.value })}
                required
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="provider">Provider (Optional)</Label>
              <Input
                id="provider"
                placeholder="Provider A"
                value={formData.provider}
                onChange={(e) => setFormData({ ...formData, provider: e.target.value })}
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="metadataJson">Metadata JSON (Optional)</Label>
              <textarea
                id="metadataJson"
                className="flex min-h-[80px] w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                placeholder='{"source": "manual"}'
                value={formData.metadataJson}
                onChange={(e) => setFormData({ ...formData, metadataJson: e.target.value })}
              />
            </div>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={loading}>
              Cancel
            </Button>
            <Button type="submit" disabled={loading}>
              {loading ? 'Saving...' : isEdit ? 'Update Quote' : 'Create Quote'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
