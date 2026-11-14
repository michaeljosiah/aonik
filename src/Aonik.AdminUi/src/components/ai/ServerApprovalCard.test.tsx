import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it, vi } from 'vitest';
import { ServerApprovalCard } from './chatSupport';

vi.mock('@/services/textToSpeechSettingsService', () => ({ textToSpeechSettingsService: {} }));
vi.mock('@/pages/ai/playground/frontendTools', () => ({ createPlaygroundFrontendTools: () => [] }));

describe('workflow approval card', () => {
  it('shows exact Gmail recipients, content and attachment hashes as escaped evidence', () => {
    const html = renderToStaticMarkup(<ServerApprovalCard approval={{ id: 'gmail-approval', approvalRequestId: '1', kind: 'high', tier: 'High',
      actionKind: 'Send message', tool: 'gmail_send_message', status: 'pending', preview: { title: 'Review Gmail send', before: null,
        after: { account: 'sender@example.test', message: { to: ['to@example.test'], cc: ['cc@example.test'], bcc: ['bcc@example.test'],
          subject: 'Invoice', body: '<img src=x onerror=attack()>', attachments: [{ fileName: 'invoice.pdf', sha256: 'exact-reviewed-hash' }] } } } }} onDecide={() => {}} />);
    for (const expected of ['sender@example.test', 'to@example.test', 'cc@example.test', 'bcc@example.test', 'exact-reviewed-hash', 'invoice.pdf']) expect(html).toContain(expected);
    expect(html).not.toContain('<img'); expect(html).toContain('&lt;img');
  });
  it('renders the server change preview as escaped text with explicit decision controls', () => {
    const html = renderToStaticMarkup(<ServerApprovalCard approval={{
      id: 'approval-1', approvalRequestId: '1', kind: 'high', tier: 'High', actionKind: 'Publish workflow',
      tool: 'aonik_request_publish_workflow', status: 'pending',
      preview: { title: 'Publish invoice route', before: { threshold: '1000.00' },
        after: { threshold: '2500.00', label: '<script>untrusted()</script>' } },
    }} onDecide={() => {}} />);
    expect(html).toContain('Publish invoice route');
    expect(html).toContain('Review exact changes');
    expect(html).toContain('1000.00');
    expect(html).toContain('2500.00');
    expect(html).toContain('Approve');
    expect(html).toContain('Reject');
    expect(html).not.toContain('<script>');
    expect(html).not.toContain('moves money');
  });
});
