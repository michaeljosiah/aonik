import { useCallback, useState, useRef, useEffect } from 'react';
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
import { streamPlaygroundReview } from '@/lib/playground-client';
import type { PlaygroundReviewResult } from '@/types/ai';
import { useAuth } from '@/auth';
import { AgentPicker } from './playground/AgentPicker';
import { AiTaskPicker } from './playground/AiTaskPicker';
import { PromptVariablesForm } from './playground/PromptVariablesForm';
import { ModelSelector } from './playground/ModelSelector';
import { UserBriefPicker } from './playground/UserBriefPicker';
import { ToolToggleList } from './playground/ToolToggleList';
import { AgentContextDrawer } from './playground/AgentContextDrawer';
import { ScenarioPicker } from './playground/ScenarioPicker';
import { PlaygroundMessageBlock } from './playground/PlaygroundMessageBlock';
import { PlaygroundOutputPanel } from './playground/PlaygroundOutputPanel';
import { ModelComparisonView, type ModelComparisonViewHandle } from './playground/ModelComparisonView';
import { RunHistoryPanel } from './playground/RunHistoryPanel';
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
import type { AgentConfigurationResponse, PlaygroundScenarioResponse } from '@/types/ai';
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
  const [playedChunkCount, setPlayedChunkCount] = useState(0);
  const chunkPlaybackBusyRef = useRef<boolean>(false);
  const guidancePlayedRef = useRef<boolean>(false);
  const voiceModeEnabledRef = useRef<boolean>(false);
  const synthesizedChunksRef = useRef<
    Map<number, Promise<Awaited<ReturnType<typeof textToSpeechSettingsService.synthesize>>>>
  >(new Map());
  const [voiceModeEnabled, setVoiceModeEnabled] = useState(false);
  const [voicePlaybackState, setVoicePlaybackState] = useState<'idle' | 'loading' | 'playing' | 'error'>('idle');
  const [voiceError, setVoiceError] = useState<string | null>(null);
  const [voiceDetails, setVoiceDetails] = useState<{
    speechText: string;
    provider: string | null;
    voiceId: string | null;
    aiRunId: string | null;
  } | null>(null);

  // Review state
  const [isReviewing, setIsReviewing] = useState(false);
  const [reviewResult, setReviewResult] = useState<PlaygroundReviewResult | null>(null);
  const [reviewRawText, setReviewRawText] = useState<string | null>(null);
  const [reviewError, setReviewError] = useState<string | null>(null);
  const reviewAbortRef = useRef<AbortController | null>(null);
  const { getAccessToken: getReviewAccessToken } = useAuth();

  // Editable user/assistant messages (system prompt is in config)
  const [editableMessages, setEditableMessages] = useState<EditableMessage[]>([
    { id: `msg-${Date.now()}`, role: 'user', content: '' },
  ]);

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
    speechChunks,
    submitMessages,
    stopStreaming,
    resetChat,
    addRunRecord,
    clearHistory,
    approveToolCall,
    rejectToolCall,
    selectToolCallOptions,
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

    chunkPlaybackBusyRef.current = false;
    setVoicePlaybackState('idle');
  }, []);

  // Keep voiceModeEnabled accessible inside in-flight async playback closures.
  useEffect(() => {
    voiceModeEnabledRef.current = voiceModeEnabled;
  }, [voiceModeEnabled]);

  useEffect(() => {
    return () => {
      stopVoicePreview();
    };
  }, [stopVoicePreview]);

  // Reset chunk playback counters when a new stream starts.
  useEffect(() => {
    if (isStreaming) {
      setPlayedChunkCount(0);
      guidancePlayedRef.current = false;
      activeSpeechMessageIdRef.current = null;
      synthesizedChunksRef.current.clear();
    }
  }, [isStreaming]);

  // Prefetch queue: kick off TTS synthesis for every arrived chunk in parallel,
  // so the audio for chunk N+1 is already buffered by the time chunk N's audio
  // finishes playing. Eliminates the synthesize-latency gap between chunks.
  useEffect(() => {
    if (!voiceModeEnabled) return;

    speechChunks.forEach((chunk, index) => {
      if (synthesizedChunksRef.current.has(index)) return;
      const text = chunk.speechText?.trim();
      if (!text) return;

      const promise = textToSpeechSettingsService.synthesize({
        speechText: text,
        locale: 'en-US',
        threadId: `playground-${chunk.messageId || Date.now()}`,
        messageId: chunk.messageId || `chunk-${index}`,
      });
      promise.catch(() => {});
      synthesizedChunksRef.current.set(index, promise);
    });
  }, [voiceModeEnabled, speechChunks]);

  // Sequential chunk player. Fires whenever new chunks arrive, when the
  // stream finishes (to play guidance), or when voice mode toggles on.
  useEffect(() => {
    if (!voiceModeEnabled) return;
    if (chunkPlaybackBusyRef.current) return;

    const nextChunk = speechChunks[playedChunkCount];
    const guidanceText = speechRender?.speechText?.trim() ?? '';
    const shouldPlayGuidance =
      !isStreaming
      && !guidancePlayedRef.current
      && !!speechRender
      && guidanceText.length > 0
      && playedChunkCount >= speechChunks.length;

    if (!nextChunk && !shouldPlayGuidance) return;

    const messageId = nextChunk?.messageId ?? speechRender?.messageId ?? '';
    const speechText = nextChunk?.speechText ?? guidanceText;
    if (!speechText) {
      if (nextChunk) setPlayedChunkCount((n) => n + 1);
      if (shouldPlayGuidance) guidancePlayedRef.current = true;
      return;
    }

    activeSpeechMessageIdRef.current = messageId;
    chunkPlaybackBusyRef.current = true;

    const advance = () => {
      chunkPlaybackBusyRef.current = false;
      if (nextChunk) {
        setPlayedChunkCount((n) => n + 1);
      } else {
        guidancePlayedRef.current = true;
      }
      if (voiceModeEnabledRef.current) {
        setVoicePlaybackState('idle');
      }
    };

    const playChunk = async () => {
      setVoicePlaybackState('loading');
      setVoiceError(null);
      setVoiceDetails({
        speechText,
        provider: null,
        voiceId: null,
        aiRunId: null,
      });

      try {
        const locale = 'en-US';
        let response: Awaited<ReturnType<typeof textToSpeechSettingsService.synthesize>> | null = null;

        try {
          // For arrived chunks the prefetch effect has already kicked off
          // synthesis; reuse that promise. Guidance and any chunk missed by
          // prefetch (voice mode toggled on mid-stream edge case) synthesize on
          // demand here.
          let synthesisPromise = nextChunk
            ? synthesizedChunksRef.current.get(playedChunkCount)
            : undefined;
          if (!synthesisPromise) {
            synthesisPromise = textToSpeechSettingsService.synthesize({
              speechText,
              locale,
              threadId: `playground-${messageId || Date.now()}`,
              messageId: messageId || `chunk-${Date.now()}`,
            });
            if (nextChunk) {
              synthesisPromise.catch(() => {});
              synthesizedChunksRef.current.set(playedChunkCount, synthesisPromise);
            }
          }
          response = await synthesisPromise;
        } catch (primaryError) {
          const fallbackSynthesis = getBrowserSpeechSynthesis();
          if (!fallbackSynthesis) throw primaryError;

          if (!voiceModeEnabledRef.current) {
            chunkPlaybackBusyRef.current = false;
            return;
          }

          const cancelBrowserSpeech = playWithBrowserSpeech(
            speechText,
            locale,
            () => { if (voiceModeEnabledRef.current) setVoicePlaybackState('playing'); },
            () => {
              browserSpeechCancelRef.current = null;
              advance();
            },
            (message) => {
              browserSpeechCancelRef.current = null;
              if (voiceModeEnabledRef.current) {
                setVoicePlaybackState('error');
                setVoiceError(message);
              }
              chunkPlaybackBusyRef.current = false;
            },
          );

          browserSpeechCancelRef.current = cancelBrowserSpeech;
          setVoiceDetails({
            speechText,
            provider: 'Browser',
            voiceId: locale,
            aiRunId: null,
          });
          return;
        }

        if (!voiceModeEnabledRef.current) {
          chunkPlaybackBusyRef.current = false;
          return;
        }

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
          advance();
        };

        audio.onerror = () => {
          if (previewAudioUrlRef.current === audioUrl) {
            URL.revokeObjectURL(audioUrl);
            previewAudioUrlRef.current = null;
          }
          previewAudioRef.current = null;
          if (voiceModeEnabledRef.current) {
            setVoicePlaybackState('error');
            setVoiceError('Voice playback failed.');
          }
          chunkPlaybackBusyRef.current = false;
        };

        setVoiceDetails({
          speechText,
          provider: response.provider,
          voiceId: response.voiceId,
          aiRunId: response.aiRunId,
        });

        await audio.play();
        if (voiceModeEnabledRef.current) {
          setVoicePlaybackState('playing');
        }
      } catch (error: unknown) {
        if (voiceModeEnabledRef.current) {
          setVoicePlaybackState('error');
          setVoiceError(resolveVoiceErrorMessage(error));
        }
        chunkPlaybackBusyRef.current = false;
      }
    };

    void playChunk();
  }, [voiceModeEnabled, speechChunks, speechRender, isStreaming, playedChunkCount]);

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

  // ── AI Review handler ───────────────────────────────────────────────────

  const handleReview = useCallback(async () => {
    if (isReviewing || !output) return;

    setIsReviewing(true);
    setReviewResult(null);
    setReviewRawText(null);
    setReviewError(null);

    const controller = new AbortController();
    reviewAbortRef.current = controller;

    // Build tool calls from outputParts
    const toolCalls = outputParts
      .filter((p): p is Extract<typeof p, { type: 'tool-call' }> => p.type === 'tool-call')
      .map((p) => ({
        toolName: p.toolCall.toolCallName,
        arguments: p.toolCall.args,
        result: p.toolCall.result,
      }));

    // Build messages from editable messages
    const msgs = editableMessages
      .filter((m) => m.content.trim())
      .map((m) => ({ role: m.role as 'user' | 'assistant', content: m.content }));

    let rawTextAccumulator = '';

    try {
      await streamPlaygroundReview({
        request: {
          systemPrompt: config.systemPrompt || undefined,
          userBriefJson: config.userBriefJson ?? undefined,
          messages: msgs,
          assistantResponse: output,
          toolCalls: toolCalls.length > 0 ? toolCalls : undefined,
          modelId: config.modelId ?? undefined,
        },
        callbacks: {
          onReviewDelta: (delta) => {
            rawTextAccumulator += delta;
            setReviewRawText(rawTextAccumulator);
          },
          onReviewFinished: (parsed) => {
            if (parsed && typeof parsed === 'object') {
              const p = parsed as Record<string, unknown>;
              setReviewResult({
                overallScore: (p.overallScore as number) ?? 0,
                metrics: (p.metrics as PlaygroundReviewResult['metrics']) ?? [],
                strengths: (p.strengths as string[]) ?? [],
                suggestions: (p.suggestions as string[]) ?? [],
                promptImprovements: (p.promptImprovements as string[]) ?? [],
              });
              setReviewRawText(null); // Clear raw text since we have structured result
            }
          },
          onReviewError: (message) => {
            setReviewError(message);
          },
        },
        getAccessToken: getReviewAccessToken,
        signal: controller.signal,
      });
    } catch (err) {
      if ((err as Error).name !== 'AbortError') {
        setReviewError((err as Error).message);
      }
    } finally {
      setIsReviewing(false);
      reviewAbortRef.current = null;
    }
  }, [isReviewing, output, outputParts, editableMessages, config, getReviewAccessToken]);

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
    // Clear review state
    reviewAbortRef.current?.abort();
    setIsReviewing(false);
    setReviewResult(null);
    setReviewRawText(null);
    setReviewError(null);
    resetChat();
    // Also reset compare view's internal chat hooks
    compareRef.current?.resetBoth();
  };

  // ── Load scenario handler ───────────────────────────────────────────────

  const handleLoadScenario = useCallback(
    (scenario: PlaygroundScenarioResponse) => {
      // Set playground mode based on scenario context
      if (scenario.aiTaskId) {
        setPlaygroundMode('task');
      } else if (scenario.agentName) {
        setPlaygroundMode('agent');
      }

      // Update config with scenario data
      const configUpdates: Record<string, unknown> = {};
      if (scenario.systemPrompt) configUpdates.systemPrompt = scenario.systemPrompt;
      if (scenario.agentName) configUpdates.agentName = scenario.agentName;
      if (scenario.aiTaskId) configUpdates.aiTaskId = scenario.aiTaskId;
      if (scenario.modelId) configUpdates.modelId = scenario.modelId;
      if (scenario.userBriefJson) configUpdates.userBriefJson = scenario.userBriefJson;
      if (scenario.promptVariables) configUpdates.promptVariables = scenario.promptVariables;
      updateConfig(configUpdates);

      // Populate conversation turns
      if (scenario.turns.length > 0) {
        setEditableMessages(
          scenario.turns.map((t, i) => ({
            id: `msg-${Date.now()}-${i}`,
            role: t.role as 'user' | 'assistant',
            content: t.content,
          })),
        );
      }

      // Clear previous output
      resetChat();
    },
    [updateConfig, resetChat],
  );

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

        <div className="mx-1 h-5 w-px bg-[var(--color-border-light)]" />

        {/* Scenarios */}
        <ScenarioPicker
          agentName={config.agentName}
          aiTaskId={config.aiTaskId}
          modelId={config.modelId}
          currentScenarioData={{
            systemPrompt: config.systemPrompt || null,
            userBriefJson: config.userBriefJson || null,
            agentName: config.agentName || null,
            aiTaskId: config.aiTaskId || null,
            modelId: config.modelId || null,
            promptVariables: config.promptVariables,
            turns: editableMessages
              .filter((m) => m.content.trim())
              .map((m) => ({ role: m.role, content: m.content })),
          }}
          onLoad={handleLoadScenario}
        />
      </div>

      {/* Split content area: left = inputs, right = output */}
      <div className="flex min-h-0 flex-1 overflow-hidden">
        {/* Left column: system prompt + messages + submit + run history */}
        <div className="flex min-w-0 flex-1 flex-col overflow-hidden">
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

        {/* Right column: output panel (full height) */}
        <div className="flex w-[45%] shrink-0 flex-col overflow-hidden border-l border-[var(--color-border-light)]">
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
            isReviewing={isReviewing}
            reviewResult={reviewResult}
            reviewRawText={reviewRawText}
            reviewError={reviewError}
            onReview={handleReview}
            onApproveToolCall={approveToolCall}
            onRejectToolCall={rejectToolCall}
            onSelectToolCallOptions={selectToolCallOptions}
            side
          />
        </div>
      </div>
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
