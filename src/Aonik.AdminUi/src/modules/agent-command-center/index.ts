import type { AdminModule } from '../types';
import type { NavigationSection } from '@/types';
import type { WorkspacePanelConfig, WorkspaceTemplate } from '@/workspace/types';
import { AgentFleetPanel } from '@/workspace/apps/AgentFleetPanel';
import { AgentPerformancePanel } from '@/workspace/apps/AgentPerformancePanel';
import { AgentCostPanel } from '@/workspace/apps/AgentCostPanel';
import { AgentErrorsPanel } from '@/workspace/apps/AgentErrorsPanel';

// ── Navigation ─────────────────────────────────────────────────────────

const navigation: NavigationSection[] = [
  {
    id: 'agent-command-center',
    label: 'Command Center',
    icon: 'Bot',
    items: [],
  },
];

// ── Panels ─────────────────────────────────────────────────────────────

const panels: WorkspacePanelConfig[] = [
  {
    id: 'agent-fleet',
    title: 'Agent Fleet',
    description: 'Overview of all AI agents — activity, latency, and token usage.',
    type: 'internal',
    category: 'micro-app',
    componentKey: 'agent-fleet',
    appCardId: '30',
    defaultWidth: 480,
  },
  {
    id: 'agent-performance',
    title: 'Performance Monitor',
    description: 'Latency percentiles, TTFT, and client vs server timing.',
    type: 'internal',
    category: 'micro-app',
    componentKey: 'agent-performance',
    appCardId: '31',
    defaultWidth: 560,
  },
  {
    id: 'agent-cost',
    title: 'Cost & Tokens',
    description: 'Token consumption and cost breakdown by agent.',
    type: 'internal',
    category: 'micro-app',
    componentKey: 'agent-cost',
    appCardId: '32',
    defaultWidth: 480,
  },
  {
    id: 'agent-errors',
    title: 'Errors & Failures',
    description: 'Error rates, failure analysis, and top error groups.',
    type: 'internal',
    category: 'micro-app',
    componentKey: 'agent-errors',
    appCardId: '33',
    defaultWidth: 480,
  },
];

const panelComponents = {
  'agent-fleet': AgentFleetPanel,
  'agent-performance': AgentPerformancePanel,
  'agent-cost': AgentCostPanel,
  'agent-errors': AgentErrorsPanel,
};

// ── Workspace Templates ────────────────────────────────────────────────

const workspaceTemplates: WorkspaceTemplate[] = [
  {
    id: 'agent-command-center-ops',
    name: 'Agent Operations',
    description: 'Fleet overview, performance monitoring, and error tracking.',
    icon: 'Bot',
    panels: ['agent-fleet', 'agent-performance', 'agent-errors'],
    layout: 'split-horizontal',
  },
  {
    id: 'agent-command-center-cost',
    name: 'Agent Cost Analysis',
    description: 'Fleet overview with token and cost breakdown.',
    icon: 'DollarSign',
    panels: ['agent-fleet', 'agent-cost'],
    layout: 'split-horizontal',
  },
  {
    id: 'agent-command-center-full',
    name: 'Full Command Center',
    description: 'All agent monitoring panels in a single workspace.',
    icon: 'LayoutDashboard',
    panels: ['agent-fleet', 'agent-performance', 'agent-cost', 'agent-errors'],
    layout: 'split-horizontal',
  },
];

// ── Module Export ───────────────────────────────────────────────────────

export const agentCommandCenterModule: AdminModule = {
  id: 'agent-command-center',
  name: 'Agent Command Center',
  navigation,
  routes: [],
  panels,
  panelComponents,
  defaultWorkspacePanels: ['agent-fleet', 'agent-performance'],
  workspaceTemplates,
  breadcrumbs: [],
};
