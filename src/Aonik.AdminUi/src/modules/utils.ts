import { createElement } from 'react';
import type { ComponentType } from 'react';
import type { WorkspacePanelRenderProps } from '@/workspace/types';

/**
 * Wraps a regular page component so it can be rendered inside a workspace panel.
 * Strips workspace-specific props and renders the page component with no props.
 */
export function wrapPage(Component: ComponentType<Record<string, never>>): ComponentType<WorkspacePanelRenderProps> {
  function WrappedPage(_: WorkspacePanelRenderProps) {
    return createElement(Component);
  }
  WrappedPage.displayName = `WorkspaceWrapped(${Component.displayName || Component.name || 'Component'})`;
  return WrappedPage;
}
