import { useEffect, useState } from 'react';
import { agentConfigService } from '@/services/aiService';
import type { AgentConfigurationResponse } from '@/types/ai';
import { Label } from '@/components/ui/label';
import { toast } from 'sonner';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

interface AgentPickerProps {
  value: string | null;
  onChange: (agentName: string | null, config?: AgentConfigurationResponse) => void;
  compact?: boolean;
}

export function AgentPicker({ value, onChange, compact = false }: AgentPickerProps) {
  const [agents, setAgents] = useState<AgentConfigurationResponse[]>([]);

  useEffect(() => {
    agentConfigService.list().then(setAgents).catch(console.error);
  }, []);

  const handleChange = async (name: string) => {
    if (name === '__raw__') {
      onChange(null);
      return;
    }
    try {
      const config = await agentConfigService.get(name);
      onChange(name, config);
    } catch {
      // Config fetch failed — fall back to list data so the agent stays selected
      const fromList = agents.find((a) => a.name === name);
      if (fromList) {
        onChange(name, fromList);
        toast.warning('Could not load full agent config — using cached data');
      } else {
        toast.error('Failed to load agent configuration');
      }
    }
  };

  const selectEl = (
    <Select value={value ?? '__raw__'} onValueChange={handleChange}>
      <SelectTrigger className={compact ? 'h-8 w-48' : 'h-9'}>
        <SelectValue placeholder="Select an agent..." />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="__raw__">Raw mode (no agent)</SelectItem>
        {agents
          .filter((a) => a.isActive)
          .map((a) => (
            <SelectItem key={a.name} value={a.name}>
              {a.name}
            </SelectItem>
          ))}
      </SelectContent>
    </Select>
  );

  if (compact) return selectEl;

  return (
    <div className="space-y-1.5">
      <Label className="text-xs">Agent</Label>
      {selectEl}
    </div>
  );
}
