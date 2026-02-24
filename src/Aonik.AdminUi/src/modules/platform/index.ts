import type { AdminModule } from '../types';
import type { NavigationSection } from '@/types';
import type { WorkspacePanelConfig } from '@/workspace/types';
import {
  AccessUsersPage,
  AccessRolesPage,
  AccessPermissionsPage,
  UserDetailPage,
  TenantsListPage,
  CreateTenantPage,
  TenantDetailPage,
  SystemToolsPage,
  ContentBlocksListPage,
  ContentBlockEditPage,
  MediaLibraryPage,
} from '@/pages';
import { wrapPage } from '../utils';

// ---------------------------------------------------------------------------
// Navigation
// ---------------------------------------------------------------------------
const navigation: NavigationSection[] = [
  {
    id: 'platform-core-access',
    label: 'Finance',
    items: [
      {
        id: 'identity-access',
        label: 'Access',
        icon: 'Users',
        viewAllHref: '/access/users',
        viewAllLabel: 'View all',
        audience: 'host',
        childGroups: [
          {
            label: 'Team',
            items: [
              { id: 'users', label: 'Users', icon: 'UserCog', href: '/access/users', audience: 'host' },
            ],
          },
          {
            label: 'Permissions',
            items: [
              { id: 'roles', label: 'Roles', icon: 'Shield', href: '/access/roles', audience: 'host' },
              { id: 'permissions', label: 'Permissions', icon: 'Key', href: '/access/permissions', audience: 'host' },
            ],
          },
        ],
      },
    ],
  },
  {
    id: 'platform-admin',
    label: 'Admin',
    audience: 'host',
    items: [
      {
        id: 'tenants',
        label: 'Tenants',
        icon: 'Building',
        href: '/tenants',
      },
      {
        id: 'system-tools',
        label: 'System Tools',
        icon: 'Wrench',
        href: '/settings/system-tools',
      },
      {
        id: 'catalog',
        label: 'Catalog',
        icon: 'Store',
        viewAllHref: '/catalog',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'Overview',
            items: [
              { id: 'catalog-overview', label: 'Home', icon: 'Store', href: '/catalog' },
            ],
          },
        ],
      },
      {
        id: 'cms',
        label: 'Content',
        icon: 'Layers',
        viewAllHref: '/cms/content-blocks',
        viewAllLabel: 'View all',
        childGroups: [
          {
            label: 'Library',
            items: [
              { id: 'content-blocks', label: 'Content Blocks', icon: 'Layers', href: '/cms/content-blocks' },
              { id: 'media-library', label: 'Media Library', icon: 'Image', href: '/cms/media' },
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
  { path: '/settings/system-tools', element: SystemToolsPage },
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
  { id: 'settings', title: 'Settings', type: 'internal', componentKey: 'placeholder', route: '/settings' },
  { id: 'settings-general', title: 'General', type: 'internal', componentKey: 'placeholder', route: '/settings/general' },
  { id: 'settings-webhooks', title: 'Webhooks', type: 'internal', componentKey: 'placeholder', route: '/settings/webhooks' },
  { id: 'settings-api-keys', title: 'API Keys', type: 'internal', componentKey: 'placeholder', route: '/settings/api-keys' },
  { id: 'settings-audit-logs', title: 'Audit Logs', type: 'internal', componentKey: 'placeholder', route: '/settings/audit-logs' },
  { id: 'cms', title: 'Content', type: 'internal', componentKey: 'placeholder', route: '/cms' },
  { id: 'cms-content-blocks', title: 'Content Blocks', type: 'internal', componentKey: 'content-blocks', route: '/cms/content-blocks' },
  { id: 'cms-media', title: 'Media Library', type: 'internal', componentKey: 'media-library', route: '/cms/media' },
];

const panelComponents = {
  'access-users': wrapPage(AccessUsersPage),
  'access-roles': wrapPage(AccessRolesPage),
  'access-permissions': wrapPage(AccessPermissionsPage),
  tenants: wrapPage(TenantsListPage),
  'content-blocks': wrapPage(ContentBlocksListPage),
  'media-library': wrapPage(MediaLibraryPage),
};

// ---------------------------------------------------------------------------
// Breadcrumbs
// ---------------------------------------------------------------------------
const breadcrumbs = [
  { pathPrefix: '/access', trail: ['Users & Access'] },
  { pathPrefix: '/tenants', trail: ['Tenants'] },
  { pathPrefix: '/settings', trail: ['Settings'] },
  { pathPrefix: '/cms', trail: ['Content'] },
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
