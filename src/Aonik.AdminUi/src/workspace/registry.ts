import type { WorkspacePanelConfig } from './types';

export const workspacePanelRegistry: WorkspacePanelConfig[] = [
  {
    id: 'invoice-manager',
    title: 'Invoice Manager',
    description: 'Create, manage, and track invoices with AI-assisted insights.',
    type: 'internal',
    componentKey: 'invoice-manager',
    appCardId: '1',
    defaultWidth: 520,
  },
  {
    id: 'reconciliation-hub',
    title: 'Reconciliation Hub',
    description: 'AI-powered matching and discrepancy detection.',
    type: 'internal',
    componentKey: 'reconciliation-hub',
    appCardId: '2',
    defaultWidth: 520,
  },
  {
    id: 'cash-flow-forecaster',
    title: 'Cash Flow Forecaster',
    description: 'Predict cash positions and run scenario planning.',
    type: 'internal',
    componentKey: 'cash-flow-forecaster',
    appCardId: '3',
    defaultWidth: 520,
  },
  {
    id: 'fraud-detection',
    title: 'Fraud Detection',
    description: 'Real-time anomaly detection and explainable alerts.',
    type: 'internal',
    componentKey: 'fraud-detection',
    appCardId: '4',
    defaultWidth: 520,
  },
];

export const defaultWorkspaceLayoutPanels = ['invoice-manager', 'reconciliation-hub'];

export function getWorkspacePanelConfig(panelId: string) {
  return workspacePanelRegistry.find((panel) => panel.id === panelId);
}

export function getWorkspacePanelForApp(appCardId: string) {
  return workspacePanelRegistry.find((panel) => panel.appCardId === appCardId);
}
