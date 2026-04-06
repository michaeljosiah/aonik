import { useEffect, useState } from 'react';
import { aiTaskService, type AiTaskResponse } from '@/services/aiService';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

interface AiTaskPickerProps {
  value: string | null;
  onChange: (taskId: string | null, task?: AiTaskResponse) => void;
  compact?: boolean;
}

export function AiTaskPicker({ value, onChange, compact = false }: AiTaskPickerProps) {
  const [tasks, setTasks] = useState<AiTaskResponse[]>([]);

  useEffect(() => {
    aiTaskService.list().then(setTasks).catch(console.error);
  }, []);

  const handleChange = (id: string) => {
    if (id === '__none__') {
      onChange(null);
      return;
    }
    const task = tasks.find((t) => t.id === id);
    onChange(id, task ?? undefined);
  };

  // Group tasks by category for better organization
  const publishedTasks = tasks.filter((t) => t.isPublished);
  const categories = [...new Set(publishedTasks.map((t) => t.category))].sort();

  const selectEl = (
    <Select value={value ?? '__none__'} onValueChange={handleChange}>
      <SelectTrigger className={compact ? 'h-8 min-w-[220px] max-w-[280px]' : 'h-9'}>
        <SelectValue placeholder="Select an AI task..." />
      </SelectTrigger>
      <SelectContent className="max-h-[300px]">
        <SelectItem value="__none__">No task selected</SelectItem>
        {categories.map((cat) => {
          const catTasks = publishedTasks.filter((t) => t.category === cat);
          return (
            <SelectGroup key={cat}>
              <SelectLabel className="text-[10px] uppercase tracking-wider text-[var(--color-text-tertiary)]">
                {cat}
              </SelectLabel>
              {catTasks.map((t) => (
                <SelectItem key={t.id} value={t.id}>
                  {t.displayName}
                </SelectItem>
              ))}
            </SelectGroup>
          );
        })}
      </SelectContent>
    </Select>
  );

  if (compact) return selectEl;

  return (
    <div className="space-y-1.5">
      <Label className="text-xs">AI Task</Label>
      {selectEl}
    </div>
  );
}
