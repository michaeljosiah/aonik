import { MySpacePage } from '@/pages/MySpacePage';
import { WorkspacePage } from '@/workspace/WorkspacePage';
import { AiChatPage } from '@/pages/ai/AiChatPage';
import type { AdminModule, ModuleRouteConfig } from '../types';
import type { NavigationSection } from '@/types';
import type { WorkspacePanelConfig } from '@/workspace/types';
import { AiModelsPage } from '@/pages/ai/AiModelsPage';
import { AgentConfigPage } from '@/pages/ai/AgentConfigPage';
import { AgentDetailPage } from '@/pages/ai/AgentDetailPage';
import { AiModelsPanel } from '@/workspace/apps/AiModelsPanel';
import { AgentConfigPanel } from '@/workspace/apps/AgentConfigPanel';
import { RoutePoliciesPage } from '@/pages/ai/RoutePoliciesPage';
import { AiTasksPage } from '@/pages/ai/AiTasksPage';
import { AiTracesPage } from '@/pages/ai/AiTracesPage';
import { AiTraceDetailPage } from '@/pages/ai/AiTraceDetailPage';
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
      // Temporarily hidden from the sidebar while the shell IA is being
      // revised. Keep the route/panel wiring intact so we can restore it.
    ],
  },
];

// ---------------------------------------------------------------------------
// Routes — shared / cross-cutting routes (dashboard, analytics, workspace, AI)
// These routes are NOT domain-specific and belong to the core shell.
// Setup/auth routes are handled separately in AuthenticatedApp, not here.
// ---------------------------------------------------------------------------
const routes: ModuleRouteConfig[] = [
  { screen: { id: "home", label: "My Space", permissions: {"authenticatedAdmin": true}, policy: "AdminUserPolicy", icon: "home" }, path: '/', element: MySpacePage },
  { screen: { id: "workspace", label: "Workspace", permissions: {"authenticatedAdmin": true}, policy: "AdminUserPolicy", icon: "stack" }, path: '/workspace', element: WorkspacePage },
  { screen: { id: "ai.chat", label: "AI Chat", permissions: {"authenticatedAdmin": true}, policy: "AdminUserPolicy", icon: "sparkles" }, path: '/ai/chat', element: AiChatPage },
  { screen: { id: "ai.chat-agent", label: "AI Chat", permissions: {"authenticatedAdmin": true}, policy: "AdminUserPolicy", icon: "sparkles" }, path: '/ai/chat/:agentId', element: AiChatPage, isDynamic: true },
  { screen: { id: "ai.models", label: "AI Models", permissions: {"authenticatedAdmin": true}, policy: "AdminUserPolicy", icon: "sparkles" }, path: '/ai/models', element: AiModelsPage },
  { screen: { id: "ai.agents", label: "Agents", permissions: {"authenticatedAdmin": true}, policy: "AdminUserPolicy", icon: "sparkles" }, path: '/ai/agents', element: AgentConfigPage },
  { screen: { id: "ai.agent-detail", label: "Agent", permissions: {"authenticatedAdmin": true}, policy: "AdminUserPolicy", icon: "sparkles" }, path: '/ai/agents/:agentName', element: AgentDetailPage },
  { screen: { id: "ai.tasks", label: "Tasks", permissions: {"authenticatedAdmin": true}, policy: "AdminUserPolicy", icon: "sparkles" }, path: '/ai/tasks', element: AiTasksPage },
  { screen: { id: "ai.traces", label: "AI Traces", permissions: {"authenticatedAdmin": true}, policy: "AdminUserPolicy", icon: "sparkles" }, path: '/ai/traces', element: AiTracesPage },
  { screen: { id: "ai.trace-detail", label: "AI Trace", permissions: {"authenticatedAdmin": true}, policy: "AdminUserPolicy", icon: "sparkles" }, path: '/ai/traces/:runId', element: AiTraceDetailPage },
  { screen: { id: "ai.routing", label: "Route Policies", permissions: {"authenticatedAdmin": true}, policy: "AdminPolicy", icon: "sparkles" }, path: '/ai/routing', element: RoutePoliciesPage },
  { screen: { id: "ai.playground", label: "Playground", permissions: {"authenticatedAdmin": true}, policy: "AdminUserPolicy", icon: "sparkles" }, path: '/ai/playground', element: AiPlaygroundPage },
];

// ---------------------------------------------------------------------------
// Workspace panels — cross-cutting panels
// ---------------------------------------------------------------------------
const panels: WorkspacePanelConfig[] = [
  { id: 'ai-agents', title: 'Agents', description: 'Configure domain agents, assign models, and manage overrides.', type: 'internal', category: 'page', componentKey: 'agentConfig', route: '/ai/agents' },
  { id: 'ai-models', title: 'AI Models', description: 'Manage AI providers and models used across the platform.', type: 'internal', category: 'page', componentKey: 'aiModels', route: '/ai/models' },
  { id: 'ai-tasks', title: 'LLM Tasks', description: 'View and manage LLM task configurations, prompts, and model routing.', type: 'internal', category: 'page', componentKey: 'aiTasks', route: '/ai/tasks' },
  { id: 'ai-routing', title: 'Route Policies', description: 'Configure AI model routing policies per use-case.', type: 'internal', category: 'page', componentKey: 'routePolicies', route: '/ai/routing' },
  { id: 'ai-playground', title: 'AI Playground', description: 'Test agents, AI tasks, prompts, and models interactively.', type: 'internal', category: 'page', componentKey: 'aiPlayground', route: '/ai/playground' },
];

const panelComponents = {
  aiModels: AiModelsPanel,
  agentConfig: AgentConfigPanel,
  aiTasks: AiTasksPanel,
  routePolicies: RoutePoliciesPanel,
  aiPlayground: AiPlaygroundPanel,
};

// ---------------------------------------------------------------------------
// Breadcrumbs
// ---------------------------------------------------------------------------
const breadcrumbs = [
  { pathPrefix: '/ai/models', trail: [{ label: 'AI', href: '/ai' }, 'Models'] },
  { pathPrefix: '/ai/traces/', trail: [{ label: 'AI', href: '/ai' }, { label: 'AI Traces', href: '/ai/traces' }, 'Run Trace'] },
  { pathPrefix: '/ai/traces', trail: [{ label: 'AI', href: '/ai' }, 'AI Traces'] },
  { pathPrefix: '/ai/tasks', trail: [{ label: 'AI', href: '/ai' }, 'LLM Tasks'] },
  { pathPrefix: '/ai/playground', trail: [{ label: 'AI', href: '/ai' }, 'AI Playground'] },
  { pathPrefix: '/ai/routing', trail: [{ label: 'AI', href: '/ai' }, 'Route Policies'] },
  { pathPrefix: '/ai/agents/', trail: [{ label: 'AI', href: '/ai' }, { label: 'Agents', href: '/ai/agents' }, 'Agent Details'] },
  { pathPrefix: '/ai/agents', trail: [{ label: 'AI', href: '/ai' }, 'Agents'] },
  { pathPrefix: '/ai', trail: ['AI'] },
  { pathPrefix: '/workspace', trail: ['Workspace'] },
];

// ---------------------------------------------------------------------------
// Module export
// ---------------------------------------------------------------------------
export const coreModule: AdminModule = {
  id: 'core',
  name: 'Core',
  requires: [],
  navigation,
  routes,
  panels,
  panelComponents,
  breadcrumbs,
};
