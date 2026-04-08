import type { ComponentType } from 'react';
import type { WorkspacePanelRenderProps } from './types';
import { getAggregatedPanelComponents } from '@/modules/registry';

/**
 * Workspace panel component map — aggregated from module definitions.
 * Each module contributes its own componentKey -> Component mappings.
 */
export const workspacePanelComponents: Record<string, ComponentType<WorkspacePanelRenderProps>> = {
  ...getAggregatedPanelComponents(),
};
