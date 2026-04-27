import type { AdminModule } from '../types';
import type { NavigationSection } from '@/types';
import type { WorkspacePanelConfig } from '@/workspace/types';
import { AiModelsPage } from '@/pages/ai/AiModelsPage';
import { AgentConfigPage } from '@/pages/ai/AgentConfigPage';
import { AgentDetailPage } from '@/pages/ai/AgentDetailPage';
import { AiModelsPanel } from '@/workspace/apps/AiModelsPanel';
import { AgentConfigPanel } from '@/workspace/apps/AgentConfigPanel';
import { PromptTemplatesPage } from '@/pages/ai/PromptTemplatesPage';
import { RoutePoliciesPage } from '@/pages/ai/RoutePoliciesPage';
import { AiTasksPage } from '@/pages/ai/AiTasksPage';
import { AiTracesPage } from '@/pages/ai/AiTracesPage';
import { AiTraceDetailPage } from '@/pages/ai/AiTraceDetailPage';
import { PromptTemplatesPanel } from '@/workspace/apps/PromptTemplatesPanel';
import { RoutePoliciesPanel } from '@/workspace/apps/RoutePoliciesPanel';
import { AiTasksPanel } from '@/workspace/apps/AiTasksPanel';
import { AiPlaygroundPage } from '@/pages/ai/AiPlaygroundPage';
import { AiPlaygroundPanel } from '@/workspace/apps/AiPlaygroundPanel';

// ---------------------------------------------------------------------------
// Navigation — "Home" section only.
//
// AI/agent nav lives in the agent-command-center module (post-Wave-7b nav
// simplification): every AI surface — Run queue, Policies, Usage,
// Playground, Traces, Models, Prompts, etc. — is grouped under a single
// "AI & Agents" parent there. This module just owns the top-of-rail
// shortcuts every signed-in user sees.
// ---------------------------------------------------------------------------
const navigation: NavigationSection[] = [
  {
    id: 'core',
    items: [
      {
        id: 'dashboard',
        label: 'Dashboard',
        icon: 'LayoutDashboard',
        href: '/',
      },
      {
        id: 'workspace',
        label: 'Workspace',
        icon: 'PanelsTopLeft',
        href: '/workspace',
        viewAllHref: '/workspace',
        viewAllLabel: 'View all',
      },
    ],
  },
];

// ---------------------------------------------------------------------------
// Routes — shared / cross-cutting routes (dashboard, analytics, workspace, AI)
// These routes are NOT domain-specific and belong to the core shell.
// Setup/auth routes are handled separately in AuthenticatedApp, not here.
// ---------------------------------------------------------------------------
const routes = [
  { path: '/ai/models', element: AiModelsPage },
  { path: '/ai/agents', element: AgentConfigPage },
  { path: '/ai/agents/:agentName', element: AgentDetailPage },
  { path: '/ai/tasks', element: AiTasksPage },
  { path: '/ai/traces', element: AiTracesPage },
  { path: '/ai/traces/:runId', element: AiTraceDetailPage },
  { path: '/ai/prompts', element: PromptTemplatesPage },
  { path: '/ai/routing', element: RoutePoliciesPage },
  { path: '/ai/playground', element: AiPlaygroundPage },
];

// ---------------------------------------------------------------------------
// Workspace panels — cross-cutting panels
// ---------------------------------------------------------------------------
const panels: WorkspacePanelConfig[] = [
  { id: 'ai-agents', title: 'Agents', description: 'Configure domain agents, assign models, and manage overrides.', type: 'internal', category: 'page', componentKey: 'agentConfig', route: '/ai/agents' },
  { id: 'ai-models', title: 'AI Models', description: 'Manage AI providers and models used across the platform.', type: 'internal', category: 'page', componentKey: 'aiModels', route: '/ai/models' },
  { id: 'ai-tasks', title: 'LLM Tasks', description: 'View and manage LLM task configurations, prompts, and model routing.', type: 'internal', category: 'page', componentKey: 'aiTasks', route: '/ai/tasks' },
  { id: 'ai-prompts', title: 'Prompt Templates', description: 'Manage versioned prompt templates for AI tasks.', type: 'internal', category: 'page', componentKey: 'promptTemplates', route: '/ai/prompts' },
  { id: 'ai-routing', title: 'Route Policies', description: 'Configure AI model routing policies per use-case.', type: 'internal', category: 'page', componentKey: 'routePolicies', route: '/ai/routing' },
  { id: 'ai-playground', title: 'AI Playground', description: 'Test agents, AI tasks, prompts, and models interactively.', type: 'internal', category: 'page', componentKey: 'aiPlayground', route: '/ai/playground' },
];

const panelComponents = {
  aiModels: AiModelsPanel,
  agentConfig: AgentConfigPanel,
  aiTasks: AiTasksPanel,
  promptTemplates: PromptTemplatesPanel,
  routePolicies: RoutePoliciesPanel,
  aiPlayground: AiPlaygroundPanel,
};

// ---------------------------------------------------------------------------
// Breadcrumbs
// ---------------------------------------------------------------------------
const breadcrumbs = [
  { pathPrefix: '/ai/models', trail: ['AI', 'Models'] },
  { pathPrefix: '/ai/traces/', trail: ['AI', 'AI Traces', 'Run Trace'] },
  { pathPrefix: '/ai/traces', trail: ['AI', 'AI Traces'] },
  { pathPrefix: '/ai/tasks', trail: ['AI', 'LLM Tasks'] },
  { pathPrefix: '/ai/prompts', trail: ['AI', 'Prompt Templates'] },
  { pathPrefix: '/ai/playground', trail: ['AI', 'AI Playground'] },
  { pathPrefix: '/ai/routing', trail: ['AI', 'Route Policies'] },
  { pathPrefix: '/ai/agents/', trail: ['AI', 'Agents', 'Agent Details'] },
  { pathPrefix: '/ai/agents', trail: ['AI', 'Agents'] },
  { pathPrefix: '/ai', trail: ['AI'] },
  { pathPrefix: '/workspace', trail: ['Workspace'] },
];

// ---------------------------------------------------------------------------
// Module export
// ---------------------------------------------------------------------------
export const coreModule: AdminModule = {
  id: 'core',
  name: 'Core',
  navigation,
  routes,
  panels,
  panelComponents,
  breadcrumbs,
};
