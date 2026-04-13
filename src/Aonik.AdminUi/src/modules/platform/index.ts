import type { AdminModule } from '../types';
import type { NavigationSection } from '@/types';
import type { WorkspacePanelConfig, WorkspaceTemplate } from '@/workspace/types';
import {
  AccessUsersPage,
  AccessRolesPage,
  AccessPermissionsPage,
  UserDetailPage,
} from '@/pages/access';
import {
  TenantsListPage,
  CreateTenantPage,
  TenantDetailPage,
} from '@/pages/tenants';
import {
  SettingsLandingPage,
  SettingsAuditLogsPage,
  SettingsTextToSpeechPage,
  SystemToolsPage,
  NotificationTemplatesPage,
  BackgroundJobsPage,
  BackgroundJobDetailPage,
  GlobalSettingsPage,
} from '@/pages/settings';
import { AlertsPage, AlertDetailPage } from '@/pages/alerts';
import { ContentBlocksListPage } from '@/pages/ContentBlocksListPage';
import { ContentBlockEditPage } from '@/pages/ContentBlockEditPage';
import { ContentWizardPage } from '@/pages/ContentWizardPage';
import { MediaLibraryPage } from '@/pages/MediaLibraryPage';
import { BackgroundJobsPanel } from '@/workspace/apps/BackgroundJobsPanel';
import { AuditLogPanel } from '@/workspace/apps/AuditLogPanel';
import { wrapPage } from '../utils';

// ---------------------------------------------------------------------------
// Navigation
// ---------------------------------------------------------------------------
const navigation: NavigationSection[] = [
  {
    id: 'team',
    audience: 'host',
    items: [
      {
        id: 'team',
        label: 'Team',
        icon: 'Users',
        audience: 'host',
        children: [
          { id: 'users', label: 'Users', icon: 'UserCog', href: '/access/users' },
          { id: 'roles', label: 'Roles', icon: 'Shield', href: '/access/roles' },
          { id: 'permissions', label: 'Permissions', icon: 'Key', href: '/access/permissions' },
        ],
      },
    ],
  },
  {
    id: 'admin',
    audience: 'host',
    items: [
      {
        id: 'admin',
        label: 'Admin',
        icon: 'Settings',
        audience: 'host',
        childGroups: [
          {
            label: 'Content',
            items: [
              { id: 'content-blocks', label: 'Content Blocks', icon: 'Layers', href: '/cms/content-blocks' },
              { id: 'content-wizard', label: 'Content Wizard', icon: 'Sparkles', href: '/cms/content-wizard' },
              { id: 'media-library', label: 'Media Library', icon: 'Image', href: '/cms/media' },
            ],
          },
          {
            label: 'Infrastructure',
            items: [
              { id: 'tenants', label: 'Tenants', icon: 'Building', href: '/tenants' },
              { id: 'platform-alerts', label: 'Platform Alerts', icon: 'Bell', href: '/admin/alerts' },
              { id: 'background-jobs', label: 'Background Jobs', icon: 'Timer', href: '/settings/background-jobs' },
              { id: 'settings-system-tools', label: 'System Tools', icon: 'Wrench', href: '/settings/system-tools' },
            ],
          },
          {
            label: 'Settings',
            items: [
              { id: 'settings-global', label: 'Settings', icon: 'SlidersHorizontal', href: '/settings/global' },
              { id: 'settings-audit-logs', label: 'Audit Logs', icon: 'ScrollText', href: '/settings/audit-logs' },
              { id: 'settings-text-to-speech', label: 'Text to Speech', icon: 'AudioLines', href: '/settings/text-to-speech' },
              { id: 'settings-autonumbering', label: 'Autonumbering', icon: 'Hash', href: '/settings/autonumbering' },
              { id: 'settings-notification-templates', label: 'Notifications', icon: 'Bell', href: '/settings/notification-templates' },
            ],
          },
        ],
      },
    ],
  },
];

// ---------------------------------------------------------------------------
// Routes
// ---------------------------------------------------------------------------
const routes = [
  { path: '/access/users', element: AccessUsersPage },
  { path: '/access/users/:userId', element: UserDetailPage, isDynamic: true },
  { path: '/access/roles', element: AccessRolesPage },
  { path: '/access/permissions', element: AccessPermissionsPage },
  { path: '/tenants', element: TenantsListPage },
  { path: '/tenants/new', element: CreateTenantPage },
  { path: '/tenants/:id', element: TenantDetailPage, isDynamic: true },
  { path: '/admin/alerts', element: AlertsPage },
  { path: '/admin/alerts/:id', element: AlertDetailPage, isDynamic: true },
  { path: '/settings', element: SettingsLandingPage },
  { path: '/settings/general', element: GlobalSettingsPage },
  { path: '/settings/global', element: GlobalSettingsPage },
  { path: '/settings/audit-logs', element: SettingsAuditLogsPage },
  { path: '/settings/text-to-speech', element: SettingsTextToSpeechPage },
  { path: '/settings/background-jobs', element: BackgroundJobsPage },
  { path: '/settings/background-jobs/:jobName', element: BackgroundJobDetailPage, isDynamic: true },
  { path: '/settings/system-tools', element: SystemToolsPage },
  { path: '/settings/notification-templates', element: NotificationTemplatesPage },
  { path: '/cms/content-blocks', element: ContentBlocksListPage },
  { path: '/cms/content-blocks/new', element: ContentBlockEditPage },
  { path: '/cms/content-blocks/:id', element: ContentBlockEditPage, isDynamic: true },
  { path: '/cms/content-wizard', element: ContentWizardPage },
  { path: '/cms/media', element: MediaLibraryPage },
];

