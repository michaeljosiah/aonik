// Shared helpers for the agents pages — derive presentation fields the
// production AgentConfigurationResponse doesn't yet carry (colour, glyph,
// tagline, autoApply) from the data we do have. Used by AgentCard,
// AgentPortrait, AgentDetailHero, etc.

import type { AgentConfigurationResponse } from '@/types/ai';

export type AgentGlyph =
  | 'orbital'
  | 'columns'
  | 'docstack'
  | 'wave'
  | 'shield'
  | 'rings'
  | 'envelope'
  | 'pulse';

export const AGENT_PALETTE = [
  '#055a60', // brand teal
  '#eb5c37', // coral
  '#3ab795', // mint
  '#7b76b6', // violet
  '#0097a9', // patrol
  '#5facbd', // sky
  '#d4a843', // amber
] as const;

export const AGENT_GLYPHS: AgentGlyph[] = [
  'orbital',
  'columns',
  'docstack',
  'wave',
  'shield',
  'rings',
  'envelope',
  'pulse',
];

function hash(value: string): number {
  let h = 0;
  for (let i = 0; i < value.length; i += 1) {
    h = (h * 31 + value.charCodeAt(i)) >>> 0;
  }
  return h;
}

/** Deterministic colour from the agent name (hashed into AGENT_PALETTE). */
export function deriveAgentColor(name: string): string {
  return AGENT_PALETTE[hash(name) % AGENT_PALETTE.length];
}

/**
 * Deterministic glyph from the agent name. A few well-known names get
 * keyword-mapped glyphs so the demo matches reader intuition; everything
 * else hashes into the 8-glyph rotation.
 */
export function deriveAgentGlyph(name: string): AgentGlyph {
  const lower = name.toLowerCase();
  if (lower.includes('orchestrator')) return 'orbital';
  if (lower.includes('ledger')) return 'columns';
  if (lower.includes('billing') || lower.includes('invoice')) return 'docstack';
  if (lower.includes('fx') || lower.includes('rate')) return 'wave';
  if (lower.includes('compliance') || lower.includes('kyc')) return 'shield';
  if (lower.includes('close') || lower.includes('treasury')) return 'rings';
  if (lower.includes('dunning') || lower.includes('email')) return 'envelope';
  if (lower.includes('insight') || lower.includes('analytics')) return 'pulse';
  return AGENT_GLYPHS[hash(name) % AGENT_GLYPHS.length];
}

/**
 * Short tagline — the template uses 60-char one-liners. We synthesise from
 * the description's first sentence/clause, capping at 60 chars.
 */
export function deriveAgentTagline(description: string | null | undefined): string {
  const text = (description ?? '').trim();
  if (!text) return '';
  const period = text.indexOf('.');
  const first = period > 0 ? text.slice(0, period) : text;
  if (first.length <= 60) return first;
  return first.slice(0, 57).trimEnd() + '…';
}

/**
 * Derive the auto-apply flag from `riskTier`. Low-risk agents apply
 * automatically; everything else proposes only. This mirrors the rule the
 * platform follows in production (risk tier ↦ AI autonomy).
 */
export function deriveAutoApply(riskTier: string | null | undefined): boolean {
  return (riskTier ?? '').toLowerCase() === 'low';
}

/**
 * Map agentType (0/1) to the template's System/Domain vocabulary.
 *  · 1 = Orchestrator → "System"
 *  · 0 = SubAgent     → "Domain"
 */
export function deriveKindLabel(agentType: number): 'System' | 'Domain' {
  return agentType === 1 ? 'System' : 'Domain';
}

/** The orchestrator gets a coral pin in the card view, like the template. */
export function isPinnedAgent(agent: AgentConfigurationResponse): boolean {
  return agent.agentType === 1;
}

/** Number of tools enabled — parsed from the JSON-encoded toolset id list. */
export function countTools(toolsetIdsJson: string | null | undefined): number {
  if (!toolsetIdsJson) return 0;
  try {
    const parsed = JSON.parse(toolsetIdsJson);
    return Array.isArray(parsed) ? parsed.length : 0;
  } catch {
    return 0;
  }
}

/** Number of policies — keys on the permissions profile JSON object. */
export function countPolicies(permissionsProfileJson: string | null | undefined): number {
  if (!permissionsProfileJson) return 0;
  try {
    const parsed = JSON.parse(permissionsProfileJson);
    if (Array.isArray(parsed)) return parsed.length;
    if (parsed && typeof parsed === 'object') return Object.keys(parsed).length;
    return 0;
  } catch {
    return 0;
  }
}

/** Parse the toolset JSON into a string list (tool names / ids). */
export function parseToolNames(toolsetIdsJson: string | null | undefined): string[] {
  if (!toolsetIdsJson) return [];
  try {
    const parsed = JSON.parse(toolsetIdsJson);
    if (Array.isArray(parsed)) return parsed.filter((v): v is string => typeof v === 'string');
    return [];
  } catch {
    return [];
  }
}

/**
 * Display state for an agent. Without a live runs-in-progress feed we can
 * only distinguish active vs paused. Map: isActive=true → "idle" (caller
 * may upgrade to "running" if a recent run exists); isActive=false → "paused".
 */
export type AgentDisplayState = 'running' | 'idle' | 'paused';

export function deriveAgentState(
  isActive: boolean,
  hasRecentRun = false,
): AgentDisplayState {
  if (!isActive) return 'paused';
  return hasRecentRun ? 'running' : 'idle';
}

/** Format a relative timestamp (e.g. "now", "2m", "3h", "1d"). */
export function formatRelativeTime(iso: string | null | undefined): string {
  if (!iso) return '—';
  const ms = Date.now() - new Date(iso).getTime();
  if (Number.isNaN(ms) || ms < 0) return '—';
  const sec = Math.floor(ms / 1000);
  if (sec < 60) return 'now';
  const min = Math.floor(sec / 60);
  if (min < 60) return `${min}m ago`;
  const hr = Math.floor(min / 60);
  if (hr < 24) return `${hr}h ago`;
  const day = Math.floor(hr / 24);
  if (day < 7) return `${day}d ago`;
  const wk = Math.floor(day / 7);
  return `${wk}w ago`;
}
