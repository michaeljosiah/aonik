import { describe, expect, it } from 'vitest';
import { agentDisplayName, notificationAgentName, resolveAgentPortrait } from './agentIdentity';

describe('agent identity presentation', () => {
  it('uses the dedicated AONIK portrait for main-assistant aliases', () => {
    for (const name of ['aonik', 'master-orchestrator', 'orch', 'AONIK AI']) {
      expect(resolveAgentPortrait(name)).toBe('/images/agents/cgi-aonik.png');
      expect(agentDisplayName(name)).toBe('AONIK');
    }
    expect(resolveAgentPortrait('finance-agent')).toBe('/images/agents/cgi-finance.png');
  });

  it('requires explicit agent notification metadata and keeps unknown authors generic', () => {
    expect(notificationAgentName({ source: 'Agent', metadataJson: '{"agentName":"finance-agent","proposalId":"p1"}' })).toBe('finance-agent');
    expect(notificationAgentName({ source: 'System', metadataJson: '{"agentName":"aonik"}' })).toBeUndefined();
    for (const metadataJson of ['{}', 'null', '[]', 'invalid', '{"agentName":42}']) {
      expect(notificationAgentName({ source: 'Agent', metadataJson })).toBeUndefined();
    }
    expect(resolveAgentPortrait('https://example.com/avatar.png')).toBeUndefined();
    expect(resolveAgentPortrait('unknown billing helper')).toBeUndefined();
    expect(agentDisplayName()).toBe('AI assistant');
  });
});