// ---------------------------------------------------------------------------
// Workspace panels
// ---------------------------------------------------------------------------
const panels: WorkspacePanelConfig[] = [
  // Page panels — wrapped full-page components
  { id: 'access-users', title: 'Users', type: 'internal', category: 'page', componentKey: 'access-users', route: '/access/users' },
  { id: 'access-roles', title: 'Roles', type: 'internal', category: 'page', componentKey: 'access-roles', route: '/access/roles' },
  { id: 'access-permissions', title: 'Permissions', type: 'internal', category: 'page', componentKey: 'access-permissions', route: '/access/permissions' },
  { id: 'tenants', title: 'Tenants', type: 'internal', category: 'page', componentKey: 'tenants', route: '/tenants' },
  { id: 'platform-alerts', title: 'Platform Alerts', type: 'internal', category: 'page', componentKey: 'platform-alerts', route: '/admin/alerts' },
  { id: 'settings', title: 'Settings', type: 'internal', category: 'page', componentKey: 'settings-home', route: '/settings' },
  { id: 'settings-global', title: 'Settings', type: 'internal', category: 'page', componentKey: 'settings-global', route: '/settings/global' },
  { id: 'settings-audit-logs', title: 'Audit Logs', type: 'internal', category: 'page', componentKey: 'settings-audit-logs', route: '/settings/audit-logs' },
  { id: 'settings-text-to-speech', title: 'Text to Speech', type: 'internal', category: 'page', componentKey: 'settings-text-to-speech', route: '/settings/text-to-speech' },
  { id: 'background-jobs', title: 'Background Jobs', type: 'internal', category: 'page', componentKey: 'background-jobs', route: '/settings/background-jobs' },
  { id: 'settings-system-tools', title: 'System Tools', type: 'internal', category: 'page', componentKey: 'settings-system-tools', route: '/settings/system-tools' },
  { id: 'settings-notification-templates', title: 'Notifications', type: 'internal', category: 'page', componentKey: 'settings-notification-templates', route: '/settings/notification-templates' },
  { id: 'cms-content-blocks', title: 'Content Blocks', type: 'internal', category: 'page', componentKey: 'content-blocks', route: '/cms/content-blocks' },
  { id: 'cms-media', title: 'Media Library', type: 'internal', category: 'page', componentKey: 'media-library', route: '/cms/media' },
  // Micro-app panels — workspace-native, cross-panel communication
  { id: 'job-monitor', title: 'Job Monitor', description: 'Monitor background jobs and trigger actions.', type: 'internal', category: 'micro-app', componentKey: 'job-monitor', appCardId: '10', defaultWidth: 480 },
  { id: 'audit-trail', title: 'Audit Trail', description: 'Cross-referenced audit logs for job runs and commands.', type: 'internal', category: 'micro-app', componentKey: 'audit-trail', appCardId: '11', defaultWidth: 520 },
];

const panelComponents = {
  'access-users': wrapPage(AccessUsersPage),
  'access-roles': wrapPage(AccessRolesPage),
  'access-permissions': wrapPage(AccessPermissionsPage),
  tenants: wrapPage(TenantsListPage),
  'platform-alerts': wrapPage(AlertsPage),
  'settings-home': wrapPage(SettingsLandingPage),
  'settings-global': wrapPage(GlobalSettingsPage),
  'settings-audit-logs': wrapPage(SettingsAuditLogsPage),
  'settings-text-to-speech': wrapPage(SettingsTextToSpeechPage),
  'background-jobs': wrapPage(BackgroundJobsPage),
  'settings-system-tools': wrapPage(SystemToolsPage),
  'settings-notification-templates': wrapPage(NotificationTemplatesPage),
  'content-blocks': wrapPage(ContentBlocksListPage),
  'media-library': wrapPage(MediaLibraryPage),
  'job-monitor': BackgroundJobsPanel,
  'audit-trail': AuditLogPanel,
};

// ---------------------------------------------------------------------------
// Workspace templates
// ---------------------------------------------------------------------------
const workspaceTemplates: WorkspaceTemplate[] = [
  {
    id: 'job-auditor',
    name: 'Job Auditor',
    description: 'Monitor scheduled jobs and cross-reference audit logs.',
    icon: 'Timer',
    panels: ['job-monitor', 'audit-trail'],
    layout: 'split-horizontal',
  },
];

// ---------------------------------------------------------------------------
// Breadcrumbs
// ---------------------------------------------------------------------------
const breadcrumbs = [
  { pathPrefix: '/access', trail: ['Team'] },
  { pathPrefix: '/admin', trail: ['Admin', 'Infrastructure'] },
  { pathPrefix: '/tenants', trail: ['Admin', 'Infrastructure'] },
  { pathPrefix: '/settings', trail: ['Admin', 'Settings'] },
  { pathPrefix: '/cms', trail: ['Admin', 'Content'] },
];

// ---------------------------------------------------------------------------
// Module export
// ---------------------------------------------------------------------------
export const platformModule: AdminModule = {
  id: 'platform',
  name: 'Platform',
  navigation,
  routes,
  panels,
  panelComponents,
  defaultWorkspacePanels: ['job-monitor', 'audit-trail'],
  workspaceTemplates,
  breadcrumbs,
};
