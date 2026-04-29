// Demo data for the agent detail page — directly ported from
// templates/aonik-admin-starterkit/screens/agent-detail.jsx so the visual
// fidelity matches the starter kit while the real backend grows the
// supporting endpoints (sub-agent links, tool usage stats, skills, MCP).
//
// The detail page renders this demo content alongside the real agent
// identity (name, description, model). When the backend ships sub-agent
// links / usage rollups / skills / MCP, replace the calls to these helpers
// with real service calls, not the page itself.

import type { AgentGlyph } from './agentMeta';

export interface DemoSubAgent {
  id: string;
  name: string;
  glyph: AgentGlyph;
  color: string;
  role: string;
  autonomy: 'auto' | 'propose' | 'block';
  calls: number;
  avgMs: number;
  last: string;
  successRate: number;
  sla: number;
}

export const DEMO_SUB_AGENTS: DemoSubAgent[] = [
  {
    id: 'ledger',
    name: 'Ledger Agent',
    glyph: 'columns',
    color: '#055a60',
    role: 'Posts journal entries from matched txns',
    autonomy: 'auto',
    calls: 142,
    avgMs: 218,
    last: '2m',
    successRate: 98.4,
    sla: 99,
  },
  {
    id: 'fx',
    name: 'FX Agent',
    glyph: 'wave',
    color: '#3ab795',
    role: 'Quotes rates for cross-currency invoices',
    autonomy: 'propose',
    calls: 38,
    avgMs: 412,
    last: '8m',
    successRate: 94.7,
    sla: 98,
  },
  {
    id: 'compl',
    name: 'Compliance Agent',
    glyph: 'shield',
    color: '#7b76b6',
    role: 'KYC re-checks before any new counterparty',
    autonomy: 'block',
    calls: 12,
    avgMs: 894,
    last: '24m',
    successRate: 100,
    sla: 99.9,
  },
  {
    id: 'dunn',
    name: 'Dunning Agent',
    glyph: 'envelope',
    color: '#5facbd',
    role: 'Drafts overdue reminders when match fails',
    autonomy: 'propose',
    calls: 28,
    avgMs: 142,
    last: '1h',
    successRate: 96.4,
    sla: 98,
  },
];

export interface DemoTool {
  name: string;
  cat: 'read' | 'write' | 'compute' | 'display';
  desc: string;
  uses: number;
  p99: string;
  errors: number;
  enabled: boolean;
}

const DEMO_TOOL_TEMPLATE: DemoTool[] = [
  { name: 'search_invoices', cat: 'read', desc: 'Query the invoice store by counterparty, status or amount.', uses: 1842, p99: '142ms', errors: 0, enabled: true },
  { name: 'list_bank_transactions', cat: 'read', desc: 'Read bank txns inside a date window from connected rails.', uses: 1318, p99: '318ms', errors: 4, enabled: true },
  { name: 'match_invoice_to_txn', cat: 'compute', desc: 'Score a candidate match (0–1) using ledger + memo signals.', uses: 1284, p99: '211ms', errors: 12, enabled: true },
  { name: 'draft_journal_entry', cat: 'write', desc: 'Compose a balanced debit/credit pair (proposal only).', uses: 892, p99: '88ms', errors: 0, enabled: true },
  { name: 'apply_journal_entry', cat: 'write', desc: 'Post a drafted entry to the ledger after approval.', uses: 416, p99: '142ms', errors: 2, enabled: true },
  { name: 'send_dunning_email', cat: 'write', desc: 'Compose and dispatch an overdue reminder.', uses: 28, p99: '512ms', errors: 0, enabled: false },
  { name: 'display_proposal_card', cat: 'display', desc: 'Render an Apply / Review / Dismiss tool card in chat.', uses: 892, p99: '12ms', errors: 0, enabled: true },
  { name: 'confirm_action', cat: 'display', desc: 'Halt and ask the human for explicit approval.', uses: 142, p99: '8ms', errors: 0, enabled: true },
];

/**
 * Tool usage demo. If the agent has real tool ids in toolsetIdsJson, we
 * map them onto the demo template's KIND/USES/P99/ERRORS shape so they
 * look realistic. Otherwise we return the template's full set.
 */
export function getDemoTools(realToolNames: string[]): DemoTool[] {
  if (realToolNames.length === 0) return DEMO_TOOL_TEMPLATE;

  const inferKind = (name: string): DemoTool['cat'] => {
    const lower = name.toLowerCase();
    if (lower.includes('apply') || lower.includes('post') || lower.includes('send') || lower.includes('create') || lower.includes('issue') || lower.includes('cancel') || lower.includes('mutate') || lower.includes('write') || lower.includes('draft')) return 'write';
    if (lower.includes('display') || lower.includes('confirm') || lower.includes('show') || lower.includes('render')) return 'display';
    if (lower.includes('match') || lower.includes('score') || lower.includes('compute')) return 'compute';
    return 'read';
  };

  return realToolNames.map((name, i) => {
    const template = DEMO_TOOL_TEMPLATE[i % DEMO_TOOL_TEMPLATE.length];
    return {
      name,
      cat: inferKind(name),
      desc: humanizeToolDesc(name),
      uses: template.uses,
      p99: template.p99,
      errors: template.errors,
      enabled: true,
    };
  });
}

function humanizeToolDesc(name: string): string {
  const verb = name.split(/[_-]/)[0];
  const subject = name.replace(/^[^_-]+[_-]?/, '').replace(/[_-]/g, ' ');
  const verbWord =
    {
      get: 'Fetch',
      list: 'List',
      query: 'Query',
      search: 'Search',
      create: 'Create',
      issue: 'Issue',
      cancel: 'Cancel',
      send: 'Send',
      draft: 'Draft',
      apply: 'Apply',
      match: 'Match',
      score: 'Score',
      compute: 'Compute',
      display: 'Display',
      confirm: 'Confirm',
    }[verb] ?? verb;
  return `${verbWord} ${subject || 'records'} via the platform API.`.trim();
}

