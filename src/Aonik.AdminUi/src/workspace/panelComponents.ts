import { PlaceholderPanel } from './apps/PlaceholderPanel';
import type { ComponentType } from 'react';
import type { WorkspacePanelRenderProps } from './types';
import { getAggregatedPanelComponents } from '@/modules/registry';

/**
 * Workspace panel component map — now aggregated from module definitions.
 * Each module contributes its own componentKey -> Component mappings.
 * The 'placeholder' component is always available as a fallback.
 */
export const workspacePanelComponents: Record<string, ComponentType<WorkspacePanelRenderProps>> = {
  placeholder: PlaceholderPanel,
  ...getAggregatedPanelComponents(),
};
