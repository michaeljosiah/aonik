import { getManifestContextKey } from '@/modules/manifestCache';
import type { WorkspaceLayoutRecord } from './types';

function storageKey(contextKey: string) { return `aonik:workspace:layouts:${contextKey}`; }

export interface WorkspaceStorageState {
  activeLayoutId?: string;
  layouts: WorkspaceLayoutRecord[];
}

export function loadWorkspaceState(contextKey = getManifestContextKey()): WorkspaceStorageState {
  const raw = localStorage.getItem(storageKey(contextKey));
  if (!raw) {
    return { layouts: [] };
  }

  try {
    const parsed = JSON.parse(raw) as WorkspaceStorageState;
    return {
      activeLayoutId: parsed.activeLayoutId,
      layouts: parsed.layouts ?? [],
    };
  } catch {
    return { layouts: [] };
  }
}

export function saveWorkspaceState(state: WorkspaceStorageState, contextKey: string) {
  localStorage.setItem(storageKey(contextKey), JSON.stringify(state));
}
