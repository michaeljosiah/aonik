import { describe, expect, it } from 'vitest';

import { upsertTrailingTextPart } from '@/hooks/playgroundOutputParts';

describe('upsertTrailingTextPart', () => {
  it('replaces an existing trailing text part instead of duplicating it', () => {
    const initial = [{ type: 'text' as const, content: 'Short answer.' }];

    const updated = upsertTrailingTextPart(initial, 'Short answer. More detail.');

    expect(updated).toEqual([{ type: 'text', content: 'Short answer. More detail.' }]);
  });

  it('appends a new text part after non-text output', () => {
    const initial = [
      {
        type: 'tool-call' as const,
        toolCall: {
          toolCallId: 'call-1',
          toolCallName: 'display_budget_breakdown',
          args: '{}',
          status: 'completed' as const,
        },
      },
    ];

    const updated = upsertTrailingTextPart(initial, 'You are over budget on transport.');

    expect(updated).toEqual([
      initial[0],
      { type: 'text', content: 'You are over budget on transport.' },
    ]);
  });
});
