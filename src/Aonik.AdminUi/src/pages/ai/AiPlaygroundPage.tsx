import { useCallback, useState, useRef, useEffect } from 'react';
import {
  FlaskConical,
  Plus,
  Play,
  RotateCcw,
  Square,
  ChevronDown,
  Columns2,
  Wrench,
  Variable,
  SlidersHorizontal,
} from 'lucide-react';
import { usePlaygroundChat } from '@/hooks/usePlaygroundChat';
import { AgentPicker } from './playground/AgentPicker';
import { ModelSelector } from './playground/ModelSelector';
import { UserBriefPicker } from './playground/UserBriefPicker';
import { ToolToggleList } from './playground/ToolToggleList';
import { PlaygroundMessageBlock } from './playground/PlaygroundMessageBlock';
import { PlaygroundOutputPanel } from './playground/PlaygroundOutputPanel';
import { ModelComparisonView } from './playground/ModelComparisonView';
import { RunHistoryPanel } from './playground/RunHistoryPanel';
import {
  Popover,
  PopoverTrigger,
  PopoverContent,
} from '@/components/ui/popover';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import type { AgentConfigurationResponse } from '@/types/ai';

// ─── Types ──────────────────────────────────────────────────────────────────

interface EditableMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
}

// ─── Constants ──────────────────────────────────────────────────────────────

const breadcrumbItems = [
  { label: 'AI', href: '/ai/agents' },
  { label: 'Playground', icon: <FlaskConical className="h-3.5 w-3.5" /> },
];

// ─── Page ───────────────────────────────────────────────────────────────────

