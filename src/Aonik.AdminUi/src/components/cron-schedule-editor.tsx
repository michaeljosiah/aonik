import { useState, useEffect, useMemo } from 'react';
import { Clock, Pencil, Check, X } from 'lucide-react';
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
import {
  describeCron,
  parseCron,
  buildCron,
  type CronPreset,
  type CronFrequency,
} from '@/lib/cronDescriber';

interface CronScheduleDisplayProps {
  cron: string | null | undefined;
  /** If provided, shows an edit button that opens the editor inline */
  onSave?: (newCron: string) => void;
  /** Compact mode for list cards (no edit) */
  compact?: boolean;
}

const FREQUENCY_LABELS: Record<CronFrequency, string> = {
  'every-minute': 'Every minute',
  'every-n-minutes': 'Every N minutes',
  'hourly': 'Hourly',
  'daily': 'Daily',
  'weekly': 'Weekly',
  'monthly': 'Monthly',
  'custom': 'Custom (raw cron)',
};

const DAYS_OF_WEEK = [
  { value: '0', label: 'Sunday' },
  { value: '1', label: 'Monday' },
  { value: '2', label: 'Tuesday' },
  { value: '3', label: 'Wednesday' },
  { value: '4', label: 'Thursday' },
  { value: '5', label: 'Friday' },
  { value: '6', label: 'Saturday' },
];

function padTime(n: number): string {
  return n.toString().padStart(2, '0');
}

/**
 * Displays a human-readable cron description with optional inline editing.
 */
export function CronScheduleDisplay({ cron, onSave, compact }: CronScheduleDisplayProps) {
  const [editing, setEditing] = useState(false);
  const description = useMemo(() => describeCron(cron), [cron]);

  if (editing && onSave && cron) {
    return (
      <CronScheduleEditor
        initialCron={cron}
        onSave={(newCron) => {
          onSave(newCron);
          setEditing(false);
        }}
        onCancel={() => setEditing(false)}
      />
    );
  }

  if (compact) {
    return (
      <span className="text-xs text-[var(--color-text-secondary)]" title={cron ?? undefined}>
        <Clock className="inline w-3 h-3 mr-1 -mt-0.5 text-[var(--color-text-tertiary)]" />
        {description}
      </span>
    );
  }

  return (
    <div className="flex items-center gap-2">
      <div>
        <div className="text-sm text-[var(--color-text-primary)]">{description}</div>
        <div className="font-mono text-xs text-[var(--color-text-tertiary)] mt-0.5">{cron ?? '--'}</div>
      </div>
      {onSave && (
        <Button
          size="sm"
          variant="ghost"
          className="h-7 px-2"
          onClick={() => setEditing(true)}
        >
          <Pencil className="w-3 h-3" />
        </Button>
      )}
    </div>
  );
}

// ── Editor ──────────────────────────────────────────────────────────

interface CronScheduleEditorProps {
  initialCron: string;
  onSave: (cron: string) => void;
  onCancel: () => void;
}

