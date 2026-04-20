import { useCallback, useMemo, useRef, useState } from 'react';
import {
  streamPlaygroundRun,
  type PlaygroundRunMetrics,
  type PlaygroundMessage,
} from '@/lib/playground-client';
import { upsertTrailingTextPart } from '@/hooks/playgroundOutputParts';
import { createPlaygroundFrontendTools } from '@/pages/ai/playground/frontendTools';
import type { PlaygroundRunRecord } from '@/types/ai';
import { useAuth } from '@/auth';

export interface PlaygroundConfig {
  agentName: string | null;
  systemPrompt: string;
  modelId: string | null;
  modelName: string | null;
  userBriefJson: string | null;
  enabledToolNames: string[];
  temperature: number;
  maxTokens: number;
  /** AI Task mode fields */
  aiTaskId: string | null;
  aiTaskName: string | null;
  promptVariables: Record<string, string>;
}

// ─── Structured output parts ─────────────────────────────────────────────────

export type PlaygroundToolCallStatus =
  | 'streaming'
  | 'pending'
  | 'awaiting-approval'
  | 'awaiting-selection'
  | 'completed'
  | 'error';

export interface PlaygroundToolCall {
  toolCallId: string;
  toolCallName: string;
  args: string;
  result?: string;
  error?: string;
  status: PlaygroundToolCallStatus;
  /** Populated when status is 'awaiting-approval' (confirmAction tool). */
  approval?: {
    action: string;
    description: string;
    severity: 'low' | 'medium' | 'high';
  };
  /** Populated when status is 'awaiting-selection' (display_option_selector tool). */
  optionSelection?: {
    question: string;
    options: Array<{ label: string; description?: string }>;
    multiSelect: boolean;
  };
}

export interface PlaygroundSpeechRender {
  messageId: string;
  speechText: string;
  requiresVisualAttention: boolean;
  requiresApproval: boolean;
}

export interface PlaygroundSpeechChunk {
  messageId: string;
  chunkIndex: number;
  speechText: string;
  isFinal: boolean;
}

export type PlaygroundOutputPart =
  | { type: 'text'; content: string }
  | { type: 'tool-call'; toolCall: PlaygroundToolCall }
  | { type: 'reasoning'; content: string };

// ─── Chat message (compare mode) ────────────────────────────────────────────

interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
}