export interface SparklineKpi {
  label: string;
  value: string;
  delta: string;
  positive: boolean;
  spark: number[];
}

export const DEMO_KPIS: SparklineKpi[] = [
  { label: 'Runs (24h)', value: '318', delta: '+12%', positive: true, spark: [40, 42, 38, 46, 52, 58, 55, 60, 68, 72, 78, 82] },
  { label: 'Avg confidence', value: '94.0%', delta: '+0.4%', positive: true, spark: [88, 89, 90, 89, 92, 93, 92, 94, 93, 94, 94, 94] },
  { label: 'Tool calls', value: '4.8k', delta: '+18%', positive: true, spark: [60, 68, 72, 80, 88, 92, 98, 108, 118, 124, 132, 142] },
  { label: 'Avg latency', value: '892ms', delta: '-42ms', positive: true, spark: [120, 118, 114, 112, 108, 106, 102, 100, 98, 95, 92, 89] },
];

export interface DemoSkill {
  name: string;
  desc: string;
  version: string;
  source: 'org' | 'community' | 'private';
  last24h: number;
  status: 'active' | 'beta';
}

export const DEMO_SKILLS: DemoSkill[] = [
  { name: 'invoice-reconciliation', desc: 'Match incoming bank txns to open invoices and draft journal entries when confidence is high.', version: '1.4.2', source: 'org', last24h: 412, status: 'active' },
  { name: 'bank-statement-intake', desc: 'Parse uploaded CSV/OFX bank statements and post lines as draft staging-ledger transactions.', version: '2.0.1', source: 'org', last24h: 184, status: 'active' },
  { name: 'ar-aging-summary', desc: 'Produce an aging summary across the AR ledger with sub-totals by tier and a chase-list.', version: '1.0.0', source: 'community', last24h: 88, status: 'active' },
  { name: 'dunning-cadence', desc: 'Choose a dunning template + channel for an overdue invoice using tier and prior-contact data.', version: '1.2.0', source: 'org', last24h: 42, status: 'active' },
  { name: 'currency-rounding-fix', desc: 'Detect and reverse off-by-cent rounding errors when invoice + settlement currencies differ.', version: '0.3.0', source: 'private', last24h: 6, status: 'beta' },
];

export interface DemoMcp {
  name: string;
  url: string;
  status: 'connected' | 'connecting' | 'error';
  tools: number;
  latency: string;
  auth: string;
  native?: boolean;
  err?: string;
}

export const DEMO_MCP: DemoMcp[] = [
  { name: 'aonik-ledger', url: 'mcp://internal/aonik-ledger', status: 'connected', tools: 12, latency: '14ms', auth: 'mTLS', native: true },
  { name: 'open-banking-uk', url: 'mcp://partner/open-banking-uk', status: 'connected', tools: 8, latency: '188ms', auth: 'OAuth2' },
  { name: 'companies-house', url: 'mcp://partner/companies-house', status: 'connected', tools: 4, latency: '412ms', auth: 'API key' },
  { name: 'fx-quotes', url: 'mcp://partner/fx-quotes-v2', status: 'connecting', tools: 6, latency: '—', auth: 'OAuth2' },
  { name: 'sanctions-screen', url: 'mcp://partner/ofac-sanctions', status: 'error', tools: 3, latency: '—', auth: 'mTLS', err: 'TLS handshake failed' },
];

export interface DemoRun {
  op: string;
  status: 'ok' | 'held' | 'err';
  dur: string;
  t: string;
  txn: string;
}

export const DEMO_RUNS: DemoRun[] = [
  { op: 'match_and_apply', status: 'ok', dur: '3.14s', t: 'now', txn: 'INV-2041' },
  { op: 'apply_invoice', status: 'held', dur: '0.84s', t: '11m', txn: 'INV-2038' },
  { op: 'match_and_apply', status: 'ok', dur: '2.94s', t: '24m', txn: 'INV-2037' },
  { op: 'summarize_ar', status: 'ok', dur: '1.94s', t: '1h', txn: '—' },
  { op: 'dunning_send', status: 'ok', dur: '0.42s', t: '2h', txn: 'INV-2014' },
  { op: 'reconcile_fx', status: 'err', dur: '4.21s', t: '3h', txn: 'INV-2009' },
];

export interface DemoPolicy {
  iconKey: 'shield' | 'lock' | 'sparkles';
  title: string;
  description: string;
  enforced: boolean;
  soft?: boolean;
}

export const DEMO_POLICIES: DemoPolicy[] = [
  { iconKey: 'shield', title: 'Dual-control payouts', description: 'Two approvers for any outbound payout', enforced: true },
  { iconKey: 'lock', title: 'Amount ceiling', description: 'Always require approval > £50,000', enforced: true },
  { iconKey: 'lock', title: 'PII redaction', description: 'Customer PII stripped from all prompts', enforced: true },
  { iconKey: 'sparkles', title: 'Auto-apply', description: 'Confidence ≥ 0.95 · audit on apply', enforced: true, soft: true },
];

/** Compute an SVG polyline points string from a numeric series. */
export function sparklinePoints(values: number[], width = 70, height = 22): string {
  if (values.length === 0) return '';
  const max = Math.max(...values);
  const min = Math.min(...values);
  const range = max - min || 1;
  return values
    .map((v, i) => `${(i / (values.length - 1)) * width},${height - ((v - min) / range) * height}`)
    .join(' ');
}
