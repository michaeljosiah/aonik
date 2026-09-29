import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { loadWorkspaceState, saveWorkspaceState } from './storage';
vi.mock('@/modules/manifestCache', () => ({ getManifestContextKey: () => 'current' }));

describe('workspace identity isolation', () => {
  beforeEach(() => {
    const values = new Map<string, string>();
    vi.stubGlobal('localStorage', { getItem: (key: string) => values.get(key) ?? null,
      setItem: (key: string, value: string) => values.set(key, value) });
  });
  afterEach(() => vi.unstubAllGlobals());
  it('keeps a late save bound to its original user and tenant', () => {
    const original = JSON.stringify(['user-a', 'food']);
    const otherTenant = JSON.stringify(['user-a', 'arke']);
    const otherUser = JSON.stringify(['user-b', 'food']);
    saveWorkspaceState({ activeLayoutId: 'arke-layout', layouts: [] }, otherTenant);
    saveWorkspaceState({ activeLayoutId: 'late-food-layout', layouts: [] }, original);
    expect(loadWorkspaceState(otherTenant).activeLayoutId).toBe('arke-layout');
    expect(loadWorkspaceState(otherUser).layouts).toEqual([]);
    expect(loadWorkspaceState(original).activeLayoutId).toBe('late-food-layout');
  });
  it('does not attribute legacy unscoped layouts to a new user', () => {
    localStorage.setItem('aonik:workspace:layouts', JSON.stringify({ activeLayoutId: 'unknown-owner', layouts: [] }));
    expect(loadWorkspaceState('new-user').activeLayoutId).toBeUndefined();
    expect(localStorage.getItem('aonik:workspace:layouts')).toContain('unknown-owner');
  });
});
