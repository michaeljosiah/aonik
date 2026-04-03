import { useEffect, useState } from 'react';
import { aiModelService } from '@/services/aiService';
import type { AiModelResponse } from '@/types/ai';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

interface ModelSelectorProps {
  value: string | null;
  onChange: (modelId: string | null, modelName?: string) => void;
  label?: string;
  compact?: boolean;
}

export function ModelSelector({
  value,
  onChange,
  label = 'Model',
  compact = false,
}: ModelSelectorProps) {
  const [models, setModels] = useState<AiModelResponse[]>([]);
  const [showInactive, setShowInactive] = useState(false);

  useEffect(() => {
    aiModelService.list().then(setModels).catch(console.error);
  }, []);

  const handleChange = (id: string) => {
    if (id === '__default__') {
      onChange(null);
      return;
    }
    const model = models.find((m) => m.id === id);
    onChange(id, model?.modelName);
  };

  // Group by provider — include inactive when toggled
  const filtered = showInactive ? models : models.filter((m) => m.isActive);
  const grouped = filtered.reduce<Record<string, AiModelResponse[]>>((acc, m) => {
    const provider = m.providerName ?? 'Unknown';
    if (!acc[provider]) acc[provider] = [];
    acc[provider].push(m);
    return acc;
  }, {});

  const inactiveCount = models.filter((m) => !m.isActive).length;

  const selectEl = (
    <Select value={value ?? '__default__'} onValueChange={handleChange}>
      <SelectTrigger className={compact ? 'h-8 w-56' : 'h-9'}>
        <SelectValue placeholder="Select model..." />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="__default__">Default model</SelectItem>

        {Object.entries(grouped).map(([provider, providerModels]) => (
          <SelectGroup key={provider}>
            <div className="px-2 py-1.5 text-[11px] font-semibold tracking-wider text-[var(--color-text-tertiary)]">
              {provider}
            </div>
            {providerModels.map((m) => (
              <SelectItem
                key={m.id}
                value={m.id}
                disabled={!m.isActive}
                className={!m.isActive ? 'opacity-50' : ''}
              >
                <span className="flex items-center gap-1.5">
                  {m.modelName}
                  {!m.isActive && (
                    <span className="rounded bg-[var(--color-background)] px-1 py-0.5 text-[9px] font-medium text-[var(--color-text-tertiary)]">
                      inactive
                    </span>
                  )}
                </span>
              </SelectItem>
            ))}
          </SelectGroup>
        ))}

        {/* Toggle for inactive models */}
        {inactiveCount > 0 && (
          <div className="border-t border-[var(--color-border-light)] px-2 py-1.5">
            <button
              type="button"
              onClick={(e) => {
                e.preventDefault();
                e.stopPropagation();
                setShowInactive((prev) => !prev);
              }}
              className="w-full text-left text-[11px] text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]"
            >
              {showInactive
                ? 'Hide inactive models'
                : `Show ${inactiveCount} inactive model${inactiveCount !== 1 ? 's' : ''}`}
            </button>
          </div>
        )}
      </SelectContent>
    </Select>
  );

  if (compact) return selectEl;

  return (
    <div className="space-y-1.5">
      <Label className="text-xs">{label}</Label>
      {selectEl}
    </div>
  );
}
