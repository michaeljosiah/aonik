import { useCallback, useState, useRef, useEffect, useMemo } from 'react';
import {
  FlaskConical,
  Loader2,
  Plus,
  Play,
  RotateCcw,
  Square,
  ChevronDown,
  Columns2,
  Volume2,
  Wrench,
  Variable,
  SlidersHorizontal,
  Bot,
  ListChecks,
} from 'lucide-react';
import { usePlaygroundChat } from '@/hooks/usePlaygroundChat';
import { AgentPicker } from './playground/AgentPicker';
import { AiTaskPicker } from './playground/AiTaskPicker';
import { PromptVariablesForm } from './playground/PromptVariablesForm';
import { ModelSelector } from './playground/ModelSelector';
import { UserBriefPicker } from './playground/UserBriefPicker';
import { ToolToggleList } from './playground/ToolToggleList';
import { AgentContextDrawer } from './playground/AgentContextDrawer';
import { PlaygroundMessageBlock } from './playground/PlaygroundMessageBlock';
import { PlaygroundOutputPanel } from './playground/PlaygroundOutputPanel';
import { ModelComparisonView, type ModelComparisonViewHandle } from './playground/ModelComparisonView';
import { RunHistoryPanel } from './playground/RunHistoryPanel';
import { createPlaygroundFrontendTools } from './playground/frontendTools';
import {
  Popover,
  PopoverTrigger,
  PopoverContent,
} from '@/components/ui/popover';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { textToSpeechSettingsService } from '@/services/textToSpeechSettingsService';
import type { AgentConfigurationResponse } from '@/types/ai';
import type { AiTaskResponse } from '@/services/aiService';

// ─── Types ──────────────────────────────────────────────────────────────────

interface EditableMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
}

function resolveVoiceErrorMessage(error: unknown): string {
  const userMessage =
    typeof error === 'object' && error && 'userMessage' in error && typeof (error as { userMessage?: unknown }).userMessage === 'string'
      ? (error as { userMessage: string }).userMessage
      : error instanceof Error
        ? error.message
        : null;

  if (userMessage && userMessage !== 'The service is unavailable right now. Please try again shortly.') {
    return userMessage;
  }

  return 'Voice synthesis is unavailable right now. Check the provider quota or credentials and try again.';
}

function getBrowserSpeechSynthesis(): SpeechSynthesis | null {
  if (typeof window === 'undefined') {
    return null;
  }

  const synthesis = window.speechSynthesis;
  return synthesis ?? null;
}

function playWithBrowserSpeech(
  speechText: string,
  locale: string,
  onStart: () => void,
  onEnd: () => void,
  onError: (message: string) => void,
): () => void {
  const synthesis = getBrowserSpeechSynthesis();
  if (!synthesis) {
    throw new Error('Browser speech synthesis is unavailable.');
  }

  synthesis.cancel();

  const utterance = new SpeechSynthesisUtterance(speechText);
  utterance.lang = locale;

  utterance.onstart = () => {
    onStart();
  };

  utterance.onend = () => {
    onEnd();
  };

  utterance.onerror = (event) => {
    onError(event.error || 'Browser speech synthesis failed.');
  };

  try {
    synthesis.speak(utterance);
  } catch (error) {
    throw error instanceof Error ? error : new Error('Browser speech synthesis failed.');
  }

  return () => {
    utterance.onstart = null;
    utterance.onend = null;
    utterance.onerror = null;
    synthesis.cancel();
  };
}

// ─── Types ──────────────────────────────────────────────────────────────────

type PlaygroundMode = 'agent' | 'task';

// ─── Constants ──────────────────────────────────────────────────────────────

const breadcrumbItems = [
  { label: 'AI', href: '/ai/agents' },
  { label: 'AI Playground', icon: <FlaskConical className="h-3.5 w-3.5" /> },
];

// ─── Page ───────────────────────────────────────────────────────────────────

