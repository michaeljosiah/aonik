import { useState } from 'react';
import { Eye, User, FileText, Cpu, AlertCircle, Loader2 } from 'lucide-react';
import { playgroundService } from '@/services/aiService';
import type { AgentConfigurationResponse } from '@/types/ai';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';

interface AgentContextDrawerProps {
  agentConfig: AgentConfigurationResponse | null;
  /** The user brief JSON currently set in the playground (if any). */
  currentUserBriefJson: string | null;
}

/**
 * Dialog that shows the full composed context for the selected agent:
 *   - System prompt (base instructions)
 *   - User Brief (projected or current)
 *   - Composed preview (what the LLM will actually receive)
 */
export function AgentContextDrawer({
  agentConfig,
  currentUserBriefJson,
}: AgentContextDrawerProps) {
  const [userId, setUserId] = useState('');
  const [loadedBrief, setLoadedBrief] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleLoadBrief = async () => {
    if (!userId.trim()) return;
    setLoading(true);
    setError(null);
    try {
      const brief = await playgroundService.projectUserBrief(userId.trim());
      setLoadedBrief(JSON.stringify(brief, null, 2));
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  // The brief to display: prefer one just loaded here, fall back to playground's current
  const displayBrief = loadedBrief ?? currentUserBriefJson;

  // Build the composed preview — the full message sequence the LLM will receive
  const composedSections: { label: string; content: string }[] = [];

  if (agentConfig?.instructionsText) {
    composedSections.push({
      label: 'System Instructions',
      content: agentConfig.instructionsText,
    });
  }

  if (displayBrief) {
    composedSections.push({
      label: 'User Brief (injected system message)',
      content: `## User Brief (current context — treat as ground truth for this session)\n\n\`\`\`json\n${displayBrief}\n\`\`\``,
    });
  }

  return (
    <Dialog>
      <DialogTrigger asChild>
        <Button
          variant="ghost"
          size="sm"
          className="h-8 text-xs"
          disabled={!agentConfig}
        >
          <Eye className="mr-1.5 h-3.5 w-3.5" />
          Context
        </Button>
      </DialogTrigger>

      <DialogContent className="max-h-[85vh] max-w-3xl overflow-hidden flex flex-col">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Cpu className="h-4 w-4" />
            Agent Context: {agentConfig?.name ?? 'None'}
          </DialogTitle>
          <DialogDescription>
            Full context that will be composed and sent to the LLM at runtime.
          </DialogDescription>
        </DialogHeader>

        {/* Agent metadata badges */}
        {agentConfig && (
          <div className="flex flex-wrap items-center gap-2 text-xs">
            <span className="rounded bg-[var(--color-background)] px-2 py-0.5 text-[var(--color-text-secondary)]">
              Domain: {agentConfig.domain}
            </span>
            <span className="rounded bg-[var(--color-background)] px-2 py-0.5 text-[var(--color-text-secondary)]">
              Risk: {agentConfig.riskTier}
            </span>
            {agentConfig.modelName && (
              <span className="rounded bg-[var(--color-background)] px-2 py-0.5 text-[var(--color-text-secondary)]">
                Model: {agentConfig.modelName}
              </span>
            )}
            <span
              className={`rounded px-2 py-0.5 ${
                agentConfig.requiresUserBrief
                  ? 'bg-blue-500/10 text-blue-600 dark:text-blue-400'
                  : 'bg-[var(--color-background)] text-[var(--color-text-tertiary)]'
              }`}
            >
              <User className="mr-1 inline h-3 w-3" />
              {agentConfig.requiresUserBrief
                ? 'Requires User Brief'
                : 'No User Brief'}
            </span>
          </div>
        )}

        {/* Tabs */}
        <Tabs defaultValue="composed" className="flex-1 overflow-hidden flex flex-col">
          <TabsList className="w-full shrink-0">
            <TabsTrigger value="composed" className="flex-1 text-xs">
              Composed Preview
            </TabsTrigger>
            <TabsTrigger value="system" className="flex-1 text-xs">
              System Prompt
            </TabsTrigger>
            <TabsTrigger value="brief" className="flex-1 text-xs">
              User Brief
            </TabsTrigger>
          </TabsList>

          {/* Composed preview tab */}
          <TabsContent value="composed" className="flex-1 overflow-y-auto">
            {composedSections.length === 0 ? (
              <div className="flex items-center gap-2 py-8 text-sm text-[var(--color-text-tertiary)] justify-center">
                <AlertCircle className="h-4 w-4" />
                Select an agent to see the composed context.
              </div>
            ) : (
              <div className="space-y-4">
                {composedSections.map((section, i) => (
                  <div key={i}>
                    <div className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold text-[var(--color-text-secondary)]">
                      <FileText className="h-3 w-3" />
                      {section.label}
                      <span className="ml-auto text-[10px] font-normal text-[var(--color-text-tertiary)]">
                        ~{Math.round(section.content.length / 4)} tokens
                      </span>
                    </div>
                    <pre className="max-h-80 overflow-auto rounded border border-[var(--color-border-light)] bg-[var(--color-background)] p-3 text-xs leading-relaxed whitespace-pre-wrap">
                      {section.content}
                    </pre>
                  </div>
                ))}
                <div className="border-t border-[var(--color-border-light)] pt-2 text-xs text-[var(--color-text-tertiary)]">
                  Total estimated: ~
                  {Math.round(
                    composedSections.reduce((sum, s) => sum + s.content.length, 0) / 4,
                  )}{' '}
                  tokens across {composedSections.length} section
                  {composedSections.length !== 1 ? 's' : ''}
                </div>
              </div>
            )}
          </TabsContent>

          {/* System prompt tab */}
          <TabsContent value="system" className="flex-1 overflow-y-auto">
            {agentConfig?.instructionsText ? (
              <pre className="overflow-auto rounded border border-[var(--color-border-light)] bg-[var(--color-background)] p-3 text-xs leading-relaxed whitespace-pre-wrap">
                {agentConfig.instructionsText}
              </pre>
            ) : (
              <div className="py-8 text-center text-sm text-[var(--color-text-tertiary)]">
                No system prompt available.
              </div>
            )}
          </TabsContent>

          {/* User brief tab */}
          <TabsContent value="brief" className="flex-1 overflow-y-auto">
            {agentConfig?.requiresUserBrief ? (
              <div className="space-y-3">
                {/* Load real user brief */}
                <div className="space-y-1.5">
                  <Label className="text-xs">Load real user brief by ID</Label>
                  <div className="flex gap-2">
                    <Input
                      placeholder="User ID (GUID)"
                      value={userId}
                      onChange={(e) => setUserId(e.target.value)}
                      className="h-8 flex-1 text-xs"
                    />
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={handleLoadBrief}
                      disabled={loading || !userId.trim()}
                      className="h-8 text-xs"
                    >
                      {loading ? (
                        <Loader2 className="h-3 w-3 animate-spin" />
                      ) : (
                        'Load'
                      )}
                    </Button>
                  </div>
                  {error && (
                    <div className="flex items-center gap-1.5 text-xs text-[var(--color-error)]">
                      <AlertCircle className="h-3 w-3" />
                      {error}
                    </div>
                  )}
                </div>

                {/* Display brief */}
                {displayBrief ? (
                  <div>
                    <div className="mb-1.5 flex items-center gap-1.5 text-xs text-[var(--color-text-secondary)]">
                      <span className="font-semibold">
                        {loadedBrief ? 'Loaded User Brief' : 'Current Playground Brief'}
                      </span>
                      <span className="ml-auto text-[10px] text-[var(--color-text-tertiary)]">
                        ~{Math.round(displayBrief.length / 4)} tokens
                      </span>
                    </div>
                    <pre className="max-h-96 overflow-auto rounded border border-[var(--color-border-light)] bg-[var(--color-background)] p-3 text-xs leading-relaxed whitespace-pre-wrap">
                      {displayBrief}
                    </pre>
                  </div>
                ) : (
                  <div className="rounded border border-dashed border-[var(--color-border)] p-4 text-center text-xs text-[var(--color-text-tertiary)]">
                    No user brief loaded. Enter a user ID above or set one in the
                    Variables popover.
                  </div>
                )}
              </div>
            ) : (
              <div className="py-8 text-center text-sm text-[var(--color-text-tertiary)]">
                This agent does not use a User Brief.
                <p className="mt-1 text-xs">
                  Only user-facing agents (e.g. personal-finance-agent) inject per-user financial context.
                </p>
              </div>
            )}
          </TabsContent>
        </Tabs>
      </DialogContent>
    </Dialog>
  );
}
