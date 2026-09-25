import { useState, useEffect, useId, useMemo } from 'react';
import { Clock, Pencil, Check, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Field, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
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
      <span className="text-xs text-muted-foreground" title={cron ?? undefined}>
        <Clock className="inline w-3 h-3 mr-1 -mt-0.5 text-muted-foreground" />
        {description}
      </span>
    );
  }

  return (
    <div className="flex items-center gap-2">
      <div>
        <div className="text-sm text-foreground">{description}</div>
        <div className="font-mono text-xs text-muted-foreground mt-0.5">{cron ?? '--'}</div>
      </div>
      {onSave && (
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              size="icon-sm"
              variant="ghost"
              aria-label="Edit schedule"
              onClick={() => setEditing(true)}
            >
              <Pencil className="w-3 h-3" />
            </Button>
          </TooltipTrigger>
          <TooltipContent>Edit schedule</TooltipContent>
        </Tooltip>
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

const fieldLabelClass = 'text-xs text-muted-foreground';

function CronScheduleEditor({ initialCron, onSave, onCancel }: CronScheduleEditorProps) {
  const [preset, setPreset] = useState<CronPreset>(() => parseCron(initialCron));
  const generatedCron = useMemo(() => buildCron(preset), [preset]);
  const previewDescription = useMemo(() => describeCron(generatedCron), [generatedCron]);
  const id = useId();

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
    <div className="rounded-lg border bg-card p-4 space-y-4">
      {/* Frequency selector */}
      <Field className="gap-1.5">
        <FieldLabel id={`${id}-frequency`} className={fieldLabelClass}>Frequency</FieldLabel>
        <ToggleGroup
          type="single"
          variant="outline"
          size="sm"
          aria-labelledby={`${id}-frequency`}
          value={preset.frequency}
          // Radix emits an empty value when the active item is clicked again;
          // keep the current frequency in that case.
          onValueChange={(v) => {
            if (v) updatePreset({ frequency: v as CronFrequency });
          }}
          className="flex-wrap gap-1 shadow-none"
        >
          {Object.entries(FREQUENCY_LABELS).map(([key, label]) => (
            <ToggleGroupItem
              key={key}
              value={key}
              className="flex-none rounded-md px-2.5 text-xs data-[variant=outline]:border-l"
            >
              {label}
            </ToggleGroupItem>
          ))}
        </ToggleGroup>
      </Field>

      {/* Interval (every N minutes) */}
      {preset.frequency === 'every-n-minutes' && (
        <Field className="gap-1.5 sm:max-w-40">
          <FieldLabel htmlFor={`${id}-interval`} className={fieldLabelClass}>Every (minutes)</FieldLabel>
          <Select
            value={String(preset.interval ?? 5)}
            onValueChange={(v) => updatePreset({ interval: parseInt(v, 10) })}
          >
            <SelectTrigger id={`${id}-interval`}>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {[1, 2, 3, 5, 10, 15, 20, 30].map(n => (
                <SelectItem key={n} value={String(n)}>{n} min</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>
      )}

      {/* Minute (for hourly) */}
      {preset.frequency === 'hourly' && (
        <Field className="gap-1.5 sm:max-w-40">
          <FieldLabel htmlFor={`${id}-at-minute`} className={fieldLabelClass}>At minute</FieldLabel>
          <Select
            value={String(preset.minute ?? 0)}
            onValueChange={(v) => updatePreset({ minute: parseInt(v, 10) })}
          >
            <SelectTrigger id={`${id}-at-minute`} className="font-mono tabular-nums">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {[0, 5, 10, 15, 20, 30, 45].map(n => (
                <SelectItem key={n} value={String(n)}>:{padTime(n)}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>
      )}

      {/* Time (for daily, weekly, monthly) */}
      {(preset.frequency === 'daily' || preset.frequency === 'weekly' || preset.frequency === 'monthly') && (
        <div className="grid grid-cols-2 gap-3 sm:max-w-64">
          <Field className="gap-1.5">
            <FieldLabel htmlFor={`${id}-hour`} className={fieldLabelClass}>Hour</FieldLabel>
            <Select
              value={String(preset.hour ?? 0)}
              onValueChange={(v) => updatePreset({ hour: parseInt(v, 10) })}
            >
              <SelectTrigger id={`${id}-hour`}>
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
          </Field>
          <Field className="gap-1.5">
            <FieldLabel htmlFor={`${id}-minute`} className={fieldLabelClass}>Minute</FieldLabel>
            <Select
              value={String(preset.minute ?? 0)}
              onValueChange={(v) => updatePreset({ minute: parseInt(v, 10) })}
            >
              <SelectTrigger id={`${id}-minute`} className="font-mono tabular-nums">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {[0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55].map(n => (
                  <SelectItem key={n} value={String(n)}>:{padTime(n)}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>
        </div>
      )}

      {/* Day of week (for weekly) */}
      {preset.frequency === 'weekly' && (
        <Field className="gap-1.5 sm:max-w-48">
          <FieldLabel htmlFor={`${id}-dow`} className={fieldLabelClass}>Day of week</FieldLabel>
          <Select
            value={String(preset.dayOfWeek ?? 1)}
            onValueChange={(v) => updatePreset({ dayOfWeek: parseInt(v, 10) })}
          >
            <SelectTrigger id={`${id}-dow`}>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {DAYS_OF_WEEK.map(d => (
                <SelectItem key={d.value} value={d.value}>{d.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>
      )}

      {/* Day of month (for monthly) */}
      {preset.frequency === 'monthly' && (
        <Field className="gap-1.5 sm:max-w-32">
          <FieldLabel htmlFor={`${id}-dom`} className={fieldLabelClass}>Day of month</FieldLabel>
          <Select
            value={String(preset.dayOfMonth ?? 1)}
            onValueChange={(v) => updatePreset({ dayOfMonth: parseInt(v, 10) })}
          >
            <SelectTrigger id={`${id}-dom`} className="font-mono tabular-nums">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {Array.from({ length: 28 }, (_, i) => (
                <SelectItem key={i + 1} value={String(i + 1)}>{i + 1}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>
      )}

      {/* Raw input (for custom) */}
      {preset.frequency === 'custom' && (
        <Field className="gap-1.5">
          <FieldLabel htmlFor={`${id}-raw`} className={fieldLabelClass}>Cron expression (Quartz 6-field)</FieldLabel>
          <Input
            id={`${id}-raw`}
            value={preset.raw ?? ''}
            onChange={(e) => updatePreset({ raw: e.target.value })}
            placeholder="0 0/30 * * * ?"
            className="font-mono"
          />
        </Field>
      )}

      {/* Preview */}
      <div className="rounded-md bg-muted px-3 py-2">
        <div className="text-xs text-muted-foreground mb-0.5">Preview</div>
        <div className="text-sm text-foreground">{previewDescription}</div>
        <div className="font-mono text-[11px] text-muted-foreground mt-0.5">{generatedCron}</div>
      </div>

      {/* Actions */}
      <div className="flex items-center gap-2">
        <Button size="sm" onClick={() => onSave(generatedCron)}>
          <Check />
          Apply
        </Button>
        <Button size="sm" variant="ghost" onClick={onCancel}>
          <X />
          Cancel
        </Button>
      </div>
    </div>
  );
}