function CronScheduleEditor({ initialCron, onSave, onCancel }: CronScheduleEditorProps) {
  const [preset, setPreset] = useState<CronPreset>(() => parseCron(initialCron));
  const generatedCron = useMemo(() => buildCron(preset), [preset]);
  const previewDescription = useMemo(() => describeCron(generatedCron), [generatedCron]);

  // Keep raw in sync when switching to custom
  useEffect(() => {
    if (preset.frequency === 'custom' && !preset.raw) {
      setPreset(p => ({ ...p, raw: initialCron }));
    }
  }, [preset.frequency, preset.raw, initialCron]);

  const updatePreset = (patch: Partial<CronPreset>) => {
    setPreset(p => ({ ...p, ...patch }));
  };

  return (
    <div className="rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4 space-y-4">
      {/* Frequency selector */}
      <div className="space-y-1.5">
        <Label className="text-xs text-[var(--color-text-tertiary)]">Frequency</Label>
        <Select
          value={preset.frequency}
          onValueChange={(v) => updatePreset({ frequency: v as CronFrequency })}
        >
          <SelectTrigger className="h-9">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {Object.entries(FREQUENCY_LABELS).map(([key, label]) => (
              <SelectItem key={key} value={key}>{label}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Interval (every N minutes) */}
      {preset.frequency === 'every-n-minutes' && (
        <div className="space-y-1.5">
          <Label className="text-xs text-[var(--color-text-tertiary)]">Every (minutes)</Label>
          <Select
            value={String(preset.interval ?? 5)}
            onValueChange={(v) => updatePreset({ interval: parseInt(v, 10) })}
          >
            <SelectTrigger className="h-9 w-32">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {[1, 2, 3, 5, 10, 15, 20, 30].map(n => (
                <SelectItem key={n} value={String(n)}>{n} min</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      )}

      {/* Minute (for hourly) */}
      {preset.frequency === 'hourly' && (
        <div className="space-y-1.5">
          <Label className="text-xs text-[var(--color-text-tertiary)]">At minute</Label>
          <Select
            value={String(preset.minute ?? 0)}
            onValueChange={(v) => updatePreset({ minute: parseInt(v, 10) })}
          >
            <SelectTrigger className="h-9 w-32">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {[0, 5, 10, 15, 20, 30, 45].map(n => (
                <SelectItem key={n} value={String(n)}>:{padTime(n)}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      )}

      {/* Time (for daily, weekly, monthly) */}
      {(preset.frequency === 'daily' || preset.frequency === 'weekly' || preset.frequency === 'monthly') && (
        <div className="flex items-end gap-3">
          <div className="space-y-1.5">
            <Label className="text-xs text-[var(--color-text-tertiary)]">Hour</Label>
            <Select
              value={String(preset.hour ?? 0)}
              onValueChange={(v) => updatePreset({ hour: parseInt(v, 10) })}
            >
              <SelectTrigger className="h-9 w-24">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {Array.from({ length: 24 }, (_, i) => (
                  <SelectItem key={i} value={String(i)}>
                    {i === 0 ? '12 AM' : i < 12 ? `${i} AM` : i === 12 ? '12 PM' : `${i - 12} PM`}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label className="text-xs text-[var(--color-text-tertiary)]">Minute</Label>
            <Select
              value={String(preset.minute ?? 0)}
              onValueChange={(v) => updatePreset({ minute: parseInt(v, 10) })}
            >
              <SelectTrigger className="h-9 w-24">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {[0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55].map(n => (
                  <SelectItem key={n} value={String(n)}>:{padTime(n)}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>
      )}

      {/* Day of week (for weekly) */}
      {preset.frequency === 'weekly' && (
        <div className="space-y-1.5">
          <Label className="text-xs text-[var(--color-text-tertiary)]">Day of week</Label>
          <Select
            value={String(preset.dayOfWeek ?? 1)}
            onValueChange={(v) => updatePreset({ dayOfWeek: parseInt(v, 10) })}
          >
            <SelectTrigger className="h-9 w-40">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {DAYS_OF_WEEK.map(d => (
                <SelectItem key={d.value} value={d.value}>{d.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      )}

      {/* Day of month (for monthly) */}
      {preset.frequency === 'monthly' && (
        <div className="space-y-1.5">
          <Label className="text-xs text-[var(--color-text-tertiary)]">Day of month</Label>
          <Select
            value={String(preset.dayOfMonth ?? 1)}
            onValueChange={(v) => updatePreset({ dayOfMonth: parseInt(v, 10) })}
          >
            <SelectTrigger className="h-9 w-24">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {Array.from({ length: 28 }, (_, i) => (
                <SelectItem key={i + 1} value={String(i + 1)}>{i + 1}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      )}

      {/* Raw input (for custom) */}
      {preset.frequency === 'custom' && (
        <div className="space-y-1.5">
          <Label className="text-xs text-[var(--color-text-tertiary)]">Cron expression (Quartz 6-field)</Label>
          <Input
            value={preset.raw ?? ''}
            onChange={(e) => updatePreset({ raw: e.target.value })}
            placeholder="0 0/30 * * * ?"
            className="h-9 font-mono text-sm"
          />
        </div>
      )}

      {/* Preview */}
      <div className="rounded-sm bg-[var(--color-surface-inset)] px-3 py-2">
        <div className="text-xs text-[var(--color-text-tertiary)] mb-0.5">Preview</div>
        <div className="text-sm text-[var(--color-text-primary)]">{previewDescription}</div>
        <div className="font-mono text-[11px] text-[var(--color-text-tertiary)] mt-0.5">{generatedCron}</div>
      </div>

      {/* Actions */}
      <div className="flex items-center gap-2">
        <Button size="sm" onClick={() => onSave(generatedCron)} className="h-8">
          <Check className="w-3.5 h-3.5 mr-1" />
          Apply
        </Button>
        <Button size="sm" variant="ghost" onClick={onCancel} className="h-8">
          <X className="w-3.5 h-3.5 mr-1" />
          Cancel
        </Button>
      </div>
    </div>
  );
}
