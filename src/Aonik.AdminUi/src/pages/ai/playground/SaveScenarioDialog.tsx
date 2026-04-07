import { useState } from 'react';
import { Loader2, Save, X } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { playgroundScenarioService } from '@/services/aiService';
import type { CreatePlaygroundScenarioRequest } from '@/types/ai';
import { toast } from 'sonner';

interface SaveScenarioDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Current playground state to save */
  scenarioData: Omit<CreatePlaygroundScenarioRequest, 'name' | 'description' | 'tags'>;
  onSaved?: () => void;
}

export function SaveScenarioDialog({
  open,
  onOpenChange,
  scenarioData,
  onSaved,
}: SaveScenarioDialogProps) {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [tagsInput, setTagsInput] = useState('');
  const [saving, setSaving] = useState(false);

  const handleSave = async () => {
    if (!name.trim()) {
      toast.error('Scenario name is required');
      return;
    }

    setSaving(true);
    try {
      const tags = tagsInput
        .split(',')
        .map((t) => t.trim().toLowerCase())
        .filter(Boolean);

      const request: CreatePlaygroundScenarioRequest = {
        ...scenarioData,
        name: name.trim(),
        description: description.trim() || null,
        tags: tags.length > 0 ? tags : undefined,
      };

      await playgroundScenarioService.create(request);
      toast.success(`Scenario "${name}" saved`);
      onOpenChange(false);
      // Reset form
      setName('');
      setDescription('');
      setTagsInput('');
      onSaved?.();
    } catch (err) {
      console.error('Failed to save scenario:', err);
      toast.error('Failed to save scenario');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Save className="h-4 w-4" />
            Save as Scenario
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="scenario-name" className="text-xs">
              Name <span className="text-red-500">*</span>
            </Label>
            <Input
              id="scenario-name"
              placeholder="e.g. Balance inquiry — happy path"
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="h-8 text-sm"
              autoFocus
            />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="scenario-description" className="text-xs">
              Description
            </Label>
            <Textarea
              id="scenario-description"
              placeholder="What does this scenario test?"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={2}
              className="text-sm"
            />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="scenario-tags" className="text-xs">
              Tags <span className="text-[var(--color-text-tertiary)]">(comma separated)</span>
            </Label>
            <Input
              id="scenario-tags"
              placeholder="e.g. happy-path, billing, multi-turn"
              value={tagsInput}
              onChange={(e) => setTagsInput(e.target.value)}
              className="h-8 text-sm"
            />
          </div>

          <div className="rounded-md border border-[var(--color-border-light)] bg-[var(--color-background)] p-3">
            <p className="text-xs text-[var(--color-text-secondary)]">
              This will save the current conversation ({scenarioData.turns.length} turn
              {scenarioData.turns.length !== 1 ? 's' : ''}), system prompt, and configuration
              as a reusable scenario.
            </p>
          </div>
        </div>

        <DialogFooter>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onOpenChange(false)}
            disabled={saving}
          >
            <X className="mr-1.5 h-3.5 w-3.5" />
            Cancel
          </Button>
          <Button
            size="sm"
            onClick={handleSave}
            disabled={saving || !name.trim()}
          >
            {saving ? (
              <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
            ) : (
              <Save className="mr-1.5 h-3.5 w-3.5" />
            )}
            Save Scenario
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
