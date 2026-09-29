import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import { MemoryRouter } from 'react-router-dom';
import { AdminScreenGate, AdminUnavailablePage } from './AdminScreenGate';
import { PageHeader } from '@/components/layout/aonik/PageHeader';
import { resolveAdminProfile } from './adminProfiles';
import { isProfilePathVisible } from './profileCatalog';
import type { AdminModule, RuntimeModuleManifest } from './types';

const state = vi.hoisted(() => ({ value: {} as object }));
vi.mock('./useModules', () => ({ useModules: () => state.value }));
vi.mock('@/auth', () => ({ useAuth: () => ({ logout: vi.fn() }) }));
vi.mock('./manifestCache', () => ({ invalidateModuleManifest: vi.fn() }));

const mounted = vi.fn();
function Page() { mounted(); return <PageHeader title="Product catalog" />; }
const modules: AdminModule[] = [{
  id: 'commerce', name: 'Commerce', requires: ['commerce'], navigation: [], panels: [], panelComponents: {}, breadcrumbs: [],
  routes: [{ path: '/products', element: Page, screen: { id: 'products', label: 'Products', permissions: { allOf: ['Products.Read'] } } }],
}];
function select(permissions: string[], enabledModules = ['commerce'], label: string | undefined = 'Menu') {
  const manifest: RuntimeModuleManifest = { businessType: 'food-commerce', enabledModules, modules: [], permissions, featureFlags: {} };
  const profile = resolveAdminProfile({ id: 'food', landingPage: 'products', screens: { products: label ? { label } : {} },
    navigation: [{ id: 'operations', label: 'Operations', items: [{ screen: 'products' }] }] }, modules, manifest, 'tenant');
  state.value = { modules, profile, landingPath: profile?.landingPath, isPathVisible: (path: string) => isProfilePathVisible(modules, profile, path) };
}
function renderPage() {
  return renderToStaticMarkup(<MemoryRouter initialEntries={['/products']}><AdminScreenGate><Page /></AdminScreenGate></MemoryRouter>);
}

describe('admin screen rendering', () => {
  beforeEach(() => mounted.mockClear());
  it('never mounts a page without its permission or backend module', () => {
    select([]);
    expect(renderPage()).toContain('This page is not available');
    select(['Products.Read'], []);
    expect(renderPage()).toContain('This page is not available');
    expect(mounted).not.toHaveBeenCalled();
  });
  it('renders the same domain label as the menu when allowed', () => {
    select(['Products.Read']);
    const html = renderPage();
    expect(html).toContain('Menu</h1>');
    expect(html).not.toContain('Products</h1>');
    expect(mounted).toHaveBeenCalledOnce();
  });
  it('uses the registered default heading when the profile has no override', () => {
    select(['Products.Read'], ['commerce'], '');
    expect(renderPage()).toContain('Products</h1>');
  });
  it('offers retry and sign out on a manifest failure without mounting business content', () => {
    select([]);
    const html = renderToStaticMarkup(<MemoryRouter><AdminUnavailablePage unavailable /></MemoryRouter>);
    expect(html).toContain('Retry');
    expect(html).toContain('Sign out');
    expect(mounted).not.toHaveBeenCalled();
  });
});
