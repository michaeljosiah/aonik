import { useState } from 'react';
import { agentConfigService } from '@/services/aiService';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { toast } from 'sonner';
import { RotateCcw, Save } from 'lucide-react';

interface SystemPromptEditorProps {
  value: string;
  onChange: (prompt: string) => void;
  agentName: string | null;
  defaultPrompt: string | null;
}

export function SystemPromptEditor({
  value,
  onChange,
  agentName,
  defaultPrompt,
}: SystemPromptEditorProps) {
  const [saving, setSaving] = useState(false);
  const hasChanges = defaultPrompt !== null && value !== defaultPrompt;

  const handleReset = () => {
    if (defaultPrompt !== null) {
      onChange(defaultPrompt);
    }
  };

  const handleSave = async () => {
    if (!agentName) return;
    setSaving(true);
    try {
      await agentConfigService.upsert(agentName, { instructionsText: value });
      toast.success('Saved prompt to agent config');
    } catch (err) {
      toast.error(`Save failed: ${(err as Error).message}`);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="flex flex-1 flex-col space-y-1.5">
      <div className="flex items-center justify-between">
        <Label className="text-xs">System Prompt</Label>
        <div className="flex gap-1">
          {hasChanges && defaultPrompt !== null && (
            <Button variant="ghost" size="sm" onClick={handleReset} className="h-6 px-2 text-xs">
              <RotateCcw className="mr-1 h-3 w-3" />
              Reset
            </Button>
          )}
          {agentName && hasChanges && (
            <Button size="sm" onClick={handleSave} disabled={saving} className="h-6 px-2 text-xs">
              <Save className="mr-1 h-3 w-3" />
              {saving ? 'Saving...' : 'Save'}
            </Button>
          )}
        </div>
      </div>
      <Textarea
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder="Enter system prompt..."
        rows={8}
        className="min-h-[120px] flex-1 resize-y font-mono text-xs leading-relaxed"
      />
      <p className="text-xs text-[var(--color-text-tertiary)]">
        ~{Math.round(value.length / 4)} tokens
      </p>
    </div>
  );
}