export function usePlaygroundChat() {
  const { getAccessToken } = useAuth();

  // Config state
  const [config, setConfig] = useState<PlaygroundConfig>({
    agentName: null,
    systemPrompt: '',
    modelId: null,
    modelName: null,
    userBriefJson: null,
    enabledToolNames: [],
    temperature: 1,
    maxTokens: 2048,
    aiTaskId: null,
    aiTaskName: null,
    promptVariables: {},
  });

  // Single-mode output state
  const [output, setOutput] = useState('');
  const [outputParts, setOutputParts] = useState<PlaygroundOutputPart[]>([]);

  // Chat state (used by compare mode)
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [streamError, setStreamError] = useState<string | null>(null);
  const [metrics, setMetrics] = useState<PlaygroundRunMetrics | null>(null);
  const [runHistory, setRunHistory] = useState<PlaygroundRunRecord[]>([]);
  const [speechRender, setSpeechRender] = useState<PlaygroundSpeechRender | null>(null);
  const [speechChunks, setSpeechChunks] = useState<PlaygroundSpeechChunk[]>([]);

  const abortRef = useRef<AbortController | null>(null);

  // Mutable refs for building structured parts during streaming
  const partsRef = useRef<PlaygroundOutputPart[]>([]);
  const currentTextRef = useRef('');
  const currentReasoningRef = useRef('');
  const toolCallMapRef = useRef(new Map<string, PlaygroundToolCall>());
  const pendingResolversRef = useRef(new Map<string, (result: string) => void>());

  const updateConfig = useCallback(
    (updates: Partial<PlaygroundConfig>) => {
      setConfig((prev) => ({ ...prev, ...updates }));
    },
    [],
  );

  /** Flush any accumulated text or reasoning into a part. */
  const flushText = useCallback(() => {
    if (currentTextRef.current) {
      partsRef.current = upsertTrailingTextPart(partsRef.current, currentTextRef.current);
      currentTextRef.current = '';
    }
  }, []);

  const flushReasoning = useCallback(() => {
    if (currentReasoningRef.current) {
      partsRef.current.push({ type: 'reasoning', content: currentReasoningRef.current });
      currentReasoningRef.current = '';
    }
  }, []);

  /** Snapshot the current parts array into React state. */
  const syncParts = useCallback(() => {
    setOutputParts([...partsRef.current]);
  }, []);

  // ── Frontend tools with React-based approval/selection ───────────────────

  const frontendTools = useMemo(() => {
    return createPlaygroundFrontendTools({
      confirmAction: (toolCallId, args) => {
        return new Promise<string>((resolve) => {
          pendingResolversRef.current.set(toolCallId, resolve);
          const tc = toolCallMapRef.current.get(toolCallId);
          if (tc) {
            tc.status = 'awaiting-approval';
            tc.approval = args;
            syncParts();
          }
        });
      },
      selectOptions: (toolCallId, args) => {
        return new Promise<string>((resolve) => {
          pendingResolversRef.current.set(toolCallId, resolve);
          const tc = toolCallMapRef.current.get(toolCallId);
          if (tc) {
            tc.status = 'awaiting-selection';
            tc.optionSelection = args;
            syncParts();
          }
        });
      },
    });
  }, [syncParts]);

  const approveToolCall = useCallback((toolCallId: string) => {
    const resolver = pendingResolversRef.current.get(toolCallId);
    if (resolver) {
      resolver('approved');
      pendingResolversRef.current.delete(toolCallId);
    }
    const tc = toolCallMapRef.current.get(toolCallId);
    if (tc) {
      tc.status = 'completed';
      tc.result = 'approved';
      syncParts();
    }
  }, [syncParts]);

  const rejectToolCall = useCallback((toolCallId: string) => {
    const resolver = pendingResolversRef.current.get(toolCallId);
    if (resolver) {
      resolver('rejected');
      pendingResolversRef.current.delete(toolCallId);
    }
    const tc = toolCallMapRef.current.get(toolCallId);
    if (tc) {
      tc.status = 'completed';
      tc.result = 'rejected';
      syncParts();
    }
  }, [syncParts]);

  const selectToolCallOptions = useCallback((toolCallId: string, selected: string[]) => {
    const resolver = pendingResolversRef.current.get(toolCallId);
    if (resolver) {
      resolver(selected.length <= 1 ? (selected[0] ?? '') : JSON.stringify(selected));
      pendingResolversRef.current.delete(toolCallId);
    }
    const tc = toolCallMapRef.current.get(toolCallId);
    if (tc) {
      tc.status = 'completed';
      tc.result = selected.join(', ');
      syncParts();
    }
  }, [syncParts]);

  // ── Submit messages (single-mode: message block editor) ────────────────────

  const submitMessages = useCallback(
    async (msgs: PlaygroundMessage[]) => {
      if (isStreaming) return;

      setIsStreaming(true);
      setStreamError(null);
      setMetrics(null);
      setOutput('');
      setOutputParts([]);
      setSpeechRender(null);
      setSpeechChunks([]);
      partsRef.current = [];
      currentTextRef.current = '';
      currentReasoningRef.current = '';
      toolCallMapRef.current.clear();

      const controller = new AbortController();
      abortRef.current = controller;

      let fullResponse = '';

      try {
        await streamPlaygroundRun({
          request: {
            agentName: config.agentName ?? undefined,
            systemPrompt: config.systemPrompt || undefined,
            modelId: config.modelId ?? undefined,
            userBriefJson: config.userBriefJson ?? undefined,
            enabledToolNames: config.enabledToolNames,
            messages: msgs,
            temperature: config.temperature,
            maxTokens: config.maxTokens,
            aiTaskId: config.aiTaskId ?? undefined,
            promptVariables: Object.keys(config.promptVariables).length > 0
              ? config.promptVariables
              : undefined,
          },
          callbacks: {
            onTextDelta: (delta) => {
              fullResponse += delta;
              setOutput(fullResponse);

              // Accumulate into current text buffer
              currentTextRef.current += delta;
              partsRef.current = upsertTrailingTextPart(partsRef.current, currentTextRef.current);
              syncParts();
            },

            onRerun: () => {
              fullResponse = '';
              setOutput('');
              currentTextRef.current = '';
              currentReasoningRef.current = '';
              partsRef.current = partsRef.current.filter((part) => part.type === 'tool-call');
              setOutputParts([...partsRef.current]);
            },

            onSpeechChunk: (payload) => {
              setSpeechChunks((prev) => [...prev, payload]);
            },

            onSpeechRender: (payload) => {
              setSpeechRender(payload);
            },

            onReasoningDelta: (delta) => {
              // Flush any pending text so reasoning appears in order
              flushText();

              currentReasoningRef.current += delta;
              // Update parts: replace or add trailing reasoning part
              const parts = partsRef.current;
              const lastPart = parts[parts.length - 1];
              if (lastPart && lastPart.type === 'reasoning') {
                lastPart.content = currentReasoningRef.current;
              } else {
                parts.push({ type: 'reasoning', content: currentReasoningRef.current });
              }
              syncParts();
            },

            onReasoningEnd: () => {
              flushReasoning();
              syncParts();
            },

            onToolCallStart: (toolCallId, toolName) => {
              // Flush any pending text so tool call appears in order
              flushText();
              currentTextRef.current = '';

              const tc: PlaygroundToolCall = {
                toolCallId,
                toolCallName: toolName,
                args: '',
                status: 'streaming',
              };
              toolCallMapRef.current.set(toolCallId, tc);
              partsRef.current.push({ type: 'tool-call', toolCall: tc });
              syncParts();
            },

            onToolCallArgs: (toolCallId, argsDelta) => {
              const tc = toolCallMapRef.current.get(toolCallId);
              if (tc) {
                tc.args += argsDelta;
                syncParts();
              }
            },

            onToolCallEnd: (toolCallId) => {
              const tc = toolCallMapRef.current.get(toolCallId);
               if (tc && tc.status === 'streaming') {
                 tc.status = 'pending';
                 syncParts();
               }
             },

            onToolResult: (toolCallId, content) => {
              const tc = toolCallMapRef.current.get(toolCallId);
              if (tc) {
                tc.result = content;
                tc.status = 'completed';
                syncParts();
              }
            },

            onRunFinished: (runMetrics) => {
              // Flush any trailing text/reasoning
              flushText();
              flushReasoning();
              syncParts();

              setMetrics(runMetrics);

              const userMsg = msgs.filter((m) => m.role === 'user').pop();
              const record: PlaygroundRunRecord = {
                id: `run-${Date.now()}`,
                timestamp: new Date(),
                modelId: config.modelId ?? undefined,
                modelName: runMetrics.modelName ?? config.modelName ?? undefined,
                agentName: config.agentName ?? undefined,
                systemPrompt: config.systemPrompt,
                userMessage: userMsg?.content ?? '',
                assistantResponse: fullResponse,
                metrics: runMetrics,
              };
              setRunHistory((prev) => [record, ...prev]);
            },
            onRunError: (message) => {
              setStreamError(message);
            },
          },
          getAccessToken,
          signal: controller.signal,
          frontendTools,
        });
      } catch (err) {
        if ((err as Error).name !== 'AbortError') {
          setStreamError((err as Error).message);
        }
      } finally {
        setIsStreaming(false);
        abortRef.current = null;
      }
    },
    [config, isStreaming, getAccessToken, frontendTools, flushText, flushReasoning, syncParts],
  );

  // ── Send message (compare-mode: chat-style) ───────────────────────────────

  const sendMessage = useCallback(
    async (text: string) => {
      if (!text.trim() || isStreaming) return;

      const userMsg: ChatMessage = {
        id: `user-${Date.now()}`,
        role: 'user',
        content: text.trim(),
      };

      const assistantMsg: ChatMessage = {
        id: `assistant-${Date.now()}`,
        role: 'assistant',
        content: '',
      };

      setMessages((prev) => [...prev, userMsg, assistantMsg]);
      setIsStreaming(true);
      setStreamError(null);
      setMetrics(null);
      setSpeechRender(null);
      setSpeechChunks([]);

      const controller = new AbortController();
      abortRef.current = controller;

      const playgroundMessages: PlaygroundMessage[] = [
        ...messages.map((m) => ({ role: m.role, content: m.content })),
        { role: 'user' as const, content: text.trim() },
      ];

      let fullResponse = '';

      try {
        await streamPlaygroundRun({
          request: {
            agentName: config.agentName ?? undefined,
            systemPrompt: config.systemPrompt || undefined,
            modelId: config.modelId ?? undefined,
            userBriefJson: config.userBriefJson ?? undefined,
            enabledToolNames: config.enabledToolNames,
            messages: playgroundMessages,
            temperature: config.temperature,
            maxTokens: config.maxTokens,
            aiTaskId: config.aiTaskId ?? undefined,
            promptVariables: Object.keys(config.promptVariables).length > 0
              ? config.promptVariables
              : undefined,
          },
          callbacks: {
            onTextDelta: (delta) => {
              fullResponse += delta;
              setMessages((prev) => {
                const updated = [...prev];
                const last = updated[updated.length - 1];
                if (last?.role === 'assistant') {
                  updated[updated.length - 1] = {
                    ...last,
                    content: last.content + delta,
                  };
                }
                return updated;
              });
            },
            onSpeechChunk: (payload) => {
              setSpeechChunks((prev) => [...prev, payload]);
            },

            onSpeechRender: (payload) => {
              setSpeechRender(payload);
            },
            onRunFinished: (runMetrics) => {
              setMetrics(runMetrics);

              const record: PlaygroundRunRecord = {
                id: `run-${Date.now()}`,
                timestamp: new Date(),
                modelId: config.modelId ?? undefined,
                modelName: runMetrics.modelName ?? config.modelName ?? undefined,
                agentName: config.agentName ?? undefined,
                systemPrompt: config.systemPrompt,
                userMessage: text.trim(),
                assistantResponse: fullResponse,
                metrics: runMetrics,
              };
              setRunHistory((prev) => [record, ...prev]);
            },
            onRunError: (message) => {
              setStreamError(message);
            },
          },
          getAccessToken,
          signal: controller.signal,
          frontendTools,
        });
      } catch (err) {
        if ((err as Error).name !== 'AbortError') {
          setStreamError((err as Error).message);
        }
      } finally {
        setIsStreaming(false);
        abortRef.current = null;
      }
    },
    [config, messages, isStreaming, getAccessToken, frontendTools],
  );

  const stopStreaming = useCallback(() => {
    abortRef.current?.abort();
  }, []);

  const resetChat = useCallback(() => {
    abortRef.current?.abort();
    setMessages([]);
    setOutput('');
    setOutputParts([]);
    setMetrics(null);
    setStreamError(null);
    setSpeechRender(null);
    setSpeechChunks([]);
    partsRef.current = [];
    currentTextRef.current = '';
    currentReasoningRef.current = '';
    toolCallMapRef.current.clear();
    pendingResolversRef.current.clear();
  }, []);

  const addRunRecord = useCallback((record: PlaygroundRunRecord) => {
    setRunHistory((prev) => [record, ...prev]);
  }, []);

  const clearHistory = useCallback(() => {
    setRunHistory([]);
  }, []);

  return {
    config,
    updateConfig,
    output,
    outputParts,
    messages,
    isStreaming,
    streamError,
    metrics,
    runHistory,
    speechRender,
    speechChunks,
    submitMessages,
    sendMessage,
    stopStreaming,
    resetChat,
    addRunRecord,
    clearHistory,
    approveToolCall,
    rejectToolCall,
    selectToolCallOptions,
  };
}
