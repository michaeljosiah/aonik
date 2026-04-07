import { useCallback, useEffect, useState } from 'react';
import {
  BookOpen,
  ChevronDown,
  Loader2,
  Save,
  Sparkles,
  Trash2,
  MessageSquare,
} from 'lucide-react';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Button } from '@/components/ui/button';
import { playgroundScenarioService } from '@/services/aiService';
import type {
  PlaygroundScenarioResponse,
  PlaygroundScenarioSummaryResponse,
  CreatePlaygroundScenarioRequest,
} from '@/types/ai';
import { SaveScenarioDialog } from './SaveScenarioDialog';
import { GenerateScenarioDialog } from './GenerateScenarioDialog';
import { toast } from 'sonner';

interface ScenarioPickerProps {
  /** Current agent name (for filtering and context) */
  agentName?: string | null;
  /** Current AI task ID (for context) */
  aiTaskId?: string | null;
  /** Current model ID (for AI wizard) */
  modelId?: string | null;
  /** Data to save when "Save as Scenario" is clicked */
  currentScenarioData: Omit<CreatePlaygroundScenarioRequest, 'name' | 'description' | 'tags'>;
  /** Called when a scenario is loaded */
  onLoad: (scenario: PlaygroundScenarioResponse) => void;
}

export function ScenarioPicker({
  agentName,
  aiTaskId,
  modelId,
  currentScenarioData,
  onLoad,
}: ScenarioPickerProps) {
  const [scenarios, setScenarios] = useState<PlaygroundScenarioSummaryResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [loadingId, setLoadingId] = useState<string | null>(null);
  const [popoverOpen, setPopoverOpen] = useState(false);
  const [saveDialogOpen, setSaveDialogOpen] = useState(false);
  const [wizardDialogOpen, setWizardDialogOpen] = useState(false);

  const fetchScenarios = useCallback(async () => {
    setLoading(true);
    try {
      const result = await playgroundScenarioService.list(agentName ?? undefined);
      setScenarios(result);
    } catch (err) {
      console.error('Failed to load scenarios:', err);
    } finally {
      setLoading(false);
    }
  }, [agentName]);

  // Refresh when popover opens or agent changes
  useEffect(() => {
    if (popoverOpen) {
      fetchScenarios();
    }
  }, [popoverOpen, fetchScenarios]);

  const handleSelectScenario = async (id: string) => {
    setLoadingId(id);
    try {
      const scenario = await playgroundScenarioService.get(id);
      onLoad(scenario);
      setPopoverOpen(false);
      toast.success(`Loaded scenario: ${scenario.name}`);
    } catch (err) {
      console.error('Failed to load scenario:', err);
      toast.error('Failed to load scenario');
    } finally {
      setLoadingId(null);
    }
  };

  const handleDeleteScenario = async (e: React.MouseEvent, id: string, name: string) => {
    e.stopPropagation();
    try {
      await playgroundScenarioService.delete(id);
      setScenarios((prev) => prev.filter((s) => s.id !== id));
      toast.success(`Deleted scenario: ${name}`);
    } catch (err) {
      console.error('Failed to delete scenario:', err);
      toast.error('Failed to delete scenario');
    }
  };

  return (
    <>
      <Popover open={popoverOpen} onOpenChange={setPopoverOpen}>
        <PopoverTrigger asChild>
          <Button variant="ghost" size="sm" className="h-8 text-xs">
            <BookOpen className="mr-1.5 h-3.5 w-3.5" />
            Scenarios
            <ChevronDown className="ml-1 h-3 w-3" />
          </Button>
        </PopoverTrigger>
        <PopoverContent align="start" className="w-80 p-0">
          <div className="border-b border-[var(--color-border-light)] px-3 py-2">
            <h4 className="text-xs font-medium text-[var(--color-text-primary)]">
              Playground Scenarios
            </h4>
            <p className="text-[10px] text-[var(--color-text-tertiary)]">
              Load a saved conversation setup or generate one with AI
            </p>
          </div>

          {/* Scenario list */}
          <div className="max-h-60 overflow-y-auto">
            {loading ? (
              <div className="flex items-center justify-center py-6">
                <Loader2 className="h-4 w-4 animate-spin text-[var(--color-text-tertiary)]" />
              </div>
            ) : scenarios.length === 0 ? (
              <div className="px-3 py-6 text-center">
                <MessageSquare className="mx-auto h-8 w-8 text-[var(--color-text-tertiary)] opacity-40" />
                <p className="mt-2 text-xs text-[var(--color-text-tertiary)]">No scenarios yet</p>
                <p className="text-[10px] text-[var(--color-text-tertiary)]">
                  Save a conversation or use the AI wizard
                </p>
              </div>
            ) : (
              scenarios.map((scenario) => (
                <div
                  key={scenario.id}
                  role="button"
                  tabIndex={0}
                  className="flex w-full cursor-pointer items-start gap-2 border-b border-[var(--color-border-light)] px-3 py-2.5 text-left transition-colors last:border-b-0 hover:bg-[var(--color-background)]"
                  onClick={() => !loadingId && handleSelectScenario(scenario.id)}
                  onKeyDown={(e) => { if (e.key === 'Enter') handleSelectScenario(scenario.id); }}
                >
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-1.5">
                      <span className="truncate text-xs font-medium text-[var(--color-text-primary)]">
                        {scenario.name}
                      </span>
                      {loadingId === scenario.id && (
                        <Loader2 className="h-3 w-3 shrink-0 animate-spin text-[var(--color-text-tertiary)]" />
                      )}
                    </div>
                    {scenario.description && (
                      <p className="mt-0.5 line-clamp-1 text-[10px] text-[var(--color-text-tertiary)]">
                        {scenario.description}
                      </p>
                    )}
                    <div className="mt-1 flex items-center gap-2">
                      <span className="text-[10px] text-[var(--color-text-tertiary)]">
                        {scenario.turnCount} turn{scenario.turnCount !== 1 ? 's' : ''}
                      </span>
                      {scenario.tags.length > 0 && (
                        <div className="flex gap-1">
                          {scenario.tags.slice(0, 3).map((tag) => (
                            <span
                              key={tag}
                              className="rounded-full bg-[var(--color-brand-primary)]/10 px-1.5 py-0 text-[9px] text-[var(--color-brand-primary)]"
                            >
                              {tag}
                            </span>
                          ))}
                          {scenario.tags.length > 3 && (
                            <span className="text-[9px] text-[var(--color-text-tertiary)]">
                              +{scenario.tags.length - 3}
                            </span>
                          )}
                        </div>
                      )}
                    </div>
                  </div>
                  <button
                    className="shrink-0 rounded p-1 text-[var(--color-text-tertiary)] transition-colors hover:bg-red-500/10 hover:text-red-500"
                    onClick={(e) => handleDeleteScenario(e, scenario.id, scenario.name)}
                    title="Delete scenario"
                  >
                    <Trash2 className="h-3 w-3" />
                  </button>
                </div>
              ))
            )}
          </div>

          {/* Actions */}
          <div className="flex gap-1 border-t border-[var(--color-border-light)] p-2">
            <Button
              variant="ghost"
              size="sm"
              className="h-7 flex-1 text-[11px]"
              onClick={() => {
                setPopoverOpen(false);
                setSaveDialogOpen(true);
              }}
            >
              <Save className="mr-1 h-3 w-3" />
              Save Current
            </Button>
            <Button
              variant="ghost"
              size="sm"
              className="h-7 flex-1 text-[11px]"
              onClick={() => {
                setPopoverOpen(false);
                setWizardDialogOpen(true);
              }}
            >
              <Sparkles className="mr-1 h-3 w-3" />
              AI Wizard
            </Button>
          </div>
        </PopoverContent>
      </Popover>

      {/* Save Dialog */}
      <SaveScenarioDialog
        open={saveDialogOpen}
        onOpenChange={setSaveDialogOpen}
        scenarioData={currentScenarioData}
        onSaved={fetchScenarios}
      />

      {/* Generate Dialog */}
      <GenerateScenarioDialog
        open={wizardDialogOpen}
        onOpenChange={setWizardDialogOpen}
        agentName={agentName}
        aiTaskId={aiTaskId}
        modelId={modelId}
        onLoad={onLoad}
        onSaved={fetchScenarios}
      />
    </>
  );
}
