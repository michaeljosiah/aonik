// Mock data + types for the Workflows screens. Derived 1:1 from the
// starterkit templates (templates/aonik-admin-starterkit/screens/workflows.jsx,
// workflow-editor-screen.jsx). Replace with a backend-backed service later;
// the shape is intentionally close to what a workflow registry API would
// return so the swap is mechanical.

export type StepKind =
  | 'start'
  | 'tool'
  | 'agent'
  | 'decision'
  | 'human'
  | 'wait'
  | 'end'
  | 'notify'
  | 'emit'
  | 'ledger'
  | 'loop';

export type WorkflowState = 'active' | 'paused' | 'draft';

export interface WorkflowStep {
  kind: StepKind;
  label: string;
  meta?: string;
}

export interface WorkflowSummary {
  id: string;
  name: string;
  desc: string;
  owner: string;
  ownerColor: string;
  contributors: string[];
  triggers: number;
  runsToday: number;
  /** 0..1 success ratio. */
  success: number;
  /** Average run duration in milliseconds. */
  avgMs: number;
  state: WorkflowState;
  version: string;
  /** Human-readable relative time, e.g. "3d ago". */
  updated: string;
  autoRetry: boolean;
  steps: WorkflowStep[];
}

// ─── Editor-specific types ────────────────────────────────────────
// Full graph definition used by the canvas. The list-page summary type
// above is a denormalised view of the same workflow concept.

export type EditorNodeKind =
  | 'trigger'
  | 'tool'
  | 'agent'
  | 'decision'
  | 'human'
  | 'wait'
  | 'notify'
  | 'emit'
  | 'loop'
  | 'end';

export interface WorkflowNodeParams {
  // trigger
  source?: string;
  filter?: string;
  // tool
  tool?: string;
  params?: string;
  // agent
  agent?: string;
  task?: string;
  // decision
  expr?: string;
  yesLabel?: string;
  noLabel?: string;
  // human
  group?: string;
  sla?: string;
  // wait
  duration?: string;
  // notify
  channel?: string;
  template?: string;
  // emit
  event?: string;
  // loop
  over?: string;
  maxIterations?: number | string;
}

export interface WorkflowNode {
  id: string;
  kind: EditorNodeKind;
  label: string;
  x: number;
  y: number;
  summary?: string;
  notes?: string;
  params: WorkflowNodeParams;
}

export interface WorkflowEdge {
  id: string;
  from: string;
  to: string;
  /** Output port index on the `from` node (decision: 0=yes, 1=no; loop: 0=body, 1=done). */
  fromIdx?: number;
  label?: string;
}

export interface WorkflowGraph {
  id: string;
  name: string;
  desc: string;
  version: string;
  ownerColor: string;
  nodes: WorkflowNode[];
  edges: WorkflowEdge[];
}

export interface WorkflowComment {
  id: string;
  x: number;
  y: number;
  author: string;
  body: string;
}

export interface WorkflowRunSummary {
  id: string;
  when: string;
  status: 'success' | 'held' | 'failed';
  duration: string;
  by: string;
  /** Ordered node ids visited in this run. */
  sequence: string[];
  total: number;
}

export interface WorkflowVersion {
  id: string;
  tag: string;
  when: string;
  by: string;
  byColor: string;
  message: string;
}

// ─── List page mock data ─────────────────────────────────────────
// Mirrors the WORKFLOWS array in screens/workflows.jsx exactly.

