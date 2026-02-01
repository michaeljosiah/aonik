import { CashFlowForecasterPanel } from './apps/CashFlowForecasterPanel';
import { FraudDetectionPanel } from './apps/FraudDetectionPanel';
import { InvoiceManagerPanel } from './apps/InvoiceManagerPanel';
import { ReconciliationHubPanel } from './apps/ReconciliationHubPanel';
import type { ComponentType } from 'react';
import type { WorkspacePanelRenderProps } from './types';

export const workspacePanelComponents: Record<string, ComponentType<WorkspacePanelRenderProps>> = {
  'invoice-manager': InvoiceManagerPanel,
  'reconciliation-hub': ReconciliationHubPanel,
  'cash-flow-forecaster': CashFlowForecasterPanel,
  'fraud-detection': FraudDetectionPanel,
};
