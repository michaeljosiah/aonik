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
  streamPlaygroundRun,
  type PlaygroundMessage,
  type PlaygroundRunRequest,
} from '@/lib/playground-client';
import {
  createPlaygroundFrontendTools,
  playgroundFrontendToolNames,
} from '@/pages/ai/playground/frontendTools';

function createSseResponse(events: Array<Record<string, unknown>>): Response {
  const payload = events.map((event) => `data: ${JSON.stringify(event)}\n\n`).join('');
  return new Response(payload, {
    status: 200,
    headers: {
      'Content-Type': 'text/event-stream',
    },
  });
}

function createInitialToolCallResponse(toolName: string, args: Record<string, unknown>): Response {
  return createSseResponse([
    {
      type: 'TOOL_CALL_START',
      toolCallId: 'call-1',
      toolCallName: toolName,
    },
    {
      type: 'TOOL_CALL_ARGS',
      toolCallId: 'call-1',
      delta: JSON.stringify(args),
    },
    {
      type: 'TOOL_CALL_END',
      toolCallId: 'call-1',
    },
    {
      type: 'RUN_FINISHED',
      metrics: {
        inputTokens: 12,
        outputTokens: 4,
        totalTokens: 16,
        latencyMs: 50,
      },
    },
  ]);
}

function createFinalResponse(): Response {
  return createSseResponse([
    {
      type: 'TEXT_MESSAGE_CONTENT',
      delta: 'Done.',
    },
    {
      type: 'RUN_FINISHED',
      metrics: {
        inputTokens: 18,
        outputTokens: 6,
        totalTokens: 24,
        latencyMs: 60,
      },
    },
  ]);
}

function createToolCallThenFinalTextResponses(toolName: string): Response[] {
  return [
    createSseResponse([
      {
        type: 'TOOL_CALL_START',
        toolCallId: 'call-1',
        toolCallName: toolName,
      },
      {
        type: 'TOOL_CALL_ARGS',
        toolCallId: 'call-1',
        delta: JSON.stringify({ action: 'Confirm this action', description: 'Proceed', severity: 'medium' }),
      },
      {
        type: 'TOOL_CALL_END',
        toolCallId: 'call-1',
      },
      {
        type: 'RUN_FINISHED',
        metrics: {
          inputTokens: 12,
          outputTokens: 0,
          totalTokens: 12,
          latencyMs: 50,
        },
      },
    ]),
    createSseResponse([
      {
        type: 'TEXT_MESSAGE_CONTENT',
        delta: 'Short answer: not safely, no. ',
      },
      {
        type: 'TEXT_MESSAGE_CONTENT',
        delta: 'You only have GBP £0.00 available.',
      },
      {
        type: 'RUN_FINISHED',
        metrics: {
          inputTokens: 18,
          outputTokens: 12,
          totalTokens: 30,
          latencyMs: 60,
        },
      },
    ]),
  ];
}

function createRequest(messages: PlaygroundMessage[]): PlaygroundRunRequest {
  return {
    agentName: 'personal-finance-agent',
    messages,
  };
}

