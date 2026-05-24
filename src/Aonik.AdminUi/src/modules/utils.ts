import { createElement } from 'react';
import type { ComponentType } from 'react';
import { Navigate } from 'react-router-dom';
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

/**
 * Returns a stable component that renders a `<Navigate replace />` to the given
 * path. Used as a route `element` to collapse vestigial landing pages into the
 * destination page (e.g. /compliance → /compliance/documents).
 */
export function redirectTo(targetPath: string): ComponentType {
  function RedirectComponent() {
    return createElement(Navigate, { to: targetPath, replace: true });
  }
  RedirectComponent.displayName = `RedirectTo(${targetPath})`;
  return RedirectComponent;
}
