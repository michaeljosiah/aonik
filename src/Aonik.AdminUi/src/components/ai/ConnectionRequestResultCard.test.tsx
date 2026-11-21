import { renderToStaticMarkup } from 'react-dom/server';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { ConnectionRequestResultCard } from './ConnectionRequestResultCard';

describe('connection request review card', () => {
  const id = 'b85dd810-1778-45d1-a3b6-4da7a67352aa';
  const result = JSON.stringify({ id, status: 'Pending', nextAction: '<img src=x onerror=alert(1)>', reviewPath: `/settings/my-connections?requestId=${id}` });

  it('escapes request text and links only to the authenticated account review page', () => {
    const html = renderToStaticMarkup(<MemoryRouter><ConnectionRequestResultCard toolName="aonik_create_connection_request" result={result} /></MemoryRouter>);
    expect(html).toContain(`href="/settings/my-connections?requestId=${id}"`);
    expect(html).toContain('&lt;img');
    expect(html).not.toContain('<img');
    expect(html).not.toContain('accounts.google.com');
  });

  it('cannot be created by a mailbox result that imitates the request contract', () => {
    expect(renderToStaticMarkup(<MemoryRouter><ConnectionRequestResultCard toolName="gmail_get_message" result={result} /></MemoryRouter>)).toBe('');
  });
});
