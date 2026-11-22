// @vitest-environment jsdom
import { renderToStaticMarkup } from 'react-dom/server';
import { expect, it } from 'vitest';
import { ChatMessageList } from './ChatMessageList';
import { describeChatError } from '@/lib/chatErrors';

it('shows one actionable quota notice and preserves a partial response without exposing raw provider errors', () => {
  const raw = 'HTTP 429 (insufficient_quota: credit_balance_exhausted) private-provider-diagnostic';
  const html = renderToStaticMarkup(<ChatMessageList messages={[{ type: 'assistant', id: 'response', content: 'The invoice is still awaiting review.', error: raw }]} isStreaming={false} />);

  expect(html).toContain('The invoice is still awaiting review.');
  expect(html.match(/role="alert"/g)).toHaveLength(1);
  expect(html).toContain('AI credits unavailable');
  expect(html).toContain('review provider billing');
  expect(html).not.toContain('private-provider-diagnostic');
  expect(html).not.toContain('Connection error');
});

it('distinguishes provider throttling from an exhausted credit balance', () => {
  expect(describeChatError('HTTP 429 rate_limit_exceeded').code).toBe('rate_limit');
  expect(describeChatError('HTTP 429 insufficient_quota').code).toBe('insufficient_quota');
});