export const MOCK_WORKFLOWS: WorkflowSummary[] = [
  {
    id: 'match_and_apply',
    name: 'Match & apply',
    desc: 'Reconcile invoice → bank txn, draft an entry, surface it for review when over policy ceiling.',
    owner: 'Billing Agent',
    ownerColor: '#eb5c37',
    contributors: ['Ledger Agent', 'Compliance Agent'],
    triggers: 4,
    runsToday: 318,
    success: 0.962,
    avgMs: 2400,
    state: 'active',
    version: 'v1.4',
    updated: '3d ago',
    autoRetry: true,
    steps: [
      { kind: 'start', label: 'On bank txn' },
      { kind: 'tool', label: 'search_invoices', meta: 'amount ± £0.01' },
      { kind: 'agent', label: 'Billing · score match', meta: 'confidence ≥ 0.85' },
      { kind: 'decision', label: 'Above ceiling?', meta: '£50,000' },
      { kind: 'human', label: 'Treasury approval', meta: 'if breached' },
      { kind: 'ledger', label: 'Draft journal entry', meta: 'AR · 1200' },
      { kind: 'notify', label: 'Notify customer', meta: 'receipt email' },
      { kind: 'end', label: 'Match applied' },
    ],
  },
  {
    id: 'sweep_unmatched',
    name: 'Sweep unmatched',
    desc: 'Hourly retry pass for invoices that fell through earlier. Loosens fuzzy matching as time passes.',
    owner: 'Billing Agent',
    ownerColor: '#eb5c37',
    contributors: [],
    triggers: 1,
    runsToday: 24,
    success: 0.71,
    avgMs: 18200,
    state: 'active',
    version: 'v0.9',
    updated: '1w ago',
    autoRetry: false,
    steps: [
      { kind: 'start', label: 'Hourly tick' },
      { kind: 'tool', label: 'list_open_invoices', meta: 'aged > 24h' },
      { kind: 'agent', label: 'Billing · fuzzy match', meta: 'tier escalates' },
      { kind: 'decision', label: 'Match found?' },
      { kind: 'tool', label: 'apply_match', meta: 'auto-apply' },
      { kind: 'end', label: 'Sweep complete' },
    ],
  },
  {
    id: 'dunning_cadence',
    name: 'Dunning cadence',
    desc: 'Send overdue reminders on a per-customer rhythm. Escalates tone every 7 days, hands to phone after day 21.',
    owner: 'Dunning Agent',
    ownerColor: '#5facbd',
    contributors: ['Compliance Agent'],
    triggers: 2,
    runsToday: 14,
    success: 0.88,
    avgMs: 4100,
    state: 'paused',
    version: 'v2.0',
    updated: '11d ago',
    autoRetry: true,
    steps: [
      { kind: 'start', label: 'Invoice overdue' },
      { kind: 'tool', label: 'lookup_customer', meta: 'segment + tier' },
      { kind: 'decision', label: 'Days overdue', meta: '7 / 14 / 21' },
      { kind: 'agent', label: 'Dunning · compose', meta: 'tone keyed to days' },
      { kind: 'human', label: 'Approve outbound', meta: 'tier-1 only' },
      { kind: 'notify', label: 'Send email', meta: 'or SMS' },
      { kind: 'wait', label: 'Wait 7 days', meta: 'or until paid' },
      { kind: 'end', label: 'Cadence step done' },
    ],
  },
  {
    id: 'forward_quote',
    name: 'Forward quote',
    desc: 'Quote a forward FX contract for cross-border invoices. Fetches rate fixings, calculates markup, drafts the contract.',
    owner: 'FX Agent',
    ownerColor: '#3ab795',
    contributors: ['Compliance Agent'],
    triggers: 2,
    runsToday: 8,
    success: 0.99,
    avgMs: 1800,
    state: 'active',
    version: 'v1.1',
    updated: '6d ago',
    autoRetry: false,
    steps: [
      { kind: 'start', label: 'Cross-border invoice' },
      { kind: 'tool', label: 'fetch_fx_fix', meta: 'CME · WMR' },
      { kind: 'agent', label: 'FX · price quote', meta: '+spread' },
      { kind: 'tool', label: 'draft_forward_contract' },
      { kind: 'human', label: 'Counterparty signs' },
      { kind: 'end', label: 'Quote delivered' },
    ],
  },
  {
    id: 'kyc_recheck',
    name: 'KYC re-check',
    desc: 'Re-screen counterparty against sanctions and PEP lists. Triggered on a 90-day rotation or risk-flag changes.',
    owner: 'Compliance Agent',
    ownerColor: '#7b76b6',
    contributors: [],
    triggers: 3,
    runsToday: 6,
    success: 0.99,
    avgMs: 920,
    state: 'active',
    version: 'v3.2',
    updated: '2d ago',
    autoRetry: true,
    steps: [
      { kind: 'start', label: 'On schedule · or flag' },
      { kind: 'tool', label: 'fetch_sanctions_lists' },
      { kind: 'tool', label: 'screen_counterparty', meta: 'OFAC · UN · EU · UK' },
      { kind: 'decision', label: 'Hit?' },
      { kind: 'human', label: 'Compliance review', meta: 'if hit' },
      { kind: 'emit', label: 'compliance.recheck.done' },
      { kind: 'end', label: 'Cleared' },
    ],
  },
  {
    id: 'monthly_close',
    name: 'Month-end close',
    desc: 'Sequences the close playbook end-to-end. Accruals, FX revaluation, intercompany eliminations, sign-off.',
    owner: 'Close Agent',
    ownerColor: '#0097a9',
    contributors: ['Ledger Agent', 'FX Agent'],
    triggers: 1,
    runsToday: 0,
    success: 0.93,
    avgMs: 384000,
    state: 'active',
    version: 'v2.7',
    updated: '17d ago',
    autoRetry: false,
    steps: [
      { kind: 'start', label: 'Last business day' },
      { kind: 'agent', label: 'Ledger · post accruals' },
      { kind: 'agent', label: 'FX · revalue balances' },
      { kind: 'agent', label: 'Ledger · intercompany' },
      { kind: 'human', label: 'Controller sign-off', meta: 'mandatory' },
      { kind: 'tool', label: 'lock_period' },
      { kind: 'notify', label: 'Close package · email' },
      { kind: 'end', label: 'Period closed' },
    ],
  },
  {
    id: 'spend_anomaly',
    name: 'Spend anomaly review',
    desc: 'When a spend category exceeds its 30-day rolling average by more than σ, surface a narrative for review.',
    owner: 'Insights Agent',
    ownerColor: '#d4a843',
    contributors: [],
    triggers: 1,
    runsToday: 3,
    success: 0.85,
    avgMs: 5400,
    state: 'draft',
    version: 'v0.3',
    updated: '4h ago',
    autoRetry: false,
    steps: [
      { kind: 'start', label: 'Daily roll-up' },
      { kind: 'tool', label: 'aggregate_spend', meta: 'by category' },
      { kind: 'decision', label: 'Anomaly?', meta: '> 2σ' },
      { kind: 'agent', label: 'Insights · narrative' },
      { kind: 'notify', label: 'Post to My Space' },
      { kind: 'end', label: 'Review filed' },
    ],
  },
];