export function AiPlaygroundPage() {
  const [mode, setMode] = useState<'single' | 'compare'>('single');
  const [allTools, setAllTools] = useState<string[]>([]);
  const defaultPromptRef = useRef<string | null>(null);

  // Editable user/assistant messages (system prompt is in config)
  const [editableMessages, setEditableMessages] = useState<EditableMessage[]>([
    { id: `msg-${Date.now()}`, role: 'user', content: '' },
  ]);

  const {
    config,
    updateConfig,
    output,
    isStreaming,
    streamError,
    metrics,
    runHistory,
    submitMessages,
    stopStreaming,
    resetChat,
    clearHistory,
  } = usePlaygroundChat();

  // ── Agent change handler ────────────────────────────────────────────────

  const handleAgentChange = useCallback(
    (agentName: string | null, agentConfig?: AgentConfigurationResponse) => {
      if (agentConfig) {
        let tools: string[] = [];
        try {
          tools = JSON.parse(agentConfig.toolsetIdsJson || '[]');
        } catch {
          tools = [];
        }

        setAllTools(tools);
        defaultPromptRef.current = agentConfig.instructionsText ?? '';

        updateConfig({
          agentName,
          systemPrompt: agentConfig.instructionsText ?? '',
          enabledToolNames: tools,
          modelId: agentConfig.modelId ?? null,
        });
      } else {
        setAllTools([]);
        defaultPromptRef.current = null;

        updateConfig({
          agentName: null,
          systemPrompt: '',
          enabledToolNames: [],
          modelId: null,
        });
      }
      resetChat();
    },
    [updateConfig, resetChat],
  );

  // ── Submit handler ──────────────────────────────────────────────────────

  const handleSubmit = useCallback(() => {
    const msgs = editableMessages
      .filter((m) => m.content.trim())
      .map((m) => ({ role: m.role, content: m.content }));
    if (msgs.length > 0) {
      submitMessages(msgs);
    }
  }, [editableMessages, submitMessages]);

  // ── Add output as assistant message ─────────────────────────────────────

  const handleAddOutputToMessages = useCallback(() => {
    if (output) {
      setEditableMessages((prev) => [
        ...prev,
        { id: `msg-${Date.now()}`, role: 'assistant' as const, content: output },
        { id: `msg-${Date.now() + 1}`, role: 'user' as const, content: '' },
      ]);
    }
  }, [output]);

  // ── Ctrl+Enter to submit ────────────────────────────────────────────────

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
        e.preventDefault();
        handleSubmit();
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [handleSubmit]);

  // ── Message management ──────────────────────────────────────────────────

  const addMessage = (role: 'user' | 'assistant') => {
    setEditableMessages((prev) => [
      ...prev,
      { id: `msg-${Date.now()}`, role, content: '' },
    ]);
  };

  const updateMessage = (id: string, updates: Partial<EditableMessage>) => {
    setEditableMessages((prev) =>
      prev.map((m) => (m.id === id ? { ...m, ...updates } : m)),
    );
  };

  const deleteMessage = (id: string) => {
    setEditableMessages((prev) => prev.filter((m) => m.id !== id));
  };

  const handleResetPlayground = () => {
    setEditableMessages([{ id: `msg-${Date.now()}`, role: 'user', content: '' }]);
    resetChat();
  };

  // ── Compare mode ────────────────────────────────────────────────────────

  if (mode === 'compare') {
    return (
      <div className="flex h-full flex-col overflow-hidden">
        <PlaygroundHeader
          mode={mode}
          onModeChange={setMode}
          onReset={handleResetPlayground}
        />
        <ModelComparisonView sharedConfig={config} />
        <RunHistoryPanel runs={runHistory} onClear={clearHistory} />
      </div>
    );
  }

  // ── Single mode (Langfuse-style) ───────────────────────────────────────

  return (
    <div className="flex h-full flex-col overflow-hidden">
      {/* Header */}
      <PlaygroundHeader
        mode={mode}
        onModeChange={setMode}
        isStreaming={isStreaming}
        onRun={handleSubmit}
        onStop={stopStreaming}
        onReset={handleResetPlayground}
      />

      {/* Config bar: agent + model + popover triggers */}
      <div className="flex items-center gap-3 border-b border-[var(--color-border-light)] px-6 py-2.5">
        <AgentPicker compact value={config.agentName} onChange={handleAgentChange} />
        <ModelSelector
          compact
          value={config.modelId}
          onChange={(id) => updateConfig({ modelId: id })}
        />

        <div className="mx-1 h-5 w-px bg-[var(--color-border-light)]" />

        {/* Tools popover */}
        <Popover>
          <PopoverTrigger asChild>
            <Button variant="ghost" size="sm" className="h-8 text-xs">
              <Wrench className="mr-1.5 h-3.5 w-3.5" />
              Tools
              {config.enabledToolNames.length > 0 && (
                <span className="ml-1.5 rounded-full bg-[var(--color-brand-primary)] px-1.5 py-0.5 text-[10px] text-white">
                  {config.enabledToolNames.length}
                </span>
              )}
            </Button>
          </PopoverTrigger>
          <PopoverContent align="start" className="w-80">
            <ToolToggleList
              allTools={allTools}
              enabledTools={config.enabledToolNames}
              onChange={(tools) => updateConfig({ enabledToolNames: tools })}
            />
          </PopoverContent>
        </Popover>

        {/* Variables popover */}
        <Popover>
          <PopoverTrigger asChild>
            <Button variant="ghost" size="sm" className="h-8 text-xs">
              <Variable className="mr-1.5 h-3.5 w-3.5" />
              Variables
              {config.userBriefJson && (
                <span className="ml-1.5 h-2 w-2 rounded-full bg-[var(--color-brand-primary)]" />
              )}
            </Button>
          </PopoverTrigger>
          <PopoverContent align="start" className="w-96">
            <UserBriefPicker
              value={config.userBriefJson}
              onChange={(json) => updateConfig({ userBriefJson: json })}
            />
          </PopoverContent>
        </Popover>

        {/* Settings popover */}
        <Popover>
          <PopoverTrigger asChild>
            <Button variant="ghost" size="sm" className="h-8 text-xs">
              <SlidersHorizontal className="mr-1.5 h-3.5 w-3.5" />
              Settings
            </Button>
          </PopoverTrigger>
          <PopoverContent align="start" className="w-72">
            <div className="space-y-4">
              <div className="space-y-1.5">
                <Label className="text-xs">
                  Temperature: {config.temperature.toFixed(1)}
                </Label>
                <input
                  type="range"
                  min={0}
                  max={2}
                  step={0.1}
                  value={config.temperature}
                  onChange={(e) =>
                    updateConfig({ temperature: parseFloat(e.target.value) })
                  }
                  className="w-full"
                />
              </div>
              <div className="space-y-1.5">
                <Label className="text-xs">Max tokens: {config.maxTokens}</Label>
                <input
                  type="range"
                  min={256}
                  max={8192}
                  step={256}
                  value={config.maxTokens}
                  onChange={(e) =>
                    updateConfig({ maxTokens: parseInt(e.target.value, 10) })
                  }
                  className="w-full"
                />
              </div>
            </div>
          </PopoverContent>
        </Popover>
      </div>

      {/* Scrollable message area */}
      <div className="flex-1 overflow-y-auto">
        {/* System message block */}
        <PlaygroundMessageBlock
          role="system"
          content={config.systemPrompt}
          onContentChange={(v) => updateConfig({ systemPrompt: v })}
          roleFixed
          agentName={config.agentName}
          defaultPrompt={defaultPromptRef.current}
        />

        {/* User / assistant message blocks */}
        {editableMessages.map((msg, i) => (
          <PlaygroundMessageBlock
            key={msg.id}
            role={msg.role}
            content={msg.content}
            index={i + 1}
            onRoleChange={(role) =>
              updateMessage(msg.id, { role: role as 'user' | 'assistant' })
            }
            onContentChange={(content) => updateMessage(msg.id, { content })}
            onDelete={
              editableMessages.length > 1
                ? () => deleteMessage(msg.id)
                : undefined
            }
          />
        ))}

        {/* Add message controls */}
        <div className="flex items-center gap-2 border-b border-[var(--color-border-light)] px-6 py-2.5">
          <AddMessageButton onAdd={addMessage} />
        </div>
      </div>

      {/* Inline draggable output panel */}
      <PlaygroundOutputPanel
        output={output}
        isStreaming={isStreaming}
        streamError={streamError}
        metrics={metrics}
        onAddToMessages={handleAddOutputToMessages}
      />

      {/* Submit button */}
      <div className="shrink-0 border-t border-[var(--color-border-light)] px-6 py-3">
        <Button
          className="w-full"
          onClick={isStreaming ? stopStreaming : handleSubmit}
          disabled={!isStreaming && editableMessages.every((m) => !m.content.trim())}
        >
          {isStreaming ? (
            <>
              <Square className="mr-2 h-3.5 w-3.5" />
              Stop
            </>
          ) : (
            'Submit'
          )}
        </Button>
      </div>

      {/* Run history (collapsible) */}
      <RunHistoryPanel runs={runHistory} onClear={clearHistory} />
    </div>
  );
}

