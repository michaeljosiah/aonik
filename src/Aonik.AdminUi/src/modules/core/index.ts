import type { AdminModule } from '../types';
import type { NavigationSection } from '@/types';
import type { WorkspacePanelConfig } from '@/workspace/types';
import { AnalyticsPage } from '@/pages/AnalyticsPage';
import { AnalyticsPanel } from '@/workspace/apps/AnalyticsPanel';
import { PlaceholderPanel } from '@/workspace/apps/PlaceholderPanel';

// ---------------------------------------------------------------------------
// Navigation — "Home" section shared across all modules
// ---------------------------------------------------------------------------
const navigation: NavigationSection[] = [
  {
    id: 'cross-functional',
    label: 'Home',
    items: [
      {
        id: 'dashboard',
        label: 'Dashboard',
        icon: 'LayoutDashboard',
        href: '/',
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
];

// ---------------------------------------------------------------------------
// Workspace panels — cross-cutting panels
// ---------------------------------------------------------------------------
const panels: WorkspacePanelConfig[] = [
  { id: 'analytics', title: 'Analytics', description: 'Portfolio-level performance, trends, and AI insights.', type: 'internal', componentKey: 'analytics', route: '/analytics', defaultWidth: 720 },
  { id: 'search', title: 'Search', type: 'internal', componentKey: 'placeholder', route: '/search' },
  { id: 'ai', title: 'AI & Agents', type: 'internal', componentKey: 'placeholder', route: '/ai' },
  { id: 'ai-agents', title: 'Agents', type: 'internal', componentKey: 'placeholder', route: '/ai/agents' },
  { id: 'ai-models', title: 'AI Models', type: 'internal', componentKey: 'placeholder', route: '/ai/models' },
  { id: 'ai-orchestrator', title: 'Orchestrator', type: 'internal', componentKey: 'placeholder', route: '/ai/orchestrator' },
];

const panelComponents = {
  analytics: AnalyticsPanel,
  placeholder: PlaceholderPanel,
};

// ---------------------------------------------------------------------------
// Breadcrumbs
// ---------------------------------------------------------------------------
const breadcrumbs = [
  { pathPrefix: '/analytics', trail: ['Analytics'] },
  { pathPrefix: '/ai', trail: ['AI & Agents'] },
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
