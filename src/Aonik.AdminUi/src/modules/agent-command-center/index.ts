import type { AdminModule } from '../types';
import type { NavigationSection } from '@/types';
import type { WorkspacePanelConfig, WorkspaceTemplate } from '@/workspace/types';
import { AgentFleetPanel } from '@/workspace/apps/AgentFleetPanel';
import { AgentPerformancePanel } from '@/workspace/apps/AgentPerformancePanel';
import { AgentCostPanel } from '@/workspace/apps/AgentCostPanel';
import { AgentErrorsPanel } from '@/workspace/apps/AgentErrorsPanel';
import { ApprovalsPage } from '@/pages/approvals';
import { AiRunQueuePage, AiPoliciesPage, AiUsagePage } from '@/pages/ai-ops';
import { WorkflowsListPage, WorkflowEditorPage } from '@/pages/workflows';

// ── Navigation ─────────────────────────────────────────────────────────
//
// One "AI & Agents" parent that pops out into three child groups —
// Operations / Configuration / Build & debug — replacing both the old
// "Command Center" section here and the loose "AI" parent that used to
// live in core/index.ts. Mirrors the starterkit's single Platform > AI
// & Agents entry, just with more depth because Aonik exposes the
// configuration surfaces too.
//
// Approvals is intentionally grouped here in the shell IA because the
// approval queue is primarily agent/operator-facing in the current admin.

const navigation: NavigationSection[] = [
  {
    id: 'ai-agents',
    items: [
      {
        id: 'ai-agents-parent',
        label: 'AI & Agents',
        icon: 'Bot',
        viewAllHref: '/ai/runs',
        viewAllLabel: 'Open run queue',
        childGroups: [
          {
            label: 'Operations',
            items: [
              { id: 'approvals', label: 'Approvals', icon: 'CheckCircle2', href: '/approvals' },
              { id: 'ai-runs', label: 'Run queue', icon: 'Activity', href: '/ai/runs' },
              { id: 'ai-policies', label: 'Policies', icon: 'ShieldCheck', href: '/ai/policies' },
              { id: 'ai-usage', label: 'Usage', icon: 'TrendingUp', href: '/ai/usage' },
            ],
          },
          {
            label: 'Configuration',
            items: [
              { id: 'ai-agents-item', label: 'Agents', icon: 'Bot', href: '/ai/agents' },
              { id: 'ai-models-item', label: 'AI Models', icon: 'Brain', href: '/ai/models' },
              { id: 'ai-tasks-item', label: 'LLM Tasks', icon: 'ListChecks', href: '/ai/tasks' },
              { id: 'ai-prompts-item', label: 'Prompt Templates', icon: 'FileText', href: '/ai/prompts' },
              { id: 'ai-routing-item', label: 'Route Policies', icon: 'Route', href: '/ai/routing' },
            ],
          },
          {
            label: 'Build & debug',
            items: [
              { id: 'ai-workflows-item', label: 'Workflows', icon: 'Workflow', href: '/ai/workflows' },
              { id: 'ai-playground-item', label: 'AI Playground', icon: 'FlaskConical', href: '/ai/playground' },
              { id: 'ai-traces-item', label: 'AI Traces', icon: 'Activity', href: '/ai/traces' },
            ],
          },
        ],
      },
    ],
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
  },
  {
    id: 'agent-performance',
    title: 'Performance Monitor',
    description: 'Latency percentiles, TTFT, and client vs server timing.',
    type: 'internal',
    category: 'micro-app',
    componentKey: 'agent-performance',
    appCardId: '31',
  },
  {
    id: 'agent-cost',
    title: 'Cost & Tokens',
    description: 'Token consumption and cost breakdown by agent.',
    type: 'internal',
    category: 'micro-app',
    componentKey: 'agent-cost',
    appCardId: '32',
  },
  {
    id: 'agent-errors',
    title: 'Errors & Failures',
    description: 'Error rates, failure analysis, and top error groups.',
    type: 'internal',
    category: 'micro-app',
    componentKey: 'agent-errors',
    appCardId: '33',
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
    id: 'agent-command-center',
    name: 'Agent Command Center',
    description: 'Fleet overview, performance, cost, and error tracking in a single dashboard.',
    icon: 'Bot',
    // Layout: Fleet (top-left) | Performance (top-right)
    //         Cost   (bot-left) | Errors     (bot-right)
    panels: ['agent-fleet', 'agent-performance', 'agent-cost', 'agent-errors'],
    layout: 'dashboard',
  },
];

// ── Module Export ───────────────────────────────────────────────────────

export const agentCommandCenterModule: AdminModule = {
  id: 'agent-command-center',
  name: 'Agent Command Center',
  navigation,
  routes: [
    { path: '/approvals', element: ApprovalsPage },
    { path: '/ai/runs', element: AiRunQueuePage },
    { path: '/ai/policies', element: AiPoliciesPage },
    { path: '/ai/usage', element: AiUsagePage },
    { path: '/ai/workflows', element: WorkflowsListPage },
    { path: '/ai/workflows/:workflowId', element: WorkflowEditorPage },
  ],
  panels,
  panelComponents,
  defaultWorkspacePanels: ['agent-fleet', 'agent-performance'],
  workspaceTemplates,
  breadcrumbs: [
    { pathPrefix: '/approvals', trail: ['Approvals'] },
    { pathPrefix: '/ai/runs', trail: ['AI', 'Run queue'] },
    { pathPrefix: '/ai/policies', trail: ['AI', 'Policies'] },
    { pathPrefix: '/ai/usage', trail: ['AI', 'Usage'] },
    { pathPrefix: '/ai/workflows/', trail: ['AI', 'Workflows', 'Editor'] },
    { pathPrefix: '/ai/workflows', trail: ['AI', 'Workflows'] },
  ],
};