// ─── Editor-page seed data ────────────────────────────────────────
// Default graph the editor opens to — matches DEFAULT_WORKFLOW from the
// template's workflow-editor-screen.jsx.

export const DEFAULT_WORKFLOW_GRAPH: WorkflowGraph = {
  id: 'match_and_apply',
  name: 'Match & apply',
  desc: 'Reconcile invoice → bank txn, draft an entry, surface for review when over policy ceiling.',
  version: 'v1.4',
  ownerColor: '#eb5c37',
  nodes: [
    {
      id: 'n1',
      kind: 'trigger',
      label: 'On bank txn',
      x: 64,
      y: 240,
      summary: 'banking.transaction.received',
      params: { source: 'banking.transaction.received', filter: 'amount > 0' },
    },
    {
      id: 'n2',
      kind: 'tool',
      label: 'Find candidate invoices',
      x: 320,
      y: 240,
      summary: 'search_invoices',
      params: { tool: 'search_invoices', params: '{ "amount_eps": 0.01 }' },
    },
    {
      id: 'n3',
      kind: 'agent',
      label: 'Score match',
      x: 576,
      y: 240,
      summary: 'Billing · confidence ≥ 0.85',
      params: { agent: 'Billing', task: 'Score candidate invoices and pick best match. Cite reasoning.' },
    },
    {
      id: 'n4',
      kind: 'decision',
      label: 'Above ceiling?',
      x: 832,
      y: 240,
      summary: 'amount > 50000',
      params: { expr: 'amount > 50000', yesLabel: 'Yes', noLabel: 'No' },
    },
    {
      id: 'n5',
      kind: 'human',
      label: 'Treasury approval',
      x: 1088,
      y: 144,
      summary: 'group: Treasury · 4h SLA',
      params: { group: 'Treasury', sla: '4h' },
    },
    {
      id: 'n6',
      kind: 'tool',
      label: 'Draft journal entry',
      x: 1088,
      y: 336,
      summary: 'AR · 1200',
      params: { tool: 'draft_journal_entry', params: '{ "account": "1200" }' },
    },
    {
      id: 'n7',
      kind: 'notify',
      label: 'Send receipt',
      x: 1344,
      y: 240,
      summary: 'email · receipt_v2',
      params: { channel: 'email', template: 'receipt_v2' },
    },
    { id: 'n8', kind: 'end', label: 'Match applied', x: 1600, y: 240, params: {} },
  ],
  edges: [
    { id: 'e1', from: 'n1', to: 'n2' },
    { id: 'e2', from: 'n2', to: 'n3' },
    { id: 'e3', from: 'n3', to: 'n4' },
    { id: 'e4', from: 'n4', to: 'n5', fromIdx: 0, label: 'yes' },
    { id: 'e5', from: 'n4', to: 'n6', fromIdx: 1, label: 'no' },
    { id: 'e6', from: 'n5', to: 'n6' },
    { id: 'e7', from: 'n6', to: 'n7' },
    { id: 'e8', from: 'n7', to: 'n8' },
  ],
};

