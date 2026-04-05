import type { AdminModule } from '../types';
import type { NavigationSection } from '@/types';
import type { WorkspacePanelConfig } from '@/workspace/types';
import { AnalyticsPage } from '@/pages/AnalyticsPage';
import { AiModelsPage } from '@/pages/ai/AiModelsPage';
import { AgentConfigPage } from '@/pages/ai/AgentConfigPage';
import { AgentDetailPage } from '@/pages/ai/AgentDetailPage';
import { AnalyticsPanel } from '@/workspace/apps/AnalyticsPanel';
import { AiModelsPanel } from '@/workspace/apps/AiModelsPanel';
import { AgentConfigPanel } from '@/workspace/apps/AgentConfigPanel';
import { PromptTemplatesPage } from '@/pages/ai/PromptTemplatesPage';
import { RoutePoliciesPage } from '@/pages/ai/RoutePoliciesPage';
import { PlaceholderPanel } from '@/workspace/apps/PlaceholderPanel';
import { PromptTemplatesPanel } from '@/workspace/apps/PromptTemplatesPanel';
import { RoutePoliciesPanel } from '@/workspace/apps/RoutePoliciesPanel';
import { AiPlaygroundPage } from '@/pages/ai/AiPlaygroundPage';
import { AiPlaygroundPanel } from '@/workspace/apps/AiPlaygroundPanel';

// ---------------------------------------------------------------------------
// Navigation — "Home" section shared across all modules
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
    ],
  },
  {
    id: 'ai',
    items: [
      {
        id: 'ai',
        label: 'AI',
        icon: 'Bot',
        viewAllHref: '/ai/agents',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'Configuration',
            items: [
              {
                id: 'ai-agents-item',
                label: 'Agents',
                icon: 'Bot',
                href: '/ai/agents',
              },
              {
                id: 'ai-models-item',
                label: 'AI Models',
                icon: 'Brain',
                href: '/ai/models',
              },
              {
                id: 'ai-prompts-item',
                label: 'Prompt Templates',
                icon: 'FileText',
                href: '/ai/prompts',
              },
              {
                id: 'ai-routing-item',
                label: 'Route Policies',
                icon: 'Route',
                href: '/ai/routing',
              },
              {
                id: 'ai-playground-item',
                label: 'Agent Playground',
                icon: 'FlaskConical',
                href: '/ai/playground',
              },
            ],
          },
        ],
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
  { path: '/analytics', element: AnalyticsPage },
  { path: '/ai/models', element: AiModelsPage },
  { path: '/ai/agents', element: AgentConfigPage },
  { path: '/ai/agents/:agentName', element: AgentDetailPage },
  { path: '/ai/prompts', element: PromptTemplatesPage },
  { path: '/ai/routing', element: RoutePoliciesPage },
  { path: '/ai/playground', element: AiPlaygroundPage },
];

// ---------------------------------------------------------------------------
// Workspace panels — cross-cutting panels
// ---------------------------------------------------------------------------
const panels: WorkspacePanelConfig[] = [
  { id: 'analytics', title: 'Analytics', description: 'Portfolio-level performance, trends, and AI insights.', type: 'internal', componentKey: 'analytics', route: '/analytics', defaultWidth: 720 },
  { id: 'search', title: 'Search', type: 'internal', componentKey: 'placeholder', route: '/search' },
  { id: 'ai', title: 'AI & Agents', type: 'internal', componentKey: 'placeholder', route: '/ai' },
  { id: 'ai-agents', title: 'Agents', description: 'Configure domain agents, assign models, and manage overrides.', type: 'internal', componentKey: 'agentConfig', route: '/ai/agents' },
  { id: 'ai-models', title: 'AI Models', description: 'Manage AI providers and models used across the platform.', type: 'internal', componentKey: 'aiModels', route: '/ai/models' },
  { id: 'ai-orchestrator', title: 'Orchestrator', type: 'internal', componentKey: 'placeholder', route: '/ai/orchestrator' },
  { id: 'ai-prompts', title: 'Prompt Templates', description: 'Manage versioned prompt templates for AI tasks.', type: 'internal', componentKey: 'promptTemplates', route: '/ai/prompts' },
  { id: 'ai-routing', title: 'Route Policies', description: 'Configure AI model routing policies per use-case.', type: 'internal', componentKey: 'routePolicies', route: '/ai/routing' },
  { id: 'ai-playground', title: 'Agent Playground', description: 'Test agents, prompts, and models interactively.', type: 'internal', componentKey: 'aiPlayground', route: '/ai/playground' },
];

const panelComponents = {
  analytics: AnalyticsPanel,
  aiModels: AiModelsPanel,
  agentConfig: AgentConfigPanel,
  promptTemplates: PromptTemplatesPanel,
  routePolicies: RoutePoliciesPanel,
  aiPlayground: AiPlaygroundPanel,
  placeholder: PlaceholderPanel,
};

// ---------------------------------------------------------------------------
// Breadcrumbs
// ---------------------------------------------------------------------------
const breadcrumbs = [
  { pathPrefix: '/analytics', trail: ['Analytics'] },
  { pathPrefix: '/ai/models', trail: ['AI', 'Models'] },
  { pathPrefix: '/ai/prompts', trail: ['AI', 'Prompt Templates'] },
  { pathPrefix: '/ai/playground', trail: ['AI', 'Agent Playground'] },
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
