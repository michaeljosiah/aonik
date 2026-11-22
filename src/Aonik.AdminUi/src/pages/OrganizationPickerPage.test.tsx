// @vitest-environment jsdom
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import { expect, it, vi } from 'vitest';
import { OrganizationPickerPage } from './OrganizationPickerPage';

const selection = vi.hoisted(() => ({ set: vi.fn() }));
vi.mock('@/auth', () => ({ useAuth: () => ({ isAuthenticated: true, isLoading: false, user: { id: 'operator' } }) }));
vi.mock('@/components/layout', () => ({ LoadingScreen: () => null }));
vi.mock('@/lib/tenantContext', () => ({ setSelectedTenant: selection.set, getLastSelectedTenantId: () => 'development', tenantEnvironmentLabel: (value: string) => value === 'Dev' ? 'Development' : 'Production' }));
vi.mock('@/modules/manifestCache', () => ({ invalidateModuleManifest: vi.fn() }));
vi.mock('@/hooks/useTenantBootstrap', () => ({ useTenantBootstrap: () => ({ state: { kind: 'picker', tenants: [
  { tenantId: 'production', name: 'Business workspace', environment: 'Prod' },
  { tenantId: 'development', name: 'Development workspace', environment: 'Dev' },
] }, refetch: vi.fn() }) }));

it('labels and prioritises the remembered workspace without automatically choosing it', async () => {
  vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
  const element = document.createElement('div'); document.body.appendChild(element); const root = createRoot(element);
  try {
    await act(async () => root.render(<MemoryRouter><OrganizationPickerPage /></MemoryRouter>));
    const choices = [...element.querySelectorAll('li button')];
    expect(choices[0].textContent).toContain('Development workspace');
    expect(choices[0].textContent).toContain('Last used');
    expect(selection.set).not.toHaveBeenCalled();

    await act(async () => (choices[1] as HTMLButtonElement).click());
    expect(selection.set).toHaveBeenCalledWith(expect.objectContaining({ tenantId: 'production' }), 'operator');
  } finally { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); }
});
