import { useState } from 'react';
import { Loader2, Sparkles, Play, Save, X } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { playgroundScenarioService } from '@/services/aiService';
import type { PlaygroundScenarioResponse } from '@/types/ai';
import { toast } from 'sonner';

interface GenerateScenarioDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  agentName?: string | null;
  aiTaskId?: string | null;
  modelId?: string | null;
  onLoad: (scenario: PlaygroundScenarioResponse) => void;
  onSaved?: () => void;
}

export function GenerateScenarioDialog({
  open,
  onOpenChange,
  agentName,
  aiTaskId,
  modelId,
  onLoad,
  onSaved,
}: GenerateScenarioDialogProps) {
  const [instructions, setInstructions] = useState('');
  const [generating, setGenerating] = useState(false);
  const [saving, setSaving] = useState(false);
  const [generatedScenario, setGeneratedScenario] = useState<PlaygroundScenarioResponse | null>(null);

  const handleGenerate = async () => {
    if (!instructions.trim()) {
      toast.error('Please describe the scenario you want to generate');
      return;
    }

    setGenerating(true);
    setGeneratedScenario(null);
    try {
      const result = await playgroundScenarioService.generate({
        instructions: instructions.trim(),
        agentName: agentName ?? null,
        aiTaskId: aiTaskId ?? null,
        modelId: modelId ?? null,
      });
      setGeneratedScenario(result);
      toast.success('Scenario generated');
    } catch (err) {
      console.error('Failed to generate scenario:', err);
      toast.error('Failed to generate scenario');
    } finally {
      setGenerating(false);
    }
  };

  const handleLoad = () => {
    if (generatedScenario) {
      onLoad(generatedScenario);
      onOpenChange(false);
      resetState();
    }
  };

  const handleSaveAndLoad = async () => {
    if (!generatedScenario) return;

    setSaving(true);
    try {
      await playgroundScenarioService.create({
        name: generatedScenario.name,
        description: generatedScenario.description,
        tags: generatedScenario.tags,
        systemPrompt: generatedScenario.systemPrompt,
        agentName: agentName ?? generatedScenario.agentName,
        aiTaskId: aiTaskId ?? generatedScenario.aiTaskId,
        turns: generatedScenario.turns.map((t) => ({ role: t.role, content: t.content })),
      });
      toast.success(`Scenario "${generatedScenario.name}" saved`);
      onLoad(generatedScenario);
      onOpenChange(false);
      resetState();
      onSaved?.();
    } catch (err) {
      console.error('Failed to save scenario:', err);
      toast.error('Failed to save scenario');
    } finally {
      setSaving(false);
    }
  };

  const resetState = () => {
    setInstructions('');
    setGeneratedScenario(null);
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(v) => {
        onOpenChange(v);
        if (!v) resetState();
      }}
    >
      <DialogContent className="max-w-[32rem]">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Sparkles className="h-4 w-4" />
            AI Scenario Wizard
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="wizard-instructions" className="text-xs">
              Describe the scenario you want
            </Label>
            <Textarea
              id="wizard-instructions"
              placeholder="e.g. Create a multi-turn conversation where a user asks about their monthly spending breakdown, then follows up about a specific category that spiked."
              value={instructions}
              onChange={(e) => setInstructions(e.target.value)}
              rows={3}
              className="text-sm"
              disabled={generating}
              autoFocus
            />
          </div>

          {(agentName || aiTaskId) && (
            <p className="text-xs text-[var(--color-text-tertiary)]">
              Context: {agentName ? `Agent: ${agentName}` : `AI Task: ${aiTaskId}`}
            </p>
          )}

          {!generatedScenario && (
            <Button
              onClick={handleGenerate}
              disabled={generating || !instructions.trim()}
              className="w-full"
              size="sm"
            >
              {generating ? (
                <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
              ) : (
                <Sparkles className="mr-1.5 h-3.5 w-3.5" />
              )}
              {generating ? 'Generating...' : 'Generate Scenario'}
            </Button>
          )}

          {/* Preview */}
          {generatedScenario && (
            <div className="space-y-3">
              <div className="rounded-md border border-[var(--color-border-light)] bg-[var(--color-background)] p-3">
                <h4 className="text-sm font-medium">{generatedScenario.name}</h4>
                {generatedScenario.description && (
                  <p className="mt-1 text-xs text-[var(--color-text-secondary)]">
                    {generatedScenario.description}
                  </p>
                )}
                {generatedScenario.tags.length > 0 && (
                  <div className="mt-2 flex flex-wrap gap-1">
                    {generatedScenario.tags.map((tag) => (
                      <span
                        key={tag}
                        className="rounded-full bg-[var(--color-brand-primary)]/10 px-2 py-0.5 text-[10px] text-[var(--color-brand-primary)]"
                      >
                        {tag}
                      </span>
                    ))}
                  </div>
                )}
              </div>

              <div className="max-h-48 space-y-2 overflow-y-auto rounded-md border border-[var(--color-border-light)] p-3">
                {generatedScenario.turns.map((turn, i) => (
                  <div key={i} className="flex gap-2 text-xs">
                    <span
                      className={`mt-0.5 shrink-0 rounded px-1.5 py-0.5 font-mono text-[10px] uppercase ${
                        turn.role === 'user'
                          ? 'bg-blue-500/10 text-blue-600'
                          : 'bg-green-500/10 text-green-600'
                      }`}
                    >
                      {turn.role}
                    </span>
                    <p className="text-[var(--color-text-secondary)] line-clamp-3">{turn.content}</p>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {generatedScenario && (
          <DialogFooter>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setGeneratedScenario(null)}
              disabled={saving}
            >
              <Sparkles className="mr-1.5 h-3.5 w-3.5" />
              Regenerate
            </Button>
            <Button variant="outline" size="sm" onClick={handleLoad} disabled={saving}>
              <Play className="mr-1.5 h-3.5 w-3.5" />
              Load Only
            </Button>
            <Button size="sm" onClick={handleSaveAndLoad} disabled={saving}>
              {saving ? (
                <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
              ) : (
                <Save className="mr-1.5 h-3.5 w-3.5" />
              )}
              Save & Load
            </Button>
          </DialogFooter>
        )}

        {!generatedScenario && (
          <DialogFooter>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                onOpenChange(false);
                resetState();
              }}
            >
              <X className="mr-1.5 h-3.5 w-3.5" />
              Cancel
            </Button>
          </DialogFooter>
        )}
      </DialogContent>
    </Dialog>
  );
}
