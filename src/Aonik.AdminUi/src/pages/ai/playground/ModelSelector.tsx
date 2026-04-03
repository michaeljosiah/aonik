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

  // Group by provider
  const grouped = models
    .filter((m) => m.isActive)
    .reduce<Record<string, AiModelResponse[]>>((acc, m) => {
      const provider = m.providerName ?? 'Unknown';
      if (!acc[provider]) acc[provider] = [];
      acc[provider].push(m);
      return acc;
    }, {});

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
              <SelectItem key={m.id} value={m.id}>
                {m.modelName}
              </SelectItem>
            ))}
          </SelectGroup>
        ))}
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
