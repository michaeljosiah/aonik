import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  streamPlaygroundRun,
  type PlaygroundRunMetrics,
  type PlaygroundMessage,
  type ServerApprovalEventPayload,
} from '@/lib/playground-client';
import { upsertTrailingTextPart } from '@/hooks/playgroundOutputParts';
import { createPlaygroundFrontendTools } from '@/pages/ai/playground/frontendTools';
import type { ServerApprovalState } from '@/components/ai/chatSupport';
import type { PlaygroundRunRecord } from '@/types/ai';
import { api } from '@/lib/api';
import { useAuth } from '@/auth';

export interface PlaygroundConfig {
  agentName: string | null;
  systemPrompt: string;
  modelId: string | null;
  modelName: string | null;
  userBriefJson: string | null;
  /**
   * When set, the playground impersonates this user for the duration of the
   * run — every service / sub-agent tool that reads ICurrentUserContext.UserId
   * targets this user's data instead of the calling admin's. Populated by
   * the UserBriefPicker when a real customer is selected.
   */
  impersonateUserId: string | null;
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
  followUpSuggestions?: {
    prompt?: string;
    suggestions: Array<{ label: string; prompt: string; description?: string }>;
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
  | { type: 'reasoning'; content: string }
  // Spec 032 — a server-owned approval card for a gated Medium/High mutation. The decision
  // routes to the backend (POST /ai/tool-approvals/{id}/decide); the card only presents and collects.
  | { type: 'approval'; approval: ServerApprovalState };

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
    impersonateUserId: null,
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

  // Spec 032 — server-owned approval cards keyed by approvalRequestId, so decideServerApproval
  // can flip their status in place after the user clicks Approve/Reject.
  const approvalPartsRef = useRef(new Map<string, ServerApprovalState>());
  // The single-mode message set that produced the current run. A Medium approval re-runs the
  // agent by appending a nudge to this set so the gate consumes the approval and runs the tool.
  const lastSubmittedMessagesRef = useRef<PlaygroundMessage[] | null>(null);
  // Like useAguiChat: a Medium card can be approved while the run is still streaming, but
  // submitMessages drops calls mid-stream, so stash the retry and flush it once idle.
  const pendingApprovalRetryRef = useRef<string | null>(null);
  const isStreamingRef = useRef(isStreaming);
  isStreamingRef.current = isStreaming;

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
      // Spec 032: mutating tools are now gated server-side and the gate surfaces its own
      // ServerApprovalCard via the tool.approval.* CUSTOM events (handled in submitMessages /
      // sendMessage below). The legacy confirmAction frontend tool is therefore redundant for the
      // Medium/High path, so we do not declare it to the model — mirroring useAguiChat. The
      // confirmAction handler is still passed (harmless) but the tool itself is excluded.
      includeConfirmAction: false,
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

  // ── Server-owned approval cards (Spec 032) ─────────────────────────────────

  /** Append a server approval card part for a gated mutation (idempotent on approvalRequestId). */
  const appendApprovalPart = useCallback(
    (payload: ServerApprovalEventPayload) => {
      const id = `approval-${payload.approvalRequestId}`;
      if (approvalPartsRef.current.has(id)) return;

      const approval: ServerApprovalState = {
        id,
        kind: payload.kind,
        approvalRequestId: payload.approvalRequestId,
        proposalId: payload.proposalId,
        toolCallId: payload.toolCallId,
        tool: payload.tool,
        tier: payload.tier,
        actionKind: payload.actionKind,
        status: 'pending',
      };
      approvalPartsRef.current.set(id, approval);
      partsRef.current.push({ type: 'approval', approval });
      syncParts();
    },
    [syncParts],
  );

  /** Mutate a tracked approval card's status in place and re-render. */
  const setApprovalStatus = useCallback(
    (id: string, status: ServerApprovalState['status'], message?: string) => {
      const approval = approvalPartsRef.current.get(id);
      if (!approval) return;
      approval.status = status;
      if (message !== undefined) approval.message = message;
      syncParts();
    },
    [syncParts],
  );

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
      approvalPartsRef.current.clear();
      // Remember the messages that drove this run so a Medium approval can re-run the agent by
      // appending a nudge (the gate consumes the args-hash-bound approval and runs the tool once).
      lastSubmittedMessagesRef.current = msgs;

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
            impersonateUserId: config.impersonateUserId ?? undefined,
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

            onServerApproval: (payload) => {
              // Flush any pending text so the approval card lands in order, then render it.
              flushText();
              appendApprovalPart(payload);
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
    [config, isStreaming, getAccessToken, frontendTools, flushText, flushReasoning, syncParts, appendApprovalPart],
  );

  // Re-run the agent for an approved Medium card — but only when idle, since submitMessages drops
  // calls while a run is in flight. Called both directly (on approve) and from the effect below
  // when the stream ends, so the retry fires whether the card was approved mid-stream or after.
  const flushPendingApprovalRetry = useCallback(() => {
    if (
      pendingApprovalRetryRef.current
      && !isStreamingRef.current
      && lastSubmittedMessagesRef.current
    ) {
      const nudge = pendingApprovalRetryRef.current;
      pendingApprovalRetryRef.current = null;
      const next: PlaygroundMessage[] = [
        ...lastSubmittedMessagesRef.current,
        { role: 'user', content: nudge },
      ];
      void submitMessages(next);
    }
  }, [submitMessages]);

  useEffect(() => {
    if (!isStreaming) {
      flushPendingApprovalRetry();
    }
  }, [isStreaming, flushPendingApprovalRetry]);

  // Spec 032 — record a decision for a server-owned approval card. Medium routes through the
  // tool-approvals decide endpoint and, on approval, nudges the agent to re-invoke the gated tool
  // (the gate consumes the args-hash-bound approval and runs it once). High routes through the same
  // proposal path the approvals queue uses, so an in-session approval takes the identical
  // authorization + dispatch path. The server is the decision authority; this only presents and collects.
  const decideServerApproval = useCallback(
    async (approval: ServerApprovalState, decision: 'Approve' | 'Reject') => {
      // Both tiers decide through the same server authority — POST /ai/tool-approvals/{id}/decide —
      // which validates identity / tenant / expiry / single-use and resolves the request. For High it
      // internally routes the linked proposal through the policy-checked approve/dismiss path.
      if (!approval.approvalRequestId) {
        setApprovalStatus(approval.id, 'error', 'This approval is missing its request reference.');
        return;
      }

      setApprovalStatus(approval.id, 'deciding');

      try {
        const result = await api.post<{ message?: string }>(
          `/ai/tool-approvals/${encodeURIComponent(approval.approvalRequestId)}/decide`,
          { decision },
        );

        if (decision === 'Approve') {
          setApprovalStatus(approval.id, 'approved', result?.message ?? `${approval.actionKind} was approved.`);
          // Medium runs inline when the agent re-invokes the gated tool, so nudge it to proceed. High
          // already executed synchronously inside the decide call — no retry needed.
          if (approval.kind === 'medium') {
            pendingApprovalRetryRef.current =
              `I approved "${approval.actionKind}". Please go ahead and complete that action now.`;
            flushPendingApprovalRetry();
          }
        } else {
          setApprovalStatus(
            approval.id,
            'rejected',
            result?.message ?? `${approval.actionKind} was rejected. Nothing was changed.`,
          );
        }
      } catch (error) {
        const message =
          (error as { userMessage?: string })?.userMessage
          ?? (error instanceof Error ? error.message : 'The decision could not be recorded.');
        setApprovalStatus(approval.id, 'error', message);
      }
    },
    [setApprovalStatus, flushPendingApprovalRetry],
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
            impersonateUserId: config.impersonateUserId ?? undefined,
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

  const runFollowUpSuggestion = useCallback(
    async (prompt: string) => {
      await sendMessage(prompt);
    },
    [sendMessage],
  );

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
    approvalPartsRef.current.clear();
    pendingApprovalRetryRef.current = null;
    lastSubmittedMessagesRef.current = null;
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
    decideServerApproval,
    runFollowUpSuggestion,
  };
}