// ─── Sub-components ─────────────────────────────────────────────────────────

function PlaygroundHeader({
  mode,
  onModeChange,
  isStreaming,
  onRun,
  onStop,
  onReset,
}: {
  mode: 'single' | 'compare';
  onModeChange: (m: 'single' | 'compare') => void;
  isStreaming?: boolean;
  onRun?: () => void;
  onStop?: () => void;
  onReset: () => void;
}) {
  return (
    <div className="shrink-0 px-6 pt-5 pb-0">
      <Breadcrumb items={breadcrumbItems} />
      <div className="mt-3 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">
            AI Playground
          </h1>
          <p className="text-sm text-[var(--color-text-secondary)]">
            Test agents, prompts, and models interactively.
          </p>
        </div>

        <div className="flex items-center gap-2">
          {/* Mode toggle */}
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onModeChange(mode === 'single' ? 'compare' : 'single')}
            className="text-xs"
          >
            <Columns2 className="mr-1.5 h-3.5 w-3.5" />
            {mode === 'single' ? 'Split window' : 'Single'}
          </Button>

          {/* Reset */}
          <Button variant="ghost" size="sm" onClick={onReset} className="text-xs">
            <RotateCcw className="mr-1.5 h-3.5 w-3.5" />
            Reset playground
          </Button>

          {/* Run All */}
          {onRun && (
            <Button
              size="sm"
              onClick={isStreaming ? onStop : onRun}
              className="text-xs"
            >
              {isStreaming ? (
                <>
                  <Square className="mr-1.5 h-3.5 w-3.5" />
                  Stop
                </>
              ) : (
                <>
                  <Play className="mr-1.5 h-3.5 w-3.5" />
                  Run All
                  <kbd className="ml-2 rounded border border-white/20 px-1 py-0.5 text-[10px] font-normal opacity-60">
                    Ctrl+Enter
                  </kbd>
                </>
              )}
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}

function AddMessageButton({
  onAdd,
}: {
  onAdd: (role: 'user' | 'assistant') => void;
}) {
  const [open, setOpen] = useState(false);

  return (
    <div className="relative">
      <Button
        variant="ghost"
        size="sm"
        onClick={() => setOpen(!open)}
        className="h-7 text-xs text-[var(--color-text-secondary)]"
      >
        <Plus className="mr-1 h-3 w-3" />
        Message
        <ChevronDown className="ml-1 h-3 w-3" />
      </Button>

      {open && (
        <>
          <div className="fixed inset-0 z-40" onClick={() => setOpen(false)} />
          <div className="absolute left-0 top-full z-50 mt-1 min-w-[120px] rounded-sm border border-[var(--color-border)] bg-[var(--color-surface)] py-1 shadow-md">
            <button
              className="w-full px-3 py-1.5 text-left text-xs text-[var(--color-text-primary)] hover:bg-[var(--color-background)]"
              onClick={() => {
                onAdd('user');
                setOpen(false);
              }}
            >
              User
            </button>
            <button
              className="w-full px-3 py-1.5 text-left text-xs text-[var(--color-text-primary)] hover:bg-[var(--color-background)]"
              onClick={() => {
                onAdd('assistant');
                setOpen(false);
              }}
            >
              Assistant
            </button>
          </div>
        </>
      )}
    </div>
  );
}
