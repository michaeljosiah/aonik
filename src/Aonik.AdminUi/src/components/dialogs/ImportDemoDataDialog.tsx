import { useState } from 'react';
import { ArrowLeftRight, Receipt, TriangleAlert } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import type { DemoSeedType } from '@/types';

interface ImportDemoDataDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onImport: (seedType: DemoSeedType) => Promise<void>;
  saving: boolean;
  error: string | null;
}

const demoOptions: Array<{
  seedType: DemoSeedType;
  title: string;
  description: string;
  icon: typeof ArrowLeftRight;
  gradientClass: string;
}> = [
  {
    seedType: 'CrossBorderPayments',
    title: 'Cross-border Payments',
    description:
      'Seeds multi-country corridors, partner routing, FX quotes, households, and richer customer relationships.',
    icon: ArrowLeftRight,
    gradientClass: 'from-[#0f766e] to-[#115e59]',
  },
  {
    seedType: 'BillCollection',
    title: 'Bill Collection',
    description:
      'Seeds a focused bill payment corridor with utilities catalog, payer/receiver parties, and pricing defaults.',
    icon: Receipt,
    gradientClass: 'from-[#1e3a8a] to-[#1d4ed8]',
  },
];

export function ImportDemoDataDialog({
  open,
  onOpenChange,
  onImport,
  saving,
  error,
}: ImportDemoDataDialogProps) {
  const [selectedType, setSelectedType] = useState<DemoSeedType>('BillCollection');
  const [step, setStep] = useState<'selection' | 'confirm'>('selection');

  const selectedOption = demoOptions.find((option) => option.seedType === selectedType) ?? demoOptions[0];

  const handleOpenChange = (nextOpen: boolean) => {
    if (!nextOpen) {
      setSelectedType('BillCollection');
      setStep('selection');
    }
    onOpenChange(nextOpen);
  };

  const handleImport = async () => {
    if (saving) return;
    await onImport(selectedType);
  };

  const handleProceedToConfirm = () => {
    if (saving) return;
    setStep('confirm');
  };

  const handleBackToSelection = () => {
    setStep('selection');
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-[700px] max-h-[90vh] overflow-y-auto p-0 gap-0">
        <div className="p-6 space-y-6">
          {step === 'selection' ? (
            <>
              <DialogHeader>
                <DialogTitle>Import Demo Data</DialogTitle>
                <DialogDescription>
                  Choose the demo dataset to import for the selected tenant.
                </DialogDescription>
              </DialogHeader>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {demoOptions.map((option) => {
                  const Icon = option.icon;
                  const selected = selectedType === option.seedType;

                  return (
                    <Card
                      key={option.seedType}
                      className={`cursor-pointer overflow-hidden transition-all group ${
                        selected
                          ? 'border-[var(--color-brand-primary)] shadow-md'
                          : 'hover:shadow-lg hover:border-[var(--color-brand-primary)]'
                      }`}
                      onClick={() => setSelectedType(option.seedType)}
                    >
                      <div className={`h-28 bg-gradient-to-br ${option.gradientClass} flex items-center justify-center`}>
                        <Icon className="w-14 h-14 text-white" />
                      </div>
                      <div className="p-5 space-y-2">
                        <h3 className="text-lg font-semibold text-[var(--color-text-primary)] group-hover:text-[var(--color-brand-primary)] transition-colors">
                          {option.title}
                        </h3>
                        <p className="text-sm text-[var(--color-text-secondary)]">{option.description}</p>
                      </div>
                    </Card>
                  );
                })}
              </div>
            </>
          ) : (
            <>
              <DialogHeader>
                <DialogTitle>Confirm Demo Import</DialogTitle>
                <DialogDescription>
                  This action will upsert demo records for the selected tenant and may overwrite demo defaults.
                </DialogDescription>
              </DialogHeader>

              <div className="rounded-md border border-[var(--color-warning)] bg-[var(--color-warning-light)] px-4 py-3 text-sm text-[var(--color-warning)] flex items-start gap-3">
                <TriangleAlert className="w-4 h-4 mt-0.5" />
                <span>Proceed only if this tenant is intended for demo or sandbox workflows.</span>
              </div>

              <Card className="p-4 space-y-1">
                <p className="text-xs font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)]">Selected Dataset</p>
                <p className="text-base font-semibold text-[var(--color-text-primary)]">{selectedOption.title}</p>
                <p className="text-sm text-[var(--color-text-secondary)]">{selectedOption.description}</p>
              </Card>
            </>
          )}

          {error && (
            <div className="rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-4 py-3 text-sm text-[var(--color-error)]">
              {error}
            </div>
          )}

          <DialogFooter>
            <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
              Cancel
            </Button>
            {step === 'selection' ? (
              <Button onClick={handleProceedToConfirm} disabled={saving}>
                Continue
              </Button>
            ) : (
              <>
                <Button variant="outline" onClick={handleBackToSelection} disabled={saving}>
                  Back
                </Button>
                <Button onClick={handleImport} disabled={saving}>
                  {saving ? 'Importing...' : 'Confirm Import'}
                </Button>
              </>
            )}
          </DialogFooter>
        </div>
      </DialogContent>
    </Dialog>
  );
}
