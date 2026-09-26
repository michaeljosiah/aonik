// Visual catalog for step / node kinds. Mirrors STEP_KIND in
// templates/aonik-admin-starterkit/screens/workflows.jsx and NODE_KINDS
// in workflow-editor.jsx. The icon strings here resolve to lucide-react
// components in the consumer (StepRail, NodeShape) — kept as strings here
// so the catalog has no JSX dependency and can be reused by both pages.

import type { StepKind, EditorNodeKind } from './workflowTypes';

// Each of the ten kinds has its own categorical token (--chart-1..10), so
// kinds stay distinct and the tints flip with the theme. Consumers derive
// fills with color-mix()/fill-opacity; glyphs on a tint use
// --primary-foreground (white on the darker light-mode hues, dark on the
// lighter dark-mode hues).

export interface StepKindMeta {
  /** lucide-react icon name. */
  icon: string;
  label: string;
  tint: string;
  /** Editor-only: number of input ports (0 for triggers). */
  inputs?: number;
  /** Editor-only: number of output ports (decision/loop = 2; end = 0). */
  outputs?: number;
  /** Editor-only: human-readable description, used in palette. */
  desc?: string;
  /** Editor-only: default param values when dropping a fresh node. */
  defaults?: Record<string, string | number>;
}

// List-page step rail kinds. Now identical to NODE_KIND below since the
// API normalises every node visit to the canonical 10-kind enum (the
// template's legacy 'start' / 'ledger' aliases became 'trigger' / 'tool'
// after the de-mock).
export const STEP_KIND: Record<StepKind, StepKindMeta> = {
  trigger: { icon: 'Zap', label: 'Trigger', tint: 'var(--chart-1)' },
  tool: { icon: 'Wrench', label: 'Tool call', tint: 'var(--chart-6)' },
  agent: { icon: 'Sparkles', label: 'Sub-agent', tint: 'var(--chart-4)' },
  decision: { icon: 'GitFork', label: 'Decision', tint: 'var(--chart-7)' },
  human: { icon: 'Users', label: 'Human approval', tint: 'var(--chart-10)' },
  wait: { icon: 'Clock', label: 'Wait', tint: 'var(--chart-2)' },
  end: { icon: 'Check', label: 'End', tint: 'var(--chart-8)' },
  notify: { icon: 'Send', label: 'Notify', tint: 'var(--chart-3)' },
  emit: { icon: 'Zap', label: 'Emit event', tint: 'var(--chart-5)' },
  loop: { icon: 'RefreshCw', label: 'Loop', tint: 'var(--chart-9)' },
};

// Editor node kinds (read from workflow-editor.jsx NODE_KINDS) — superset
// of the list step kinds with input/output port counts and palette defaults.
export const NODE_KIND: Record<EditorNodeKind, StepKindMeta> = {
  trigger: {
    label: 'Trigger',
    tint: 'var(--chart-1)',
    icon: 'Zap',
    desc: 'Where the workflow starts',
    inputs: 0,
    outputs: 1,
    defaults: { source: 'banking.transaction.received', filter: '' },
  },
  tool: {
    label: 'Tool call',
    tint: 'var(--chart-6)',
    icon: 'Wrench',
    desc: 'Invoke a registered tool',
    inputs: 1,
    outputs: 1,
    defaults: { tool: 'search_invoices', params: '{}' },
  },
  agent: {
    label: 'Sub-agent',
    tint: 'var(--chart-4)',
    icon: 'Sparkles',
    desc: 'Hand off to another agent',
    inputs: 1,
    outputs: 1,
    defaults: { agent: 'Billing', task: 'Score match candidates' },
  },
  decision: {
    label: 'Decision',
    tint: 'var(--chart-7)',
    icon: 'GitFork',
    desc: 'Branch on a condition',
    inputs: 1,
    outputs: 2,
    defaults: { expr: 'amount > 50000', yesLabel: 'Yes', noLabel: 'No' },
  },
  human: {
    label: 'Human approval',
    tint: 'var(--chart-10)',
    icon: 'Users',
    desc: 'Pause for a person to decide',
    inputs: 1,
    outputs: 1,
    defaults: { group: 'Treasury', sla: '4h' },
  },
  wait: {
    label: 'Wait',
    tint: 'var(--chart-2)',
    icon: 'Clock',
    desc: 'Delay for a fixed duration',
    inputs: 1,
    outputs: 1,
    defaults: { duration: '7d' },
  },
  notify: {
    label: 'Notify',
    tint: 'var(--chart-3)',
    icon: 'Send',
    desc: 'Email, SMS, or Slack message',
    inputs: 1,
    outputs: 1,
    defaults: { channel: 'email', template: 'receipt_v2' },
  },
  emit: {
    label: 'Emit event',
    tint: 'var(--chart-5)',
    icon: 'Zap',
    desc: 'Fire an event back into the bus',
    inputs: 1,
    outputs: 1,
    defaults: { event: 'workflow.completed' },
  },
  loop: {
    label: 'Loop',
    tint: 'var(--chart-9)',
    icon: 'RefreshCw',
    desc: 'Iterate over a collection',
    inputs: 1,
    outputs: 2,
    defaults: { over: 'invoices', maxIterations: 100 },
  },
  end: {
    label: 'End',
    tint: 'var(--chart-8)',
    icon: 'Check',
    desc: 'Workflow completes',
    inputs: 1,
    outputs: 0,
    defaults: {},
  },
};
