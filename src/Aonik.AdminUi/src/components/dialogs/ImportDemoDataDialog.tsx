import { useState } from 'react';
import { AlertCircle, ArrowLeftRight, Receipt, TriangleAlert } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Alert, AlertDescription } from '@/components/ui/alert';
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
  iconClass: string;
}> = [
  {
    seedType: 'CrossBorderPayments',
    title: 'Cross-border payments',
    description:
      'Seeds multi-country corridors, partner routing, FX quotes, households, and richer customer relationships.',
    icon: ArrowLeftRight,
    gradientClass: 'from-primary to-[color-mix(in_oklab,var(--primary)_78%,black)]',
    iconClass: 'text-primary-foreground',
  },
  {
    seedType: 'BillCollection',
    title: 'Bill collection',
    description:
      'Seeds a focused bill payment corridor with utilities catalog, payer/receiver parties, and pricing defaults.',
    icon: Receipt,
    gradientClass: 'from-info-foreground to-info',
    iconClass: 'text-background',
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
      <DialogContent className="max-w-[700px] max-h-[90vh] overflow-y-auto p-0 gap-0">
        <div className="p-6 space-y-6">
          {step === 'selection' ? (
            <>
              <DialogHeader>
                <DialogTitle>Import demo data</DialogTitle>
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
                          ? 'border-primary shadow-md'
                          : 'hover:shadow-lg hover:border-primary'
                      }`}
                      onClick={() => setSelectedType(option.seedType)}
                    >
                      <div className={`h-28 bg-gradient-to-br ${option.gradientClass} flex items-center justify-center`}>
                        <Icon className={`w-14 h-14 ${option.iconClass}`} />
                      </div>
                      <div className="p-5 space-y-2">
                        <h3 className="text-lg font-semibold text-foreground group-hover:text-primary transition-colors">
                          {option.title}
                        </h3>
                        <p className="text-sm text-muted-foreground">{option.description}</p>
                      </div>
                    </Card>
                  );
                })}
              </div>
            </>
          ) : (
            <>
              <DialogHeader>
                <DialogTitle>Confirm demo import</DialogTitle>
                <DialogDescription>
                  This action will upsert demo records for the selected tenant and may overwrite demo defaults.
                </DialogDescription>
              </DialogHeader>

              <Alert variant="warning">
                <TriangleAlert />
                <AlertDescription>
                  Proceed only if this tenant is intended for demo or sandbox workflows.
                </AlertDescription>
              </Alert>

              <Card className="p-4 space-y-1">
                <p className="text-xs font-medium text-muted-foreground">Selected dataset</p>
                <p className="text-base font-semibold text-foreground">{selectedOption.title}</p>
                <p className="text-sm text-muted-foreground">{selectedOption.description}</p>
              </Card>
            </>
          )}

          {error && (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
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
                  {saving ? 'Importing...' : 'Confirm import'}
                </Button>
              </>
            )}
          </DialogFooter>
        </div>
      </DialogContent>
    </Dialog>
  );
}
