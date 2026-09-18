import type { AdminModule } from '../types';
import type { NavigationSection } from '@/types';
import type { WorkspacePanelConfig } from '@/workspace/types';
import { AgentExtensionsPanel } from '@/workspace/apps/AgentExtensionsPanel';
import { AgentExtensionsPage } from '@/pages/agent-extensions/AgentExtensionsPage';

// ── Navigation ─────────────────────────────────────────────────────────────
// Contributes one link into the shared "AI & Agents" section (merged by id with
// the agent-command-center section's items).
const navigation: NavigationSection[] = [
  {
    id: 'ai-agents',
    items: [
      { id: 'ai-agent-extensions', label: 'Agent Extensions', icon: 'Puzzle', href: '/ai/agent-extensions' },
    ],
  },
];

// ── Panels (Spec 033 §10) ────────────────────────────────────────────────────
const panels: WorkspacePanelConfig[] = [
  {
    id: 'agent-extensions',
    title: 'Agent Extensions',
    description: 'Tenant-managed agent skills, remote MCP servers, and HTTP tools — add, test, and review.',
    type: 'internal',
    category: 'micro-app',
    componentKey: 'agent-extensions',
    appCardId: '34',
  },
];

const panelComponents = {
  'agent-extensions': AgentExtensionsPanel,
};

// ── Module export ────────────────────────────────────────────────────────────
export const agentExtensionsModule: AdminModule = {
  id: 'agent-extensions',
  name: 'Agent Extensions',
  requires: ['agents'],
  navigation,
  routes: [
    { path: '/ai/agent-extensions', element: AgentExtensionsPage },
  ],
  panels,
  panelComponents,
  defaultWorkspacePanels: [],
  breadcrumbs: [
    { pathPrefix: '/ai/agent-extensions', trail: [{ label: 'AI', href: '/ai' }, 'Agent Extensions'] },
  ],
};