describe('streamPlaygroundRun frontend tools', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('registers all supported AI Playground frontend tools', () => {
    const tools = createPlaygroundFrontendTools({ confirmAction: async () => 'approved' });

    expect(Array.from(tools.keys())).toEqual([...playgroundFrontendToolNames]);
  });

  it.each([
    {
      toolName: 'confirmAction',
      args: {
        action: 'Create starter budget',
        description: 'Create a simple April budget.',
        severity: 'high',
      },
      expectedResult: 'approved',
    },
    {
      toolName: 'display_fx_rate_chart',
      args: {
        baseCurrency: 'GBP',
        targetCurrency: 'NGN',
        rates: [{ date: 'Apr 1', rate: 2010.5 }],
        signal: 'hold',
      },
      expectedResult: 'displayed',
    },
    {
      toolName: 'display_budget_breakdown',
      args: {
        period: 'April 2026',
        totalBudget: 1000,
        totalSpent: 450,
        currency: 'USD',
        categories: [{ name: 'Transport', budgeted: 200, spent: 150, status: 'under' }],
      },
      expectedResult: 'displayed',
    },
    {
      toolName: 'display_spending_pie_chart',
      args: {
        currency: 'USD',
        totalSpent: 512.5,
        categories: [
          { name: 'Utilities', amount: 200, percentage: 39 },
          { name: 'Transport', amount: 120, percentage: 23.4 },
        ],
      },
      expectedResult: 'displayed',
    },
    {
      toolName: 'display_autopilot_proposal',
      args: {
        agent: 'personal-finance-agent',
        action: 'Create starter budget',
        description: 'Suggest a baseline budget for this month.',
      },
      expectedResult: 'displayed',
    },
    {
      toolName: 'display_follow_up_suggestions',
      args: {
        prompt: 'Pick a next step',
        suggestions: [
          {
            label: 'Check budget',
            prompt: 'How am I doing against my budget this month?',
          },
          {
            label: 'Upcoming bills',
            prompt: 'What bills are coming up next?',
          },
        ],
      },
      expectedResult: 'displayed',
    },
    {
      toolName: 'display_option_selector',
      args: {
        question: 'Which account should I use?',
        options: [
          { label: 'Main account', description: 'Everyday spending account' },
          { label: 'Savings pot', description: 'Emergency buffer' },
        ],
        multiSelect: false,
      },
      expectedResult: 'Main account',
    },
  ])('executes $toolName client-side and reruns with the tool result', async ({
    toolName,
    args,
    expectedResult,
  }) => {
    const confirmAction = vi.fn(async () => 'approved');
    const selectOptions = vi.fn(async () => 'Main account');
    const frontendTools = createPlaygroundFrontendTools({ confirmAction, selectOptions });
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(createInitialToolCallResponse(toolName, args))
      .mockResolvedValueOnce(createFinalResponse());
    vi.stubGlobal('fetch', fetchMock);

    const onToolResult = vi.fn();
    const onRerun = vi.fn();

    await streamPlaygroundRun({
      request: createRequest([{ role: 'user', content: 'Test frontend tool rerun.' }]),
      callbacks: { onToolResult, onRerun },
      getAccessToken: async () => null,
      frontendTools,
    });

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(onRerun).toHaveBeenCalledTimes(1);

    const firstRequest = JSON.parse(String(fetchMock.mock.calls[0]?.[1]?.body)) as PlaygroundRunRequest;
    expect(firstRequest.toolDefinitions?.map((tool) => tool.name)).toEqual([...playgroundFrontendToolNames]);

    const rerunRequest = JSON.parse(String(fetchMock.mock.calls[1]?.[1]?.body)) as PlaygroundRunRequest;
    const assistantToolCall = rerunRequest.messages.at(-2);
    const toolResultMessage = rerunRequest.messages.at(-1);

    expect(assistantToolCall).toMatchObject({
      role: 'assistant',
      content: '',
      toolCalls: [
        {
          id: 'call-1',
          type: 'function',
          function: {
            name: toolName,
            arguments: JSON.stringify(args),
          },
        },
      ],
    });
    expect(toolResultMessage).toEqual({
      id: 'tool-result-call-1',
      role: 'tool',
      toolCallId: 'call-1',
      content: expectedResult,
    });
    expect(onToolResult).toHaveBeenCalledWith('call-1', expectedResult);

    if (toolName === 'confirmAction') {
      expect(confirmAction).toHaveBeenCalledWith('call-1', {
        action: 'Create starter budget',
        description: 'Create a simple April budget.',
        severity: 'high',
      });
      expect(selectOptions).not.toHaveBeenCalled();
    } else if (toolName === 'display_option_selector') {
      expect(selectOptions).toHaveBeenCalledWith('call-1', {
        question: 'Which account should I use?',
        options: [
          { label: 'Main account', description: 'Everyday spending account' },
          { label: 'Savings pot', description: 'Emergency buffer' },
        ],
        multiSelect: false,
      });
      expect(confirmAction).not.toHaveBeenCalled();
    } else {
      expect(confirmAction).not.toHaveBeenCalled();
      expect(selectOptions).not.toHaveBeenCalled();
    }
  });

  it('does not duplicate final text when a frontend tool triggers a rerun', async () => {
    const [initialResponse, finalResponse] = createToolCallThenFinalTextResponses('confirmAction');
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(initialResponse)
      .mockResolvedValueOnce(finalResponse);
    vi.stubGlobal('fetch', fetchMock);

    const frontendTools = createPlaygroundFrontendTools({ confirmAction: async () => 'approved' });
    let combinedOutput = '';

    await streamPlaygroundRun({
      request: createRequest([{ role: 'user', content: 'Can I spend £300?' }]),
      callbacks: {
        onRerun: () => {
          combinedOutput = '';
        },
        onTextDelta: (delta) => {
          combinedOutput += delta;
        },
      },
      getAccessToken: async () => null,
      frontendTools,
    });

    expect(combinedOutput).toBe('Short answer: not safely, no. You only have GBP £0.00 available.');
  });
});