export const DEFAULT_COMMENTS: WorkflowComment[] = [
  {
    id: 'c1',
    x: 1024,
    y: 60,
    author: 'Maria · Treasury',
    body: 'Approval ceiling raised from £25K → £50K on 12 Apr per CFO memo.',
  },
];

export const DEFAULT_RUNS: WorkflowRunSummary[] = [
  {
    id: 'run_8421',
    when: '2m ago',
    status: 'success',
    duration: '2.4s',
    by: 'auto · banking.transaction.received',
    sequence: ['n1', 'n2', 'n3', 'n4', 'n6', 'n7', 'n8'],
    total: 7,
  },
  {
    id: 'run_8418',
    when: '14m ago',
    status: 'success',
    duration: '2.5s',
    by: 'auto · banking.transaction.received',
    sequence: ['n1', 'n2', 'n3', 'n4', 'n6', 'n7', 'n8'],
    total: 7,
  },
  {
    id: 'run_8412',
    when: '38m ago',
    status: 'held',
    duration: '7m 14s',
    by: 'held · over ceiling',
    sequence: ['n1', 'n2', 'n3', 'n4', 'n5'],
    total: 7,
  },
];

export const DEFAULT_VERSIONS: WorkflowVersion[] = [
  {
    id: 'v1.4',
    tag: 'v1.4',
    when: 'today',
    by: 'Maria',
    byColor: '#eb5c37',
    message: 'Raised approval ceiling £25K → £50K. Added Treasury approval branch.',
  },
  {
    id: 'v1.3',
    tag: 'v1.3',
    when: '8d ago',
    by: 'Aonik',
    byColor: '#055a60',
    message: 'Auto-link to receipt template after journal entry posted.',
  },
  {
    id: 'v1.2',
    tag: 'v1.2',
    when: '21d ago',
    by: 'Rafa',
    byColor: '#7b76b6',
    message: 'Switch matcher from regex to fuzzy + score.',
  },
  {
    id: 'v1.1',
    tag: 'v1.1',
    when: '2 mo ago',
    by: 'Aonik',
    byColor: '#055a60',
    message: 'Initial draft auto-generated from playbook.',
  },
];

// ─── Helpers ─────────────────────────────────────────────────────

export function formatDuration(ms: number): string {
  if (ms < 1000) return `${ms}ms`;
  if (ms < 60_000) return `${(ms / 1000).toFixed(1)}s`;
  if (ms < 3_600_000) return `${Math.round(ms / 60_000)}m`;
  return `${(ms / 3_600_000).toFixed(1)}h`;
}
