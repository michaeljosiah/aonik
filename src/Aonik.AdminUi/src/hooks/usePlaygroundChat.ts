import { useCallback, useRef, useState } from 'react';
import {
  streamPlaygroundRun,
  type PlaygroundRunMetrics,
  type PlaygroundMessage,
} from '@/lib/playground-client';
import type { PlaygroundRunRecord } from '@/types/ai';
import { useAuth } from '@/auth';

export interface PlaygroundConfig {
  agentName: string | null;
  systemPrompt: string;
  modelId: string | null;
  userBriefJson: string | null;
  enabledToolNames: string[];
  temperature: number;
  maxTokens: number;
}

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
    userBriefJson: null,
    enabledToolNames: [],
    temperature: 0.7,
    maxTokens: 2048,
  });

  // Single-mode output state
  const [output, setOutput] = useState('');

  // Chat state (used by compare mode)
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [streamError, setStreamError] = useState<string | null>(null);
  const [metrics, setMetrics] = useState<PlaygroundRunMetrics | null>(null);
  const [runHistory, setRunHistory] = useState<PlaygroundRunRecord[]>([]);

  const abortRef = useRef<AbortController | null>(null);

  const updateConfig = useCallback(
    (updates: Partial<PlaygroundConfig>) => {
      setConfig((prev) => ({ ...prev, ...updates }));
    },
    [],
  );

  // ── Submit messages (single-mode: message block editor) ────────────────────

  const submitMessages = useCallback(
    async (msgs: PlaygroundMessage[]) => {
      if (isStreaming) return;

      setIsStreaming(true);
      setStreamError(null);
      setMetrics(null);
      setOutput('');

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
          },
          callbacks: {
            onTextDelta: (delta) => {
              fullResponse += delta;
              setOutput(fullResponse);
            },
            onRunFinished: (runMetrics) => {
              setMetrics(runMetrics);

              const userMsg = msgs.filter((m) => m.role === 'user').pop();
              const record: PlaygroundRunRecord = {
                id: `run-${Date.now()}`,
                timestamp: new Date(),
                modelId: config.modelId ?? undefined,
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
    [config, isStreaming, getAccessToken],
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
            onRunFinished: (runMetrics) => {
              setMetrics(runMetrics);

              const record: PlaygroundRunRecord = {
                id: `run-${Date.now()}`,
                timestamp: new Date(),
                modelId: config.modelId ?? undefined,
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
    [config, messages, isStreaming, getAccessToken],
  );

  const stopStreaming = useCallback(() => {
    abortRef.current?.abort();
  }, []);

  const resetChat = useCallback(() => {
    abortRef.current?.abort();
    setMessages([]);
    setOutput('');
    setMetrics(null);
    setStreamError(null);
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
    messages,
    isStreaming,
    streamError,
    metrics,
    runHistory,
    submitMessages,
    sendMessage,
    stopStreaming,
    resetChat,
    addRunRecord,
    clearHistory,
  };
}
