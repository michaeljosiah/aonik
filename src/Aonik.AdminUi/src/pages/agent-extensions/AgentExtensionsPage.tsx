import { AgentExtensionsPanel } from '@/workspace/apps/AgentExtensionsPanel';

/**
 * Full-page route wrapper for the Spec 033 "Agent extensions" workspace app, so the hub is reachable
 * via /ai/agent-extensions in addition to the workspace dock. The panel manages its own scrolling.
 */
export function AgentExtensionsPage() {
  return (
    <div className="h-full min-h-0" style={{ minHeight: 'calc(100vh - 8rem)' }}>
      <AgentExtensionsPanel panelId="agent-extensions" title="Agent Extensions" />
    </div>
  );
}

export default AgentExtensionsPage;
