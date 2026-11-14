import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it, vi } from 'vitest';
import { ServerApprovalCard } from './chatSupport';

vi.mock('@/services/textToSpeechSettingsService', () => ({ textToSpeechSettingsService: {} }));
vi.mock('@/pages/ai/playground/frontendTools', () => ({ createPlaygroundFrontendTools: () => [] }));

describe('workflow approval card', () => {
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
