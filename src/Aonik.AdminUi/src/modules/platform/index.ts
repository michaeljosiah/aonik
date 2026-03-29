import type { AdminModule } from '../types';
import type { NavigationSection } from '@/types';
import type { WorkspacePanelConfig } from '@/workspace/types';
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
  SettingsGeneralPage,
  SettingsWebhooksPage,
  SettingsApiKeysPage,
  SettingsAuditLogsPage,
  SystemToolsPage,
  NotificationTemplatesPage,
} from '@/pages/settings';
import { ContentBlocksListPage } from '@/pages/ContentBlocksListPage';
import { ContentBlockEditPage } from '@/pages/ContentBlockEditPage';
import { MediaLibraryPage } from '@/pages/MediaLibraryPage';
import { wrapPage } from '../utils';

// ---------------------------------------------------------------------------
// Navigation
// ---------------------------------------------------------------------------
const navigation: NavigationSection[] = [
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
            label: 'Team',
            items: [
              { id: 'users', label: 'Users', icon: 'UserCog', href: '/access/users' },
              { id: 'roles', label: 'Roles', icon: 'Shield', href: '/access/roles' },
              { id: 'permissions', label: 'Permissions', icon: 'Key', href: '/access/permissions' },
            ],
          },
          {
            label: 'Content',
            items: [
              { id: 'content-blocks', label: 'Content Blocks', icon: 'Layers', href: '/cms/content-blocks' },
              { id: 'media-library', label: 'Media Library', icon: 'Image', href: '/cms/media' },
            ],
          },
          {
            label: 'Settings',
            items: [
              { id: 'settings-general', label: 'General', icon: 'Cog', href: '/settings/general' },
              { id: 'settings-webhooks', label: 'Webhooks', icon: 'Webhook', href: '/settings/webhooks' },
              { id: 'settings-api-keys', label: 'API Keys', icon: 'KeyRound', href: '/settings/api-keys' },
              { id: 'settings-audit-logs', label: 'Audit Logs', icon: 'ScrollText', href: '/settings/audit-logs' },
              { id: 'settings-autonumbering', label: 'Autonumbering', icon: 'Hash', href: '/settings/autonumbering' },
              { id: 'settings-notification-templates', label: 'Notifications', icon: 'Bell', href: '/settings/notification-templates' },
            ],
          },
        ],
      },
    ],
  },
  {
    id: 'system',
    audience: 'host',
    items: [
      {
        id: 'system',
        label: 'System',
        icon: 'Cog',
        audience: 'host',
        childGroups: [
          {
            label: 'Infrastructure',
            items: [
              { id: 'tenants', label: 'Tenants', icon: 'Building', href: '/tenants' },
            ],
          },
          {
            label: 'Tools',
            items: [
              { id: 'settings-system-tools', label: 'System Tools', icon: 'Wrench', href: '/settings/system-tools' },
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
  { path: '/settings', element: SettingsLandingPage },
  { path: '/settings/general', element: SettingsGeneralPage },
  { path: '/settings/webhooks', element: SettingsWebhooksPage },
  { path: '/settings/api-keys', element: SettingsApiKeysPage },
  { path: '/settings/audit-logs', element: SettingsAuditLogsPage },
  { path: '/settings/system-tools', element: SystemToolsPage },
  { path: '/settings/notification-templates', element: NotificationTemplatesPage },
  { path: '/cms/content-blocks', element: ContentBlocksListPage },
  { path: '/cms/content-blocks/new', element: ContentBlockEditPage },
  { path: '/cms/content-blocks/:id', element: ContentBlockEditPage, isDynamic: true },
  { path: '/cms/media', element: MediaLibraryPage },
];

// ---------------------------------------------------------------------------
// Workspace panels
// ---------------------------------------------------------------------------
const panels: WorkspacePanelConfig[] = [
  { id: 'access-users', title: 'Users', type: 'internal', componentKey: 'access-users', route: '/access/users' },
  { id: 'access-roles', title: 'Roles', type: 'internal', componentKey: 'access-roles', route: '/access/roles' },
  { id: 'access-permissions', title: 'Permissions', type: 'internal', componentKey: 'access-permissions', route: '/access/permissions' },
  { id: 'tenants', title: 'Tenants', type: 'internal', componentKey: 'tenants', route: '/tenants' },
  { id: 'settings', title: 'Settings', type: 'internal', componentKey: 'settings-home', route: '/settings' },
  { id: 'settings-general', title: 'General', type: 'internal', componentKey: 'settings-general', route: '/settings/general' },
  { id: 'settings-webhooks', title: 'Webhooks', type: 'internal', componentKey: 'settings-webhooks', route: '/settings/webhooks' },
  { id: 'settings-api-keys', title: 'API Keys', type: 'internal', componentKey: 'settings-api-keys', route: '/settings/api-keys' },
  { id: 'settings-audit-logs', title: 'Audit Logs', type: 'internal', componentKey: 'settings-audit-logs', route: '/settings/audit-logs' },
  { id: 'settings-system-tools', title: 'System Tools', type: 'internal', componentKey: 'settings-system-tools', route: '/settings/system-tools' },
  { id: 'settings-notification-templates', title: 'Notifications', type: 'internal', componentKey: 'settings-notification-templates', route: '/settings/notification-templates' },
  { id: 'cms', title: 'Content', type: 'internal', componentKey: 'placeholder', route: '/cms' },
  { id: 'cms-content-blocks', title: 'Content Blocks', type: 'internal', componentKey: 'content-blocks', route: '/cms/content-blocks' },
  { id: 'cms-media', title: 'Media Library', type: 'internal', componentKey: 'media-library', route: '/cms/media' },
];

const panelComponents = {
  'access-users': wrapPage(AccessUsersPage),
  'access-roles': wrapPage(AccessRolesPage),
  'access-permissions': wrapPage(AccessPermissionsPage),
  tenants: wrapPage(TenantsListPage),
  'settings-home': wrapPage(SettingsLandingPage),
  'settings-general': wrapPage(SettingsGeneralPage),
  'settings-webhooks': wrapPage(SettingsWebhooksPage),
  'settings-api-keys': wrapPage(SettingsApiKeysPage),
  'settings-audit-logs': wrapPage(SettingsAuditLogsPage),
  'settings-system-tools': wrapPage(SystemToolsPage),
  'settings-notification-templates': wrapPage(NotificationTemplatesPage),
  'content-blocks': wrapPage(ContentBlocksListPage),
  'media-library': wrapPage(MediaLibraryPage),
};

// ---------------------------------------------------------------------------
// Breadcrumbs
// ---------------------------------------------------------------------------
const breadcrumbs = [
  { pathPrefix: '/access', trail: ['Admin', 'Team'] },
  { pathPrefix: '/tenants', trail: ['System', 'Tenants'] },
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
  breadcrumbs,
};
