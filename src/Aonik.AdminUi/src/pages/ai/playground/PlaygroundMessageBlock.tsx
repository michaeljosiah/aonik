import { useCallback, useEffect, useRef, useState } from 'react';
import {
  Check,
  Loader2,
  Maximize2,
  Minimize2,
  RefreshCw,
  RotateCcw,
  Save,
  Sparkles,
  Trash2,
  X,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { agentConfigService } from '@/services/aiService';
import { toast } from 'sonner';

type MessageRole = 'system' | 'user' | 'assistant';

interface PlaygroundMessageBlockProps {
  role: MessageRole;
  content: string;
  index?: number;
  onRoleChange?: (role: MessageRole) => void;
  onContentChange: (content: string) => void;
  onDelete?: () => void;
  readOnly?: boolean;
  roleFixed?: boolean;
  /** Agent name — enables Save/Reset for system prompt blocks */
  agentName?: string | null;
  /** Default prompt from agent config — enables Reset */
  defaultPrompt?: string | null;
  /** Called after a successful save so the parent can update the baseline */
  onDefaultPromptSaved?: (newDefault: string) => void;
}

export function PlaygroundMessageBlock({
  role,
  content,
  index,
  onRoleChange,
  onContentChange,
  onDelete,
  readOnly = false,
  roleFixed = false,
  agentName,
  defaultPrompt,
  onDefaultPromptSaved,
}: PlaygroundMessageBlockProps) {
  const [saving, setSaving] = useState(false);
  const tokenEstimate = Math.round(content.length / 4);

  const isSystem = role === 'system';
  const isSystemWithAgent = isSystem && agentName;
  const hasChanges = defaultPrompt !== null && defaultPrompt !== undefined && content !== defaultPrompt;

  // ── AI prompt wizard state ──────────────────────────────────────
  const [wizardOpen, setWizardOpen] = useState(false);
  const [wizardIntent, setWizardIntent] = useState('');
  const [wizardImproving, setWizardImproving] = useState(false);
  const [wizardPreview, setWizardPreview] = useState<string | null>(null);

  // ── Fullscreen state ────────────────────────────────────────────
  const [fullscreen, setFullscreen] = useState(false);
  const fullscreenTextareaRef = useRef<HTMLTextAreaElement>(null);

  // Lock body scroll when fullscreen
  useEffect(() => {
    if (fullscreen) {
      document.body.style.overflow = 'hidden';
      // Focus the fullscreen textarea
      setTimeout(() => fullscreenTextareaRef.current?.focus(), 50);
    } else {
      document.body.style.overflow = '';
    }
    return () => { document.body.style.overflow = ''; };
  }, [fullscreen]);

  // Close fullscreen on Escape
  useEffect(() => {
    if (!fullscreen) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setFullscreen(false);
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [fullscreen]);

  // ── Handlers ────────────────────────────────────────────────────

  const handleSave = async () => {
    if (!agentName) return;
    setSaving(true);
    try {
      await agentConfigService.upsert(agentName, { instructionsText: content });
      onDefaultPromptSaved?.(content);
      toast.success('Saved prompt to agent config');
    } catch (err) {
      toast.error(`Save failed: ${(err as Error).message}`);
    } finally {
      setSaving(false);
    }
  };

  const handleReset = () => {
    if (defaultPrompt !== null && defaultPrompt !== undefined) {
      onContentChange(defaultPrompt);
    }
  };

  const openWizard = useCallback(() => {
    setWizardOpen(true);
    setWizardIntent('');
    setWizardPreview(null);
  }, []);

  const closeWizard = useCallback(() => {
    setWizardOpen(false);
    setWizardIntent('');
    setWizardPreview(null);
    setWizardImproving(false);
  }, []);

  const runImprove = useCallback(async () => {
    if (!wizardIntent.trim()) return;
    setWizardImproving(true);
    try {
      const improved = await agentConfigService.improvePrompt(content || null, wizardIntent);
      setWizardPreview(improved);
    } catch (err) {
      toast.error(`AI improvement failed: ${(err as Error).message}`);
    } finally {
      setWizardImproving(false);
    }
  }, [content, wizardIntent]);

  const acceptWizard = useCallback(() => {
    if (wizardPreview) {
      onContentChange(wizardPreview);
      toast.success('Prompt updated');
    }
    closeWizard();
  }, [wizardPreview, onContentChange, closeWizard]);

  // ── Render ──────────────────────────────────────────────────────

  return (
    <>
      <div className="flex gap-3 border-b border-[var(--color-border-light)] px-6 py-4">
        {/* Role selector / label */}
        <div className="w-24 shrink-0 pt-2">
          {roleFixed ? (
            <span className="text-xs font-medium capitalize text-[var(--color-text-secondary)]">
              {role}
            </span>
          ) : (
            <Select value={role} onValueChange={(v) => onRoleChange?.(v as MessageRole)}>
              <SelectTrigger className="h-7 text-xs">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="system">System</SelectItem>
                <SelectItem value="user">User</SelectItem>
                <SelectItem value="assistant">Assistant</SelectItem>
              </SelectContent>
            </Select>
          )}
          {index !== undefined && (
            <span className="mt-0.5 block text-[10px] text-[var(--color-text-tertiary)]">
              #{index}
            </span>
          )}
        </div>

        {/* Content area */}
        <div className="min-w-0 flex-1 space-y-2">
          {/* Action bar above textarea for system prompts */}
          {isSystem && (
            <div className="flex items-center justify-end gap-1">
              <Button
                variant="ghost"
                size="sm"
                onClick={openWizard}
                className="gap-1.5 text-xs h-7 text-[var(--color-brand-primary)] hover:text-[var(--color-brand-primary)]"
              >
                <Sparkles className="w-3.5 h-3.5" />
                Regenerate with AI
              </Button>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setFullscreen(true)}
                title="Edit in fullscreen"
                className="h-7 w-7 p-0 text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)]"
              >
                <Maximize2 className="w-3.5 h-3.5" />
              </Button>
            </div>
          )}

          <Textarea
            value={content}
            onChange={(e) => onContentChange(e.target.value)}
            placeholder={`Enter ${role} message...`}
            rows={role === 'system' ? 10 : 2}
            className="resize-y font-mono text-xs leading-relaxed"
            readOnly={readOnly}
          />

          {/* Inline AI wizard */}
          {isSystem && wizardOpen && (
            <div className="space-y-2 rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-3">
              <Textarea
                value={wizardIntent}
                onChange={(e) => setWizardIntent(e.target.value)}
                placeholder="Describe how you'd like the prompt changed (e.g. 'Make it more concise and add risk assessment focus')..."
                rows={2}
                disabled={wizardImproving}
                className="text-sm bg-[var(--color-surface)]"
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) {
                    e.preventDefault();
                    runImprove();
                  }
                }}
              />

              {wizardPreview && (
                <pre className="text-xs text-[var(--color-text-secondary)] bg-[var(--color-surface)] rounded-md p-3 overflow-auto max-h-48 whitespace-pre-wrap font-mono border border-[var(--color-border-light)]">
                  {wizardPreview}
                </pre>
              )}

              <div className="flex items-center gap-2">
                <Button size="sm" onClick={runImprove} disabled={wizardImproving || !wizardIntent.trim()} className="gap-1.5">
                  {wizardImproving ? (
                    <><Loader2 className="w-3.5 h-3.5 animate-spin" /> Generating...</>
                  ) : (
                    <><Sparkles className="w-3.5 h-3.5" /> Generate prompt</>
                  )}
                </Button>
                <Button variant="ghost" size="sm" onClick={closeWizard} className="gap-1 text-[var(--color-text-tertiary)]">
                  <X className="w-3.5 h-3.5" /> Discard
                </Button>
                {wizardPreview && (
                  <>
                    <Button variant="ghost" size="sm" onClick={() => setWizardPreview(null)} className="gap-1 text-[var(--color-text-tertiary)]">
                      <RefreshCw className="w-3.5 h-3.5" /> Regenerate
                    </Button>
                    <Button variant="ghost" size="sm" onClick={acceptWizard} className="gap-1 text-[var(--color-success)]">
                      <Check className="w-3.5 h-3.5" /> Accept
                    </Button>
                  </>
                )}
              </div>
            </div>
          )}
        </div>

        {/* Actions column */}
        <div className="flex shrink-0 flex-col items-end gap-1.5 pt-1">
          <span className="rounded-full bg-[var(--color-surface-inset)] px-2 py-0.5 text-[10px] font-medium tabular-nums text-[var(--color-text-tertiary)]">
            {tokenEstimate}
          </span>

          {/* Save / Reset for system prompt when an agent is selected */}
          {isSystemWithAgent && hasChanges && (
            <>
              <Button
                variant="ghost"
                size="sm"
                onClick={handleReset}
                title="Reset to agent default"
                className="h-6 w-6 p-0 text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)]"
              >
                <RotateCcw className="h-3 w-3" />
              </Button>
              <Button
                variant="ghost"
                size="sm"
                onClick={handleSave}
                disabled={saving}
                title="Save prompt to agent config"
                className="h-6 w-6 p-0 text-[var(--color-brand-primary)] hover:text-[var(--color-brand-primary)]"
              >
                <Save className="h-3 w-3" />
              </Button>
            </>
          )}

          {onDelete && (
            <Button
              variant="ghost"
              size="sm"
              onClick={onDelete}
              className="h-6 w-6 p-0 text-[var(--color-text-tertiary)] hover:text-[var(--color-error)]"
            >
              <Trash2 className="h-3 w-3" />
            </Button>
          )}
        </div>
      </div>

      {/* ── Fullscreen overlay ── */}
      {fullscreen && (
        <div className="fixed inset-0 z-[200] flex flex-col bg-[var(--color-background)]">
          {/* Toolbar */}
          <div className="flex items-center justify-between border-b border-[var(--color-border-light)] bg-[var(--color-surface)] px-6 py-3">
            <div className="flex items-center gap-3">
              <span className="text-sm font-semibold text-[var(--color-text-primary)]">System Prompt</span>
              <span className="rounded-full bg-[var(--color-surface-inset)] px-2 py-0.5 text-[10px] font-medium tabular-nums text-[var(--color-text-tertiary)]">
                ~{tokenEstimate} tokens
              </span>
            </div>
            <div className="flex items-center gap-2">
              <Button
                variant="ghost"
                size="sm"
                onClick={openWizard}
                className="gap-1.5 text-xs h-8 text-[var(--color-brand-primary)] hover:text-[var(--color-brand-primary)]"
              >
                <Sparkles className="w-3.5 h-3.5" />
                Regenerate with AI
              </Button>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setFullscreen(false)}
                title="Exit fullscreen (Esc)"
                className="h-8 w-8 p-0 text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)]"
              >
                <Minimize2 className="w-4 h-4" />
              </Button>
            </div>
          </div>

          {/* Editor area */}
          <div className="flex-1 overflow-auto p-6">
            <div className="mx-auto max-w-[56rem] space-y-4">
              <textarea
                ref={fullscreenTextareaRef}
                value={content}
                onChange={(e) => onContentChange(e.target.value)}
                placeholder="Enter system prompt..."
                className="w-full min-h-[calc(100vh-200px)] resize-none rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)] p-5 font-mono text-sm leading-relaxed text-[var(--color-text-primary)] placeholder:text-[var(--color-text-tertiary)] focus:border-[var(--color-brand-primary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-brand-primary)]"
                readOnly={readOnly}
              />

              {/* Inline AI wizard in fullscreen */}
              {wizardOpen && (
                <div className="space-y-3 rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-4">
                  <Textarea
                    value={wizardIntent}
                    onChange={(e) => setWizardIntent(e.target.value)}
                    placeholder="Describe how you'd like the prompt changed..."
                    rows={2}
                    disabled={wizardImproving}
                    className="text-sm bg-[var(--color-surface)]"
                    onKeyDown={(e) => {
                      if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) {
                        e.preventDefault();
                        runImprove();
                      }
                    }}
                  />

                  {wizardPreview && (
                    <pre className="text-xs text-[var(--color-text-secondary)] bg-[var(--color-surface)] rounded-md p-4 overflow-auto max-h-64 whitespace-pre-wrap font-mono border border-[var(--color-border-light)]">
                      {wizardPreview}
                    </pre>
                  )}

                  <div className="flex items-center gap-2">
                    <Button size="sm" onClick={runImprove} disabled={wizardImproving || !wizardIntent.trim()} className="gap-1.5">
                      {wizardImproving ? (
                        <><Loader2 className="w-3.5 h-3.5 animate-spin" /> Generating...</>
                      ) : (
                        <><Sparkles className="w-3.5 h-3.5" /> Generate prompt</>
                      )}
                    </Button>
                    <Button variant="ghost" size="sm" onClick={closeWizard} className="gap-1 text-[var(--color-text-tertiary)]">
                      <X className="w-3.5 h-3.5" /> Discard
                    </Button>
                    {wizardPreview && (
                      <>
                        <Button variant="ghost" size="sm" onClick={() => setWizardPreview(null)} className="gap-1 text-[var(--color-text-tertiary)]">
                          <RefreshCw className="w-3.5 h-3.5" /> Regenerate
                        </Button>
                        <Button variant="ghost" size="sm" onClick={acceptWizard} className="gap-1 text-[var(--color-success)]">
                          <Check className="w-3.5 h-3.5" /> Accept
                        </Button>
                      </>
                    )}
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </>
  );
}
