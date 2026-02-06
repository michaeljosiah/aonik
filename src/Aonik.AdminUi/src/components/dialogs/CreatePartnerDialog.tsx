import { useMemo, useState } from 'react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import type { CreatePartnerRequest } from '@/types/partners';

interface CreatePartnerDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSave: (request: CreatePartnerRequest) => Promise<void>;
}

const defaultStatus = 'Active';

const statusOptions = [
  { value: 'Active', label: 'Active' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Suspended', label: 'Suspended' },
  { value: 'Inactive', label: 'Inactive' },
];

const parseCapabilities = (value: string) => {
  return value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);
};

const buildOperatingHoursJson = (summary: string) => {
  const trimmed = summary.trim();
  if (!trimmed) {
    return JSON.stringify({});
  }

  return JSON.stringify({ summary: trimmed });
};

export function CreatePartnerDialog({ open, onOpenChange, onSave }: CreatePartnerDialogProps) {
  const [name, setName] = useState('');
  const [status, setStatus] = useState(defaultStatus);
  const [capabilities, setCapabilities] = useState('BillPay, Collections');
  const [operatingHoursSummary, setOperatingHoursSummary] = useState('Mon-Fri 08:00-18:00 local time');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canSave = useMemo(() => name.trim().length > 1, [name]);

  const reset = () => {
    setName('');
    setStatus(defaultStatus);
    setCapabilities('BillPay, Collections');
    setOperatingHoursSummary('Mon-Fri 08:00-18:00 local time');
    setSaving(false);
    setError(null);
  };

  const handleOpenChange = (nextOpen: boolean) => {
    if (!nextOpen) {
      reset();
    }
    onOpenChange(nextOpen);
  };

  const handleSave = async () => {
    if (!canSave || saving) {
      return;
    }

    const request: CreatePartnerRequest = {
      name: name.trim(),
      status,
      capabilitiesJson: JSON.stringify(parseCapabilities(capabilities)),
      operatingHoursJson: buildOperatingHoursJson(operatingHoursSummary),
    };

    setSaving(true);
    setError(null);

    try {
      await onSave(request);
      handleOpenChange(false);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to create partner.';
      setError(message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-[560px]">
        <DialogHeader>
          <DialogTitle>Add Partner</DialogTitle>
          <DialogDescription>
            Register a bill pay partner so operators can map billers and corridors.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="partner-name">Partner name</Label>
            <Input
              id="partner-name"
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder="e.g. UtilityPay Network"
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="partner-status">Status</Label>
            <Select value={status} onValueChange={setStatus}>
              <SelectTrigger aria-label="Partner status" id="partner-status">
                <SelectValue placeholder="Select status" />
              </SelectTrigger>
              <SelectContent>
                {statusOptions.map((option) => (
                  <SelectItem key={option.value} value={option.value}>
                    {option.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="partner-capabilities">Capabilities</Label>
            <Input
              id="partner-capabilities"
              value={capabilities}
              onChange={(event) => setCapabilities(event.target.value)}
              placeholder="BillPay, Collections"
            />
            <p className="text-xs text-[var(--color-text-tertiary)]">
              Enter comma-separated values used by routing and operations teams.
            </p>
          </div>

          <div className="space-y-2">
            <Label htmlFor="partner-hours">Operating hours</Label>
            <Textarea
              id="partner-hours"
              value={operatingHoursSummary}
              onChange={(event) => setOperatingHoursSummary(event.target.value)}
              rows={3}
              placeholder="Mon-Fri 08:00-18:00 local time"
            />
          </div>

          {error && (
            <div className="rounded-sm border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
              {error}
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => handleOpenChange(false)} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={handleSave} disabled={!canSave || saving}>
            {saving ? 'Saving...' : 'Create partner'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
