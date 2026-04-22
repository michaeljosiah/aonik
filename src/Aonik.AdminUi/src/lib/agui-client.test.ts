import { afterEach, describe, expect, it, vi } from 'vitest';

vi.mock('@/auth', () => ({
  apiConfig: {
    baseUrl: 'http://example.test',
  },
}));

vi.mock('@/lib/tenantContext', () => ({
  getSelectedTenant: () => null,
}));

import {
  streamAguiChat,
  type FrontendToolRegistration,
  type RunAgentInput,
} from '@/lib/agui-client';

function createSseResponse(events: Array<Record<string, unknown>>): Response {
  const payload = events.map((event) => `data: ${JSON.stringify(event)}\n\n`).join('');
  return new Response(payload, {
    status: 200,
    headers: {
      'Content-Type': 'text/event-stream',
    },
  });
}

describe('streamAguiChat frontend tool reruns', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('emits synthetic tool results for client-side tools and reruns with the appended history', async () => {
    const frontendTools = new Map<string, FrontendToolRegistration>([
      [
        'display_budget_breakdown',
        {
          tool: {
            name: 'display_budget_breakdown',
            description: 'Display a budget chart.',
            parameters: { type: 'object' },
          },
          handler: async () => 'displayed',
        },
      ],
    ]);

    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(
        createSseResponse([
          {
            type: 'TOOL_CALL_START',
            toolCallId: 'call-1',
            toolCallName: 'display_budget_breakdown',
          },
          {
            type: 'TOOL_CALL_ARGS',
            toolCallId: 'call-1',
            delta: JSON.stringify({
              period: 'April 2026',
              totalBudget: 1000,
              totalSpent: 450,
              currency: 'GBP',
              categories: [],
            }),
          },
          {
            type: 'TOOL_CALL_END',
            toolCallId: 'call-1',
          },
          {
            type: 'RUN_FINISHED',
            threadId: 'thread-1',
            runId: 'run-1',
          },
        ]),
      )
      .mockResolvedValueOnce(
        createSseResponse([
          {
            type: 'TEXT_MESSAGE_CONTENT',
            messageId: 'assistant-2',
            delta: 'Rendered.',
          },
          {
            type: 'RUN_FINISHED',
            threadId: 'thread-1',
            runId: 'run-2',
          },
        ]),
      );

    vi.stubGlobal('fetch', fetchMock);

    const onToolCallResult = vi.fn();
    const onEvent = vi.fn();

    const input: RunAgentInput = {
      threadId: 'thread-1',
      runId: 'run-1',
      agentId: 'personal-finance-agent',
      messages: [
        {
          id: 'user-1',
          role: 'user',
          content: 'Show my budget breakdown.',
        },
      ],
      tools: [frontendTools.get('display_budget_breakdown')!.tool],
    };

    await streamAguiChat({
      input,
      callbacks: {
        onToolCallResult,
        onEvent,
      },
      getAccessToken: async () => null,
      frontendTools,
    });

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(onToolCallResult).toHaveBeenCalledWith(
      expect.objectContaining({
        type: 'TOOL_CALL_RESULT',
        toolCallId: 'call-1',
        content: 'displayed',
      }),
    );
    expect(onEvent).toHaveBeenCalledWith(
      expect.objectContaining({
        type: 'TOOL_CALL_RESULT',
        toolCallId: 'call-1',
        content: 'displayed',
      }),
    );

    const rerunRequest = JSON.parse(String(fetchMock.mock.calls[1]?.[1]?.body)) as RunAgentInput;
    expect(rerunRequest.parentRunId).toBe('run-1');
    expect(rerunRequest.messages.at(-2)).toMatchObject({
      role: 'assistant',
      toolCalls: [
        {
          id: 'call-1',
          type: 'function',
          function: {
            name: 'display_budget_breakdown',
          },
        },
      ],
    });
    expect(rerunRequest.messages.at(-1)).toEqual({
      id: 'tool-result-call-1',
      role: 'tool',
      content: 'displayed',
      toolCallId: 'call-1',
    });
  });
});
