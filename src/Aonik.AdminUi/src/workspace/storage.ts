import type { WorkspaceLayoutRecord } from './types';

const storageKey = 'aonik:workspace:layouts';

export interface WorkspaceStorageState {
  activeLayoutId?: string;
  layouts: WorkspaceLayoutRecord[];
}

export function loadWorkspaceState(): WorkspaceStorageState {
  const raw = localStorage.getItem(storageKey);
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

export function saveWorkspaceState(state: WorkspaceStorageState) {
  localStorage.setItem(storageKey, JSON.stringify(state));
}
