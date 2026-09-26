import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';

interface ToolToggleListProps {
  allTools: string[];
  enabledTools: string[];
  onChange: (tools: string[]) => void;
}

export function ToolToggleList({ allTools, enabledTools, onChange }: ToolToggleListProps) {
  if (allTools.length === 0) {
    return (
      <div className="space-y-1.5">
        <Label className="text-xs">Tools</Label>
        <p className="text-xs italic text-muted-foreground">
          No tools (raw mode)
        </p>
      </div>
    );
  }

  const allEnabled = enabledTools.length === allTools.length;

  const toggleTool = (name: string) => {
    if (enabledTools.includes(name)) {
      onChange(enabledTools.filter((t) => t !== name));
    } else {
      onChange([...enabledTools, name]);
    }
  };

  const toggleAll = () => {
    onChange(allEnabled ? [] : [...allTools]);
  };

  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between">
        <Label className="text-xs">
          Tools ({enabledTools.length}/{allTools.length})
        </Label>
        <button
          onClick={toggleAll}
          className="text-xs text-primary hover:underline"
        >
          {allEnabled ? 'None' : 'All'}
        </button>
      </div>
      <div className="max-h-40 space-y-0.5 overflow-y-auto rounded-md border border-border bg-card p-2">
        {allTools.map((name) => {
          const enabled = enabledTools.includes(name);
          return (
            <label
              key={name}
              className="flex cursor-pointer items-center gap-2 rounded-md px-1.5 py-1 text-xs hover:bg-accent"
            >
              <Checkbox
                checked={enabled}
                onCheckedChange={() => toggleTool(name)}
              />
              <span className={enabled ? 'text-foreground' : 'text-muted-foreground'}>
                {name}
              </span>
            </label>
          );
        })}
      </div>
    </div>
  );
}