export function AiPlaygroundPage() {
  const [mode, setMode] = useState<'single' | 'compare'>('single');
  const [playgroundMode, setPlaygroundMode] = useState<PlaygroundMode>('agent');
  const [allTools, setAllTools] = useState<string[]>([]);
  const [selectedAgentConfig, setSelectedAgentConfig] = useState<AgentConfigurationResponse | null>(null);
  const [selectedTask, setSelectedTask] = useState<AiTaskResponse | null>(null);
  const defaultPromptRef = useRef<string | null>(null);
  const compareRef = useRef<ModelComparisonViewHandle>(null);
  const previewAudioRef = useRef<HTMLAudioElement | null>(null);
  const previewAudioUrlRef = useRef<string | null>(null);
  const browserSpeechCancelRef = useRef<(() => void) | null>(null);
  const activeSpeechMessageIdRef = useRef<string | null>(null);
  const [voiceModeEnabled, setVoiceModeEnabled] = useState(false);
  const [voicePlaybackState, setVoicePlaybackState] = useState<'idle' | 'loading' | 'playing' | 'error'>('idle');
  const [voiceError, setVoiceError] = useState<string | null>(null);
  const [voiceDetails, setVoiceDetails] = useState<{
    speechText: string;
    provider: string | null;
    voiceId: string | null;
    aiRunId: string | null;
  } | null>(null);

  // Editable user/assistant messages (system prompt is in config)
  const [editableMessages, setEditableMessages] = useState<EditableMessage[]>([
    { id: `msg-${Date.now()}`, role: 'user', content: '' },
  ]);

  // ── Frontend tools (same as main chat AG-UI process) ────────────────────
  const frontendTools = useMemo(() => {
    return createPlaygroundFrontendTools();
  }, []);

  const {
    config,
    updateConfig,
    output,
    outputParts,
    isStreaming,
    streamError,
    metrics,
    runHistory,
    speechRender,
    submitMessages,
    stopStreaming,
    resetChat,
    addRunRecord,
    clearHistory,
  } = usePlaygroundChat(frontendTools);

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
        setSelectedAgentConfig(agentConfig);
        defaultPromptRef.current = agentConfig.instructionsText ?? '';

        updateConfig({
          agentName,
          systemPrompt: agentConfig.instructionsText ?? '',
          enabledToolNames: tools,
          modelId: agentConfig.modelId ?? null,
          modelName: agentConfig.modelName ?? null,
        });
      } else {
        setAllTools([]);
        setSelectedAgentConfig(null);
        defaultPromptRef.current = null;

        updateConfig({
          agentName: null,
          systemPrompt: '',
          enabledToolNames: [],
          modelId: null,
          modelName: null,
        });
      }
      resetChat();
    },
    [updateConfig, resetChat],
  );

  // ── AI Task change handler ──────────────────────────────────────────────

  const handleTaskChange = useCallback(
    (taskId: string | null, task?: AiTaskResponse) => {
      if (task) {
        setSelectedTask(task);
        updateConfig({
          aiTaskId: taskId,
          aiTaskName: task.displayName,
          agentName: null,
          systemPrompt: task.systemTemplate ?? '',
          enabledToolNames: [],
          promptVariables: {},
        });
      } else {
        setSelectedTask(null);
        updateConfig({
          aiTaskId: null,
          aiTaskName: null,
          systemPrompt: '',
          promptVariables: {},
        });
      }
      resetChat();
    },
    [updateConfig, resetChat],
  );

  // ── Playground mode switch handler ─────────────────────────────────────

  const handlePlaygroundModeChange = useCallback(
    (newMode: PlaygroundMode) => {
      setPlaygroundMode(newMode);
      // Clear selections when switching modes
      setSelectedAgentConfig(null);
      setSelectedTask(null);
      setAllTools([]);
      defaultPromptRef.current = null;
      updateConfig({
        agentName: null,
        aiTaskId: null,
        aiTaskName: null,
        systemPrompt: '',
        enabledToolNames: [],
        modelId: null,
        modelName: null,
        promptVariables: {},
      });
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

  const stopVoicePreview = useCallback(() => {
    browserSpeechCancelRef.current?.();
    browserSpeechCancelRef.current = null;

    const synthesis = getBrowserSpeechSynthesis();
    synthesis?.cancel();

    previewAudioRef.current?.pause();
    previewAudioRef.current?.removeAttribute('src');
    previewAudioRef.current = null;

    if (previewAudioUrlRef.current) {
      URL.revokeObjectURL(previewAudioUrlRef.current);
      previewAudioUrlRef.current = null;
    }

    setVoicePlaybackState('idle');
  }, []);

  useEffect(() => {
    return () => {
      stopVoicePreview();
    };
  }, [stopVoicePreview]);

  useEffect(() => {
    if (!voiceModeEnabled || !speechRender || isStreaming) {
      return;
    }

    if (activeSpeechMessageIdRef.current === speechRender.messageId) {
      return;
    }

    activeSpeechMessageIdRef.current = speechRender.messageId;
    let cancelled = false;

    const playVoice = async () => {
      setVoicePlaybackState('loading');
      setVoiceError(null);
      setVoiceDetails({
        speechText: speechRender.speechText,
        provider: null,
        voiceId: null,
        aiRunId: null,
      });

      try {
        const locale = 'en-US';
        let response: Awaited<ReturnType<typeof textToSpeechSettingsService.synthesize>> | null = null;

        try {
          response = await textToSpeechSettingsService.synthesize({
            speechText: speechRender.speechText,
            locale,
            threadId: `playground-${Date.now()}`,
            messageId: speechRender.messageId,
          });
        } catch (primaryError) {
          const fallbackSynthesis = getBrowserSpeechSynthesis();
          if (!fallbackSynthesis) {
            throw primaryError;
          }

          const cancelBrowserSpeech = playWithBrowserSpeech(
            speechRender.speechText,
            locale,
            () => {
              if (!cancelled) {
                setVoicePlaybackState('playing');
              }
            },
            () => {
              browserSpeechCancelRef.current = null;
              if (!cancelled) {
                setVoicePlaybackState('idle');
              }
            },
            (message) => {
              browserSpeechCancelRef.current = null;
              if (!cancelled) {
                setVoicePlaybackState('error');
                setVoiceError(message);
              }
            },
          );

          if (cancelled) {
            cancelBrowserSpeech();
            return;
          }

          browserSpeechCancelRef.current = cancelBrowserSpeech;
          setVoiceDetails({
            speechText: speechRender.speechText,
            provider: 'Browser',
            voiceId: locale,
            aiRunId: null,
          });
          return;
        }

        if (cancelled) {
          return;
        }

        stopVoicePreview();

        const audioUrl = URL.createObjectURL(response.audioBlob);
        const audio = new Audio(audioUrl);
        previewAudioRef.current = audio;
        previewAudioUrlRef.current = audioUrl;

        audio.onended = () => {
          if (previewAudioUrlRef.current === audioUrl) {
            URL.revokeObjectURL(audioUrl);
            previewAudioUrlRef.current = null;
          }
          previewAudioRef.current = null;
          setVoicePlaybackState('idle');
        };

        audio.onerror = () => {
          if (previewAudioUrlRef.current === audioUrl) {
            URL.revokeObjectURL(audioUrl);
            previewAudioUrlRef.current = null;
          }
          previewAudioRef.current = null;
          setVoicePlaybackState('error');
          setVoiceError('Voice playback failed.');
        };

        setVoiceDetails({
          speechText: speechRender.speechText,
          provider: response.provider,
          voiceId: response.voiceId,
          aiRunId: response.aiRunId,
        });

        await audio.play();
        if (!cancelled) {
          setVoicePlaybackState('playing');
        }
      } catch (error: unknown) {
        if (cancelled) {
          return;
        }

        setVoicePlaybackState('error');
        setVoiceError(resolveVoiceErrorMessage(error));
      }
    };

    void playVoice();

    return () => {
      cancelled = true;
    };
  }, [voiceModeEnabled, speechRender, isStreaming, stopVoicePreview]);

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

  // ── Ctrl+Enter to submit (single mode only) ─────────────────────────────

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (mode !== 'single') return;
      if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
        e.preventDefault();
        handleSubmit();
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [handleSubmit, mode]);

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
    stopVoicePreview();
    setVoiceError(null);
    setVoiceDetails(null);
    activeSpeechMessageIdRef.current = null;
    resetChat();
    // Also reset compare view's internal chat hooks
    compareRef.current?.resetBoth();
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
        <ModelComparisonView ref={compareRef} sharedConfig={config} onRunRecorded={addRunRecord} />
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

      {/* Config bar: mode toggle + agent/task picker + model + popover triggers */}
      <div className="flex items-center gap-3 border-b border-[var(--color-border-light)] px-6 py-2.5">
        {/* Mode toggle: Agent | AI Task */}
        <div className="flex rounded-md border border-[var(--color-border-light)]">
          <button
            className={`flex items-center gap-1.5 px-3 py-1.5 text-xs transition-colors ${
              playgroundMode === 'agent'
                ? 'bg-[var(--color-brand-primary)] text-white'
                : 'text-[var(--color-text-secondary)] hover:bg-[var(--color-background)]'
            } rounded-l-md`}
            onClick={() => handlePlaygroundModeChange('agent')}
          >
            <Bot className="h-3.5 w-3.5" />
            Agent
          </button>
          <button
            className={`flex items-center gap-1.5 px-3 py-1.5 text-xs transition-colors ${
              playgroundMode === 'task'
                ? 'bg-[var(--color-brand-primary)] text-white'
                : 'text-[var(--color-text-secondary)] hover:bg-[var(--color-background)]'
            } rounded-r-md`}
            onClick={() => handlePlaygroundModeChange('task')}
          >
            <ListChecks className="h-3.5 w-3.5" />
            AI Task
          </button>
        </div>

        {/* Conditionally render Agent or Task picker */}
        {playgroundMode === 'agent' ? (
          <AgentPicker compact value={config.agentName} onChange={handleAgentChange} />
        ) : (
          <AiTaskPicker compact value={config.aiTaskId} onChange={handleTaskChange} />
        )}

        <ModelSelector
          compact
          value={config.modelId}
          onChange={(id, name) => updateConfig({ modelId: id, modelName: name ?? null })}
        />

        <div className="mx-1 h-5 w-px bg-[var(--color-border-light)]" />

        <div className="flex items-center gap-2 rounded-md border border-[var(--color-border-light)] px-3 py-1.5">
          <Volume2 className="h-3.5 w-3.5 text-[var(--color-text-secondary)]" />
          <Label htmlFor="playground-voice-mode" className="text-xs text-[var(--color-text-secondary)]">
            Voice mode
          </Label>
          <Switch
            id="playground-voice-mode"
            checked={voiceModeEnabled}
            onCheckedChange={(checked) => {
              setVoiceModeEnabled(checked);
              if (!checked) {
                stopVoicePreview();
                setVoiceError(null);
                setVoiceDetails(null);
                activeSpeechMessageIdRef.current = null;
              }
            }}
          />
          {voicePlaybackState === 'loading' && (
            <Loader2 className="h-3.5 w-3.5 animate-spin text-[var(--color-text-tertiary)]" />
          )}
          {voicePlaybackState === 'playing' && (
            <span className="text-[10px] text-[var(--color-text-tertiary)]">Playing</span>
          )}
        </div>

        {/* Tools popover (agent mode only) */}
        {playgroundMode === 'agent' && (
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
        )}

        {/* Prompt Variables popover (AI Task mode) */}
        {playgroundMode === 'task' && selectedTask && (
          <Popover>
            <PopoverTrigger asChild>
              <Button variant="ghost" size="sm" className="h-8 text-xs">
                <Variable className="mr-1.5 h-3.5 w-3.5" />
                Variables
                {Object.keys(config.promptVariables).length > 0 && (
                  <span className="ml-1.5 rounded-full bg-[var(--color-brand-primary)] px-1.5 py-0.5 text-[10px] text-white">
                    {Object.keys(config.promptVariables).length}
                  </span>
                )}
              </Button>
            </PopoverTrigger>
            <PopoverContent align="start" className="w-96">
              <PromptVariablesForm
                variablesSchema={selectedTask.variablesSchemaJson}
                variables={config.promptVariables}
                onChange={(vars) => updateConfig({ promptVariables: vars })}
              />
            </PopoverContent>
          </Popover>
        )}

        {/* User Brief popover (agent mode) */}
        {playgroundMode === 'agent' && (
          <Popover>
            <PopoverTrigger asChild>
              <Button variant="ghost" size="sm" className="h-8 text-xs">
                <Variable className="mr-1.5 h-3.5 w-3.5" />
                User Brief
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
        )}

        {/* Agent context viewer (agent mode only) */}
        {playgroundMode === 'agent' && (
          <AgentContextDrawer
            agentConfig={selectedAgentConfig}
            currentUserBriefJson={config.userBriefJson}
          />
        )}

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
          onDefaultPromptSaved={(saved) => { defaultPromptRef.current = saved; }}
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
        outputParts={outputParts}
        isStreaming={isStreaming}
        streamError={streamError}
        metrics={metrics}
        modelName={config.modelName}
        voiceModeEnabled={voiceModeEnabled}
        voicePlaybackState={voicePlaybackState}
        voiceError={voiceError}
        voiceDetails={voiceDetails}
        onStopVoice={stopVoicePreview}
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
            Test agents, AI tasks, prompts, and models interactively.
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
