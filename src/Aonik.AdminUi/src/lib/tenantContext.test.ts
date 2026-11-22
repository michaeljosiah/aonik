// @vitest-environment jsdom
import { beforeEach, expect, it } from 'vitest';
import { clearSelectedTenant, getLastSelectedTenantId, getSelectedTenant, setSelectedTenant } from './tenantContext';

beforeEach(() => { localStorage.clear(); sessionStorage.clear(); });

it('remembers the last choice for its user without retaining an active tenant after logout', () => {
  setSelectedTenant({ tenantId: 'development' }, 'first-user');
  clearSelectedTenant();

  expect(getSelectedTenant()).toBeNull();
  expect(getLastSelectedTenantId('first-user')).toBe('development');
  expect(getLastSelectedTenantId('second-user')).toBeNull();
  expect(getLastSelectedTenantId(undefined)).toBeNull();
});
