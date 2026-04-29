// Agent detail page — visual port of `ScreenAgentDetail` from
// templates/aonik-admin-starterkit/screens/agent-detail.jsx.
//
// The page renders the real agent identity (name, description, model,
// tier) at the top, then uses the template's demo content for sections
// where the production backend doesn't yet have aggregated data:
//   • KPI sparklines + deltas
//   • Connected sub-agents (relationship metadata not yet stored)
//   • Tool usage stats (24h uses, p99 latency, errors)
//   • Skills (SKILL.md system not yet shipped)
//   • MCP servers (separate from /ai/providers)
//   • Network call graph
//   • Recent runs op/ref/duration formatting
//
// Demo data lives in `agents/demoData.ts` and the page passes the agent's
// real data through where it can (e.g. tool names from toolsetIdsJson are
// mapped onto the demo template's KIND/USES/P99 shape).

import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  AlertCircle,
  AlertTriangle,
  Calendar,
  ChevronRight,
  Edit3,
  FileText,
  Filter,
  Lock,
  MoreHorizontal,
  Play,
  Plus,
  RefreshCw,
  Search,
  Server,
  ShieldCheck,
  Sparkles,
  Folder,
  Upload,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Pill } from '@/components/layout/aonik';
import { agentConfigService, agentRunService } from '@/services/aiService';
import type { AgentConfigurationResponse, AgentRunSummary } from '@/types/ai';
import type { PagedResult } from '@/types';
import { cn } from '@/lib/utils';

import { AgentEditPanel } from './agents/AgentEditPanel';
import { AgentDetailHero } from './agents/AgentDetailHero';
import { AgentPortrait } from './agents/AgentPortrait';
import {
  countTools,
  deriveAgentColor,
  deriveAutoApply,
  formatRelativeTime,
  parseToolNames,
} from './agents/agentMeta';
import {
  DEMO_KPIS,
  DEMO_MCP,
  DEMO_POLICIES,
  DEMO_RUNS,
  DEMO_SKILLS,
  DEMO_SUB_AGENTS,
  type DemoSubAgent,
  type DemoTool,
  getDemoTools,
  sparklinePoints,
} from './agents/demoData';

type Tab = 'Overview' | 'Sub-agents' | 'Tools' | 'Skills' | 'MCP Servers' | 'Activity' | 'Settings';
const TABS: Tab[] = ['Overview', 'Sub-agents', 'Tools', 'Skills', 'MCP Servers', 'Activity', 'Settings'];

const DEFAULT_PAGE_SIZE = 20;

const CAT_TONE: Record<DemoTool['cat'], { bg: string; fg: string }> = {
  read: { bg: 'var(--color-brand-primary-10)', fg: 'var(--color-brand-primary)' },
  write: { bg: '#eb5c371a', fg: '#eb5c37' },
  compute: { bg: '#3ab7951a', fg: '#3ab795' },
  display: { bg: '#7b76b61a', fg: '#7b76b6' },
};

const AUTONOMY_TONE: Record<DemoSubAgent['autonomy'], { bg: string; fg: string; t: string }> = {
  auto: { bg: '#1f7a5e1a', fg: '#1f7a5e', t: 'Auto-apply' },
  propose: { bg: '#b4741e1a', fg: '#b4741e', t: 'Propose' },
  block: { bg: '#7b76b61a', fg: '#7b76b6', t: 'Required' },
};

export function AgentDetailPage() {
  const { agentName: rawAgentName } = useParams();
  const agentName = rawAgentName ? decodeURIComponent(rawAgentName) : '';
  const navigate = useNavigate();

  const [agent, setAgent] = useState<AgentConfigurationResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [tab, setTab] = useState<Tab>('Overview');
  const [editing, setEditing] = useState(false);

  const [runs, setRuns] = useState<PagedResult<AgentRunSummary> | null>(null);
  const [runsLoading, setRunsLoading] = useState(false);

  const loadAgent = useCallback(async () => {
    if (!agentName) return;
    setLoading(true);
    setError(null);
    try {
      const result = await agentConfigService.get(agentName);
      setAgent(result);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || `Failed to load agent "${agentName}".`);
    } finally {
      setLoading(false);
    }
  }, [agentName]);

  useEffect(() => {
    void loadAgent();
  }, [loadAgent]);

  useEffect(() => {
    if (!agent) return;
    let cancelled = false;
    setRunsLoading(true);
    void (async () => {
      try {
        const result = await agentRunService.list(agent.id, 1, DEFAULT_PAGE_SIZE);
        if (!cancelled) setRuns(result);
      } catch {
        if (!cancelled) setRuns(null);
      } finally {
        if (!cancelled) setRunsLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [agent]);

  if (loading) {
    return (
      <div className="flex h-full items-center justify-center text-[13px] text-[var(--color-text-secondary)]">
        Loading agent…
      </div>
    );
  }

  if (error || !agent) {
    return (
      <div className="flex h-full flex-col items-center justify-center gap-4 p-6">
        <div className="flex items-center gap-2 text-[13px] text-[var(--color-error)]">
          <AlertCircle className="h-4 w-4" />
          {error ?? 'Agent not found.'}
        </div>
        <Button variant="outline" onClick={() => navigate('/ai/agents')}>
          Back to agents
        </Button>
      </div>
    );
  }

  const color = deriveAgentColor(agent.name);
  const totalRuns = runs?.totalCount ?? 0;
  const lastRunAt = runs?.items[0]?.updatedAt ?? null;
  const tools = getDemoTools(parseToolNames(agent.toolsetIdsJson));

  return (
    <div className="relative h-full overflow-hidden">
      <div className="h-full overflow-auto">
        <AgentDetailHero
          agent={agent}
          hasRecentRun={agent.isActive}
          lastRunAt={lastRunAt}
          onEdit={() => setEditing(true)}
        />

        {/* Sticky tab nav */}
        <div className="sticky top-0 z-10 flex items-center gap-1 border-b border-[var(--color-border-light)] bg-[var(--color-surface)] px-8">
          {TABS.map((t) => {
            const active = t === tab;
            return (
              <button
                key={t}
                type="button"
                onClick={() => setTab(t)}
                className={cn(
                  '-mb-px border-b-2 px-3.5 py-3.5 text-[13px] transition-colors',
                  active
                    ? 'font-semibold text-[var(--color-text-primary)]'
                    : 'font-medium text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]',
                )}
                style={{ borderColor: active ? color : 'transparent' }}
              >
                {t}
              </button>
            );
          })}
          <div className="flex-1" />
          <span className="font-[family-name:var(--font-mono)] text-[10.5px] text-[var(--color-text-tertiary)]">
            agent_id: {agent.id.slice(0, 12)}
          </span>
        </div>

        {/* Tab body */}
        <div className="mx-auto max-w-[1600px] px-8 py-6 pb-10">
          {tab === 'Overview' && (
            <OverviewTab
              agent={agent}
              color={color}
              tools={tools}
              runs={runs?.items ?? []}
              totalRuns={totalRuns}
              runsLoading={runsLoading}
            />
          )}
          {tab === 'Sub-agents' && <SubAgentsTab />}
          {tab === 'Tools' && <ToolsTab agent={agent} tools={tools} color={color} />}
          {tab === 'Skills' && <SkillsTab color={color} />}
          {tab === 'MCP Servers' && <McpTab />}
          {tab === 'Activity' && (
            <ActivityTab agent={agent} runs={runs} runsLoading={runsLoading} />
          )}
          {tab === 'Settings' && (
            <SettingsTab agent={agent} onEdit={() => setEditing(true)} />
          )}
        </div>
      </div>

      {editing && (
        <AgentEditPanel
          agent={agent}
          onClose={() => setEditing(false)}
          onSaved={(updated) => setAgent(updated)}
          onDeleted={() => navigate('/ai/agents')}
        />
      )}
    </div>
  );
}

// ── Overview tab ───────────────────────────────────────────────────

function OverviewTab({
  agent,
  color,
  tools,
  runs,
  totalRuns,
  runsLoading,
}: {
  agent: AgentConfigurationResponse;
  color: string;
  tools: DemoTool[];
  runs: AgentRunSummary[];
  totalRuns: number;
  runsLoading: boolean;
}) {
  return (
    <div className="grid grid-cols-1 gap-6 xl:grid-cols-[1fr_360px]">
      <div className="flex min-w-0 flex-col gap-5">
        <KpiStrip color={color} />
        <SubAgentsSection />
        <ToolsSection tools={tools} />
        <SkillsSection color={color} />
        <McpSection />
      </div>
      <div className="flex flex-col gap-5">
        <ConnectionMapCard color={color} agentName={agent.name} />
        <RecentRunsCard runs={runs} totalRuns={totalRuns} loading={runsLoading} />
        <PolicyCard agent={agent} />
      </div>
    </div>
  );
}

function KpiStrip({ color }: { color: string }) {
  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
      {DEMO_KPIS.map((k) => (
        <div
          key={k.label}
          className="rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-3.5"
        >
          <div className="text-[10.5px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
            {k.label}
          </div>
          <div className="mt-1.5 flex items-end gap-2">
            <div className="font-[family-name:var(--font-mono)] text-[22px] font-semibold leading-none tabular-nums text-[var(--color-text-primary)]">
              {k.value}
            </div>
            <div className="flex-1" />
            <svg width={70} height={22} aria-hidden>
              <polyline
                fill="none"
                stroke={color}
                strokeWidth={1.5}
                points={sparklinePoints(k.spark, 70, 22)}
              />
            </svg>
          </div>
          <div
            className="mt-1 font-[family-name:var(--font-mono)] text-[11px]"
            style={{ color: k.positive ? 'var(--color-success)' : '#c44536' }}
          >
            {k.delta}
          </div>
        </div>
      ))}
    </div>
  );
}

function SubAgentsSection() {
  return (
    <Section
      title="Connected sub-agents"
      subtitle="Other agents this one delegates work to. Calls require the destination's policies to allow the operation."
      count={DEMO_SUB_AGENTS.length}
      action={
        <Button variant="outline" size="sm">
          <Plus className="h-3 w-3" />
          Connect agent
        </Button>
      }
    >
      <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-2">
        {DEMO_SUB_AGENTS.map((s) => {
          const tone = AUTONOMY_TONE[s.autonomy];
          return (
            <div
              key={s.id}
              className="flex cursor-pointer gap-3 rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)] p-3 transition-[border-color,transform] duration-150 hover:-translate-y-px hover:border-[var(--color-text-secondary)]"
            >
              <AgentPortrait name={s.name} color={s.color} glyph={s.glyph} size={42} ring={false} />
              <div className="min-w-0 flex-1">
                <div className="mb-0.5 flex items-center gap-2">
                  <span className="text-[13px] font-semibold text-[var(--color-text-primary)]">
                    {s.name}
                  </span>
                  <span
                    className="rounded px-1.5 py-[1px] text-[9.5px] font-semibold uppercase tracking-[0.04em]"
                    style={{ background: tone.bg, color: tone.fg }}
                  >
                    {tone.t}
                  </span>
                </div>
                <div className="mb-1.5 text-[11.5px] leading-relaxed text-[var(--color-text-secondary)]">
                  {s.role}
                </div>
                <div className="flex gap-3.5 font-[family-name:var(--font-mono)] text-[10.5px] text-[var(--color-text-tertiary)]">
                  <span>{s.calls} calls / 24h</span>
                  <span>~{s.avgMs}ms</span>
                </div>
              </div>
              <ChevronRight className="h-3 w-3 self-center text-[var(--color-text-tertiary)]" />
            </div>
          );
        })}
      </div>
    </Section>
  );
}

function ToolsSection({ tools }: { tools: DemoTool[] }) {
  return (
    <Section
      title="Tools"
      subtitle="Functions the agent can invoke. Read tools fetch data; write tools mutate state and require policy clearance; display tools render UI in chat."
      count={tools.length}
      action={
        <>
          <Button variant="ghost" size="sm">
            View schema
          </Button>
          <Button variant="outline" size="sm">
            <Plus className="h-3 w-3" />
            Add tool
          </Button>
        </>
      }
    >
      <div className="overflow-hidden rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)]">
        <ToolsHeaderRow />
        {tools.map((t, i) => (
          <ToolRow key={t.name} tool={t} last={i === tools.length - 1} />
        ))}
      </div>
    </Section>
  );
}

function ToolsHeaderRow() {
  return (
    <div
      className="grid items-center gap-3.5 border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-3.5 py-2"
      style={{ gridTemplateColumns: '90px 1fr 90px 80px 24px' }}
    >
      <div className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
        Kind
      </div>
      <div className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
        Tool
      </div>
      <div className="text-right text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
        Uses · 24h
      </div>
      <div className="text-right text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
        p99
      </div>
      <div />
    </div>
  );
}

function ToolRow({ tool, last }: { tool: DemoTool; last: boolean }) {
  const tone = CAT_TONE[tool.cat];
  return (
    <div
      className={cn('grid items-center gap-3.5 px-3.5 py-3', !last && 'border-b border-[var(--color-border-light)]')}
      style={{ gridTemplateColumns: '90px 1fr 90px 80px 24px' }}
    >
      <span
        className="justify-self-start rounded px-1.5 py-[2px] text-center font-[family-name:var(--font-mono)] text-[9.5px] font-semibold uppercase tracking-[0.06em]"
        style={{ background: tone.bg, color: tone.fg }}
      >
        {tool.cat}
      </span>
      <div className="min-w-0">
        <div className="font-[family-name:var(--font-mono)] text-[12.5px] font-semibold text-[var(--color-text-primary)]">
          {tool.name}
        </div>
        <div className="mt-0.5 line-clamp-2 text-[11.5px] text-[var(--color-text-secondary)]">
          {tool.desc}
        </div>
      </div>
      <div className="text-right font-[family-name:var(--font-mono)] text-[12px] tabular-nums text-[var(--color-text-secondary)]">
        {tool.uses.toLocaleString()}
      </div>
      <div
        className="text-right font-[family-name:var(--font-mono)] text-[12px]"
        style={{ color: tool.errors > 0 ? '#c44536' : 'var(--color-text-secondary)' }}
      >
        {tool.errors > 0 ? `${tool.errors} err` : tool.p99}
      </div>
      <MoreHorizontal className="h-3 w-3 text-[var(--color-text-tertiary)]" />
    </div>
  );
}

function SkillsSection({ color }: { color: string }) {
  const sourceTone = { org: '#055a60', community: '#7b76b6', private: '#b4741e' } as const;
  return (
    <Section
      title="Skills"
      subtitle="Folders with a SKILL.md. The agent reads name + description on every turn and only loads the full instructions when a task matches."
      count={DEMO_SKILLS.length}
      action={
        <>
          <Button variant="ghost" size="sm">
            Browse registry
          </Button>
          <Button variant="outline" size="sm">
            <Plus className="h-3 w-3" />
            Install skill
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-2">
        {DEMO_SKILLS.map((s) => {
          const tone = sourceTone[s.source];
          return (
            <div
              key={s.name}
              className="grid cursor-pointer items-start gap-3 rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)] p-3 transition-[border-color,transform] duration-150 hover:-translate-y-px hover:border-[var(--color-text-secondary)]"
              style={{ gridTemplateColumns: '30px 1fr auto' }}
            >
              <div
                className="grid h-7 w-7 place-items-center rounded-[7px]"
                style={{ background: `${color}14`, color }}
              >
                <Folder className="h-3.5 w-3.5" />
              </div>
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-[family-name:var(--font-mono)] text-[12.5px] font-semibold text-[var(--color-text-primary)]">
                    {s.name}
                  </span>
                  <span className="font-[family-name:var(--font-mono)] text-[10.5px] text-[var(--color-text-tertiary)]">
                    v{s.version}
                  </span>
                  {s.status === 'beta' && (
                    <span
                      className="rounded px-1.5 py-[1px] text-[9.5px] font-semibold tracking-[0.04em]"
                      style={{ background: '#b4741e1a', color: '#b4741e' }}
                    >
                      BETA
                    </span>
                  )}
                  <span
                    className="rounded px-1.5 py-[1px] text-[9.5px] font-semibold uppercase tracking-[0.04em]"
                    style={{ background: `${tone}14`, color: tone }}
                  >
                    {s.source}
                  </span>
                </div>
                <div className="mt-1 text-[11.5px] leading-relaxed text-[var(--color-text-secondary)]">
                  {s.desc}
                </div>
              </div>
              <div className="text-right whitespace-nowrap">
                <div className="font-[family-name:var(--font-mono)] text-[12px] text-[var(--color-text-primary)]">
                  {s.last24h}
                </div>
                <div className="mt-px text-[10px] text-[var(--color-text-tertiary)]">
                  activations · 24h
                </div>
              </div>
            </div>
          );
        })}
      </div>
    </Section>
  );
}

function McpSection() {
  const stTone = {
    connected: { c: 'var(--color-success)', t: 'Connected' },
    connecting: { c: '#b4741e', t: 'Connecting' },
    error: { c: '#c44536', t: 'Error' },
  } as const;
  return (
    <Section
      title="MCP Servers"
      subtitle="External Model Context Protocol servers this agent connects to. Each server exposes a typed tool surface guarded by tenant-level auth."
      count={DEMO_MCP.length}
      action={
        <>
          <Button variant="ghost" size="sm">
            Browse marketplace
          </Button>
          <Button variant="outline" size="sm">
            <Plus className="h-3 w-3" />
            Connect server
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-2">
        {DEMO_MCP.map((s) => {
          const st = stTone[s.status];
          return (
            <div
              key={s.name}
              className="grid items-center gap-3.5 rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)] px-3.5 py-3"
              style={{ gridTemplateColumns: '36px 1fr 110px 90px 90px 24px' }}
            >
              <div className="relative grid h-8 w-8 place-items-center rounded-[7px] bg-[var(--color-surface-inset)]">
                <Server className="h-3.5 w-3.5 text-[var(--color-text-secondary)]" />
                <span
                  className="absolute -bottom-0.5 -right-0.5 h-2.5 w-2.5 rounded-full border-2"
                  style={{
                    background: st.c,
                    borderColor: 'var(--color-surface)',
                  }}
                />
              </div>
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <span className="text-[13px] font-semibold text-[var(--color-text-primary)]">
                    {s.name}
                  </span>
                  {s.native && (
                    <span
                      className="rounded px-1.5 py-[1px] text-[9.5px] font-semibold tracking-[0.04em]"
                      style={{
                        background: 'var(--color-brand-primary-10)',
                        color: 'var(--color-brand-primary)',
                      }}
                    >
                      NATIVE
                    </span>
                  )}
                </div>
                <div className="mt-px truncate font-[family-name:var(--font-mono)] text-[10.5px] text-[var(--color-text-tertiary)]">
                  {s.url}
                  {s.err && <span className="ml-2 text-[#c44536]">· {s.err}</span>}
                </div>
              </div>
              <span
                className="inline-flex items-center gap-1.5 text-[11px] font-semibold"
                style={{ color: st.c }}
              >
                <span className="h-1.5 w-1.5 rounded-full" style={{ background: st.c }} />
                {st.t}
              </span>
              <div className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-secondary)]">
                <span className="text-[var(--color-text-tertiary)]">tools </span>
                {s.tools}
              </div>
              <div className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-secondary)]">
                {s.latency}
              </div>
              <MoreHorizontal className="h-3 w-3 text-[var(--color-text-tertiary)]" />
            </div>
          );
        })}
      </div>
    </Section>
  );
}

// ── Right rail ─────────────────────────────────────────────────────

function ConnectionMapCard({ color, agentName }: { color: string; agentName: string }) {
  const cx = 160;
  const cy = 130;
  const r = 72;
  const subs = DEMO_SUB_AGENTS.map((s, i) => ({
    ...s,
    angle: -90 + i * 70 - 30,
  }));

  return (
    <CardShell title="Network" eyebrow="Live · last 60s">
      <div className="relative" style={{ height: 240 }}>
        <svg width="100%" height="240" viewBox="0 0 320 240" className="block">
          <defs>
            <radialGradient id="cm-glow" cx="0.5" cy="0.5" r="0.5">
              <stop offset="0%" stopColor={color} stopOpacity={0.35} />
              <stop offset="100%" stopColor={color} stopOpacity={0} />
            </radialGradient>
          </defs>
          <circle cx={cx} cy={cy} r={70} fill="url(#cm-glow)" />
          <circle cx={cx} cy={cy} r={58} fill="none" stroke="var(--color-border-light)" strokeDasharray="2 4" />
          <circle cx={cx} cy={cy} r={84} fill="none" stroke="var(--color-border-light)" strokeDasharray="2 4" opacity={0.6} />

          {/* Links + animated dots */}
          {subs.map((s, i) => {
            const x = cx + Math.cos((s.angle * Math.PI) / 180) * r;
            const y = cy + Math.sin((s.angle * Math.PI) / 180) * r;
            return (
              <g key={`l-${s.id}`}>
                <line x1={cx} y1={cy} x2={x} y2={y} stroke={s.color} strokeOpacity={0.3} strokeWidth={1} />
                <circle r={3} fill={s.color}>
                  <animateMotion
                    dur={`${3 + i * 0.6}s`}
                    repeatCount="indefinite"
                    path={`M${cx} ${cy} L${x} ${y}`}
                  />
                </circle>
              </g>
            );
          })}

          {/* Sub-agent nodes */}
          {subs.map((s) => {
            const x = cx + Math.cos((s.angle * Math.PI) / 180) * r;
            const y = cy + Math.sin((s.angle * Math.PI) / 180) * r;
            return (
              <g key={`n-${s.id}`}>
                <circle cx={x} cy={y} r={14} fill={s.color} fillOpacity={0.15} stroke={s.color} strokeWidth={1} />
                <circle cx={x} cy={y} r={6} fill={s.color} />
                <text
                  x={x}
                  y={y + 26}
                  textAnchor="middle"
                  fontSize={9.5}
                  fontFamily="var(--font-mono)"
                  fill="var(--color-text-secondary)"
                >
                  {s.name.split(' ')[0]}
                </text>
              </g>
            );
          })}

          {/* Centre node — agent itself */}
          <circle cx={cx} cy={cy} r={22} fill={color} />
          <circle cx={cx} cy={cy} r={22} fill="none" stroke={color} strokeOpacity={0.3} strokeWidth={2}>
            <animate attributeName="r" from="22" to="34" dur="2s" repeatCount="indefinite" />
            <animate attributeName="stroke-opacity" from="0.4" to="0" dur="2s" repeatCount="indefinite" />
          </circle>
          <text
            x={cx}
            y={cy + 4}
            textAnchor="middle"
            fontSize={11}
            fontFamily="var(--font-brand)"
            fontWeight={600}
            fill="#fff"
          >
            {agentName.split(/[\s_-]+/)[0]}
          </text>
        </svg>
      </div>
      <div className="flex items-center justify-between border-t border-[var(--color-border-light)] px-3 py-2.5">
        <span className="text-[11px] text-[var(--color-text-secondary)]">
          {DEMO_SUB_AGENTS.length} sub-agents · 220 calls / hr
        </span>
        <Button variant="ghost" size="sm">
          Open graph →
        </Button>
      </div>
    </CardShell>
  );
}

function RecentRunsCard({
  runs,
  totalRuns,
  loading,
}: {
  runs: AgentRunSummary[];
  totalRuns: number;
  loading: boolean;
}) {
  const useReal = runs.length > 0;
  const display = useReal
    ? runs.slice(0, 6).map((r) => ({
        op: r.goal || r.id.slice(0, 8),
        status: mapRunStatus(r.status),
        dur: `${r.stepCount} steps`,
        t: formatRelativeTime(r.updatedAt ?? r.createdAt),
        txn: '—',
      }))
    : DEMO_RUNS;
  const eyebrow = useReal ? `${runs.length} of ${totalRuns}` : `${DEMO_RUNS.length} of 318`;

  const tone: Record<'ok' | 'held' | 'err', string> = {
    ok: 'var(--color-success)',
    held: '#b4741e',
    err: '#c44536',
  };

  return (
    <CardShell
      title="Recent runs"
      eyebrow={eyebrow}
      action={
        <Button variant="ghost" size="sm">
          View all →
        </Button>
      }
    >
      <div className="flex flex-col">
        {loading && useReal === false && (
          <div className="px-4 py-5 text-center text-[12px] text-[var(--color-text-tertiary)]">
            Loading…
          </div>
        )}
        {display.map((r, i) => (
          <div
            key={i}
            className={cn(
              'grid items-center gap-2 px-3 py-2.5',
              i === 0 && 'border-t border-[var(--color-border-light)]',
              'border-b border-[var(--color-border-light)]',
            )}
            style={{ gridTemplateColumns: '40px 1fr 50px' }}
          >
            <span
              className="rounded px-1.5 py-[2px] text-center font-[family-name:var(--font-mono)] text-[9.5px] font-semibold uppercase tracking-[0.04em]"
              style={{ background: `${tone[r.status]}1a`, color: tone[r.status] }}
            >
              {r.status}
            </span>
            <div className="min-w-0">
              <div className="truncate font-[family-name:var(--font-mono)] text-[11.5px] text-[var(--color-text-primary)]">
                {r.op}
              </div>
              <div className="mt-0.5 font-[family-name:var(--font-mono)] text-[10px] text-[var(--color-text-tertiary)]">
                {r.txn} · {r.dur}
              </div>
            </div>
            <span className="text-right font-[family-name:var(--font-mono)] text-[10.5px] text-[var(--color-text-tertiary)]">
              {r.t}
            </span>
          </div>
        ))}
      </div>
    </CardShell>
  );
}

function mapRunStatus(status: string): 'ok' | 'held' | 'err' {
  const lower = status.toLowerCase();
  if (lower === 'failed' || lower === 'error' || lower === 'err') return 'err';
  if (lower === 'pending' || lower === 'queued' || lower === 'held') return 'held';
  return 'ok';
}

function PolicyCard({ agent }: { agent: AgentConfigurationResponse }) {
  const autoApply = deriveAutoApply(agent.riskTier);
  const policies = DEMO_POLICIES.map((p) => ({
    ...p,
    enforced: p.iconKey === 'sparkles' ? autoApply : p.enforced,
  }));

  const renderIcon = (key: 'shield' | 'lock' | 'sparkles') => {
    const cls = 'h-3.5 w-3.5';
    if (key === 'shield') return <ShieldCheck className={cls} />;
    if (key === 'lock') return <Lock className={cls} />;
    return <Sparkles className={cls} />;
  };

  return (
    <CardShell title="Policies & safety" eyebrow={`${policies.filter((p) => p.enforced).length} active`}>
      <div className="flex flex-col gap-2.5 px-3.5 py-3">
        {policies.map((p, i) => (
          <div key={i} className="flex items-start gap-2.5">
            <span
              className="mt-0.5 flex-none"
              style={{ color: p.soft ? 'var(--color-brand-primary)' : 'var(--color-text-secondary)' }}
            >
              {renderIcon(p.iconKey)}
            </span>
            <div className="flex-1">
              <div className="text-[12.5px] font-medium text-[var(--color-text-primary)]">{p.title}</div>
              <div className="mt-0.5 text-[11px] text-[var(--color-text-secondary)]">{p.description}</div>
            </div>
            <Pill
              tone={p.enforced ? (p.soft ? 'info' : 'success') : 'muted'}
              size="sm"
            >
              {p.enforced ? (p.soft ? 'on' : 'enforced') : 'off'}
            </Pill>
          </div>
        ))}
      </div>
    </CardShell>
  );
}

// ── Sub-agents tab ────────────────────────────────────────────────

function SubAgentsTab() {
  const log = [
    { to: DEMO_SUB_AGENTS[0], op: 'apply_journal_entry', at: 'now', ms: 142, status: 'ok' },
    { to: DEMO_SUB_AGENTS[2], op: 'kyc_recheck', at: '2m', ms: 894, status: 'ok' },
    { to: DEMO_SUB_AGENTS[1], op: 'quote_forward', at: '4m', ms: 412, status: 'ok' },
    { to: DEMO_SUB_AGENTS[0], op: 'apply_journal_entry', at: '11m', ms: 188, status: 'held' },
    { to: DEMO_SUB_AGENTS[3], op: 'queue_reminder', at: '1h', ms: 142, status: 'ok' },
  ] as const;
  const tone = { ok: '#1f7a5e', held: '#b4741e', err: '#c44536' } as const;

  return (
    <div className="grid grid-cols-1 gap-6 xl:grid-cols-[1fr_380px]">
      <div className="flex flex-col gap-5">
        <Section
          title="Connected sub-agents"
          subtitle="Other agents this one delegates work to. Calls go through the orchestrator and inherit each callee's policies."
          count={DEMO_SUB_AGENTS.length}
          action={
            <>
              <Button variant="ghost" size="sm">
                Open call graph
              </Button>
              <Button variant="outline" size="sm">
                <Plus className="h-3 w-3" />
                Connect agent
              </Button>
            </>
          }
        >
          <div className="overflow-hidden rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)]">
            <div
              className="grid items-center gap-3.5 border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-4 py-2"
              style={{ gridTemplateColumns: '52px 1.5fr 1fr 110px 80px 90px 24px' }}
            >
              <div />
              <div className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">Agent · role</div>
              <div className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">Autonomy</div>
              <div className="text-right text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">Calls · 24h</div>
              <div className="text-right text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">Success</div>
              <div className="text-right text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">Avg · last</div>
              <div />
            </div>
            {DEMO_SUB_AGENTS.map((s, i, arr) => {
              const autonomyTone = AUTONOMY_TONE[s.autonomy];
              return (
                <div
                  key={s.id}
                  className={cn('grid cursor-pointer items-center gap-3.5 px-4 py-3.5', i < arr.length - 1 && 'border-b border-[var(--color-border-light)]')}
                  style={{ gridTemplateColumns: '52px 1.5fr 1fr 110px 80px 90px 24px' }}
                >
                  <AgentPortrait name={s.name} color={s.color} glyph={s.glyph} size={42} ring={false} />
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="text-[13.5px] font-semibold text-[var(--color-text-primary)]">{s.name}</span>
                      <Pill tone="info" size="sm">Domain</Pill>
                    </div>
                    <div className="mt-0.5 text-[11.5px] text-[var(--color-text-secondary)]">{s.role}</div>
                  </div>
                  <span className="justify-self-start rounded px-2 py-[3px] text-[9.5px] font-semibold uppercase tracking-[0.06em]" style={{ background: autonomyTone.bg, color: autonomyTone.fg }}>{autonomyTone.t}</span>
                  <span className="text-right font-[family-name:var(--font-mono)] text-[12px] text-[var(--color-text-secondary)]">{s.calls}</span>
                  <span className="text-right font-[family-name:var(--font-mono)] text-[12px]" style={{ color: s.successRate >= s.sla ? 'var(--color-success)' : '#b4741e' }}>{s.successRate.toFixed(1)}%</span>
                  <span className="text-right font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">{s.avgMs}ms · {s.last}</span>
                  <ChevronRight className="h-3 w-3 text-[var(--color-text-tertiary)]" />
                </div>
              );
            })}
          </div>
        </Section>

        <Section title="Recent delegations" subtitle="Invocation log across all sub-agents (24h)." count={log.length} action={<Button variant="ghost" size="sm">Open in Traces →</Button>}>
          <div className="overflow-hidden rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)]">
            {log.map((entry, i) => (
              <div key={`${entry.op}-${i}`} className={cn('grid items-center gap-3.5 px-3.5 py-2.5', i < log.length - 1 && 'border-b border-[var(--color-border-light)]')} style={{ gridTemplateColumns: '40px 1fr 1fr 80px 60px 50px' }}>
                <span className="rounded px-1.5 py-[2px] text-center font-[family-name:var(--font-mono)] text-[9.5px] font-semibold uppercase tracking-[0.04em]" style={{ background: `${tone[entry.status]}1a`, color: tone[entry.status] }}>{entry.status}</span>
                <div className="flex items-center gap-2">
                  <span className="text-[11px] text-[var(--color-text-secondary)]">self</span>
                  <ChevronRight className="h-2.5 w-2.5 text-[var(--color-text-tertiary)]" />
                  <AgentPortrait name={entry.to.name} color={entry.to.color} glyph={entry.to.glyph} size={20} ring={false} />
                  <span className="text-[11px] font-medium text-[var(--color-text-primary)]">{entry.to.name.replace(' Agent', '')}</span>
                </div>
                <span className="font-[family-name:var(--font-mono)] text-[11.5px] text-[var(--color-text-primary)]">{entry.op}</span>
                <span className="text-right font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">{entry.ms}ms</span>
                <span className="text-right font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">{entry.at}</span>
                <Button variant="ghost" size="sm" className="h-[22px] px-1.5 text-[10.5px]">trace</Button>
              </div>
            ))}
          </div>
        </Section>
      </div>

      <div className="flex flex-col gap-5">
        <ConnectionMapCard color="#eb5c37" agentName="Billing" />
        <CardShell title="How sub-agents work">
          <div className="p-3.5 text-[12px] leading-relaxed text-[var(--color-text-secondary)]">
            Calls inherit tenant scope and trace context. The callee still runs under its own policies, so Compliance can block even if Billing requested the check.
          </div>
        </CardShell>
      </div>
    </div>
  );
}

// ── Tools tab ─────────────────────────────────────────────────────

function ToolsTab({
  tools,
  color,
}: {
  agent: AgentConfigurationResponse;
  tools: DemoTool[];
  color: string;
}) {
  const [sel, setSel] = useState(0);
  const t = tools[sel];

  return (
    <div className="grid grid-cols-1 gap-6 xl:grid-cols-[1fr_460px]">
      <Section
        title="Tools"
        subtitle="Read pulls data; write mutates state and runs through policy clearance; compute is pure scoring; display renders UI in chat."
        count={tools.length}
        action={
          <>
            <Button variant="ghost" size="sm">
              Kind
            </Button>
            <Button variant="outline" size="sm">
              <Plus className="h-3 w-3" />
              Add tool
            </Button>
          </>
        }
      >
        <div className="overflow-hidden rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)]">
          {tools.map((tt, i) => {
            const tone = CAT_TONE[tt.cat];
            const active = i === sel;
            return (
              <button
                key={tt.name}
                type="button"
                onClick={() => setSel(i)}
                className={cn(
                  'grid w-full items-center gap-3.5 px-3.5 py-3.5 text-left',
                  i < tools.length - 1 && 'border-b border-[var(--color-border-light)]',
                  !tt.enabled && 'opacity-55',
                )}
                style={{
                  gridTemplateColumns: '90px 1fr 80px 80px 16px',
                  background: active ? `${color}0a` : 'transparent',
                  borderLeft: `3px solid ${active ? color : 'transparent'}`,
                }}
              >
                <span
                  className="justify-self-start rounded px-1.5 py-[2px] text-center font-[family-name:var(--font-mono)] text-[9.5px] font-semibold uppercase tracking-[0.06em]"
                  style={{ background: tone.bg, color: tone.fg }}
                >
                  {tt.cat}
                </span>
                <div>
                  <div className="font-[family-name:var(--font-mono)] text-[12.5px] font-semibold text-[var(--color-text-primary)]">
                    {tt.name}
                  </div>
                  <div className="mt-0.5 text-[11.5px] text-[var(--color-text-secondary)]">
                    {tt.desc}
                  </div>
                </div>
                <div className="text-right font-[family-name:var(--font-mono)] text-[12px] text-[var(--color-text-secondary)]">
                  {tt.uses.toLocaleString()}
                </div>
                <div
                  className="text-right font-[family-name:var(--font-mono)] text-[12px]"
                  style={{ color: tt.errors > 0 ? '#c44536' : 'var(--color-text-secondary)' }}
                >
                  {tt.errors > 0 ? `${tt.errors} err` : tt.p99}
                </div>
                <ChevronRight className="h-3 w-3" style={{ color: active ? color : 'var(--color-text-tertiary)' }} />
              </button>
            );
          })}
        </div>
      </Section>

      <div className="flex flex-col gap-4">
        <CardShell
          title={t.name}
          eyebrow={`${t.cat} tool · v2.1.0`}
          action={
            <Button variant="ghost" size="sm">
              Open in Playground →
            </Button>
          }
        >
          <div className="flex flex-col gap-3.5 p-3.5">
            <p className="m-0 text-[12.5px] leading-relaxed text-[var(--color-text-secondary)]">
              {t.desc}
            </p>
            <div className="grid grid-cols-3 gap-2">
              <MiniStat label="Uses · 24h" value={t.uses.toLocaleString()} />
              <MiniStat label="p99" value={t.p99} />
              <MiniStat
                label="Errors"
                value={t.errors.toString()}
                tone={t.errors > 0 ? '#c44536' : null}
              />
            </div>
            <div>
              <div className="mb-1.5 text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                Input schema
              </div>
              <pre className="m-0 whitespace-pre-wrap rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-3 font-[family-name:var(--font-mono)] text-[11px] leading-[1.55] text-[var(--color-text-primary)]">
{`{
  "candidate_id": "string",
  "context": "string",
  "tolerance_pct": "number  // default 0.5"
}`}
              </pre>
            </div>
            <div>
              <div className="mb-1.5 text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
                Returns
              </div>
              <pre className="m-0 whitespace-pre-wrap rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-3 font-[family-name:var(--font-mono)] text-[11px] leading-[1.55] text-[var(--color-text-primary)]">
{`{
  "score": 0.0..1.0,
  "reasons": [string],
  "result": object | null
}`}
              </pre>
            </div>
          </div>
        </CardShell>

        <CardShell title="Recent invocations" eyebrow={`last 5 of ${t.uses.toLocaleString()}`}>
          <div>
            {[
              { ms: 142, at: 'now', status: 'ok', ref: 'INV-2041' },
              { ms: 88, at: '11m', status: 'ok', ref: 'INV-2038' },
              { ms: 211, at: '24m', status: 'ok', ref: 'INV-2037' },
              { ms: 318, at: '1h', status: 'err', ref: 'INV-2031' },
              { ms: 142, at: '2h', status: 'ok', ref: 'INV-2024' },
            ].map((r, i, arr) => {
              const rowColor = r.status === 'ok' ? '#1f7a5e' : '#c44536';
              return (
                <div key={`${r.ref}-${i}`} className={cn('grid items-center gap-2.5 px-3.5 py-2', i < arr.length - 1 && 'border-b border-[var(--color-border-light)]')} style={{ gridTemplateColumns: '40px 1fr 60px 50px' }}>
                  <span className="rounded px-1.5 py-[2px] text-center font-[family-name:var(--font-mono)] text-[9.5px] font-semibold uppercase" style={{ background: `${rowColor}1a`, color: rowColor }}>{r.status}</span>
                  <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-primary)]">{r.ref}</span>
                  <span className="text-right font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">{r.ms}ms</span>
                  <span className="text-right font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">{r.at}</span>
                </div>
              );
            })}
          </div>
        </CardShell>
      </div>
    </div>
  );
}

function MiniStat({ label, value, tone }: { label: string; value: string; tone?: string | null }) {
  return (
    <div className="rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)] px-3 py-2">
      <div className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
        {label}
      </div>
      <div
        className="mt-1 font-[family-name:var(--font-mono)] text-[14px] font-semibold tabular-nums"
        style={{ color: tone ?? 'var(--color-text-primary)' }}
      >
        {value}
      </div>
    </div>
  );
}

// ── Skills tab ────────────────────────────────────────────────────

function SkillsTab({ color }: { color: string }) {
  const [selected, setSelected] = useState(0);
  const [filter, setFilter] = useState<'all' | 'active' | 'beta' | 'org' | 'community' | 'private'>('all');
  const details = DEMO_SKILLS.map((skill, index) => ({
    ...skill,
    slug: skill.name,
    displayName: skill.name.replace(/-/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase()),
    installed: index < 2 ? '2 weeks ago' : '12 days ago',
    hitRate: index === 4 ? 0.84 : 0.96,
    skillMd: index === 0 ? `---\nname: invoice-reconciliation\ndescription: Match incoming bank txns to open invoices.\nallowed-tools: [search_invoices, match_invoice_to_txn, draft_journal_entry]\n---\n\n# Invoice reconciliation\n\nUse this when a user asks to reconcile a receipt or match an invoice to a bank transaction.\n\n- Pull candidate transactions.\n- Score each candidate.\n- Draft a journal entry when confidence is high.\n- Require approval before posting.` : null,
  }));
  const filtered = details.filter((s) => filter === 'all' || s.status === filter || s.source === filter);
  const active = filtered[selected] ?? filtered[0];
  const sourceTone = { org: '#055a60', community: '#7b76b6', private: '#b4741e' } as const;

  return (
    <div className="flex flex-col gap-5">
      <div className="grid overflow-hidden rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] md:grid-cols-3">
        {[
          { eyebrow: '1 · Discovery', title: 'Read name + description', body: 'On every turn the agent skims registered skills; only frontmatter is loaded.' },
          { eyebrow: '2 · Activation', title: 'Load full SKILL.md', body: 'When a skill matches, its full instructions enter the context window.' },
          { eyebrow: '3 · Execution', title: 'Run scripts + refs', body: 'Bundled scripts, references, and assets are pulled only when needed.' },
        ].map((step, i) => (
          <div key={step.eyebrow} className={cn('p-4', i < 2 && 'border-b border-[var(--color-border-light)] md:border-b-0 md:border-r')}>
            <div className="font-[family-name:var(--font-mono)] text-[10px] font-semibold tracking-[0.06em]" style={{ color }}>{step.eyebrow}</div>
            <div className="mt-1 text-[13px] font-semibold text-[var(--color-text-primary)]">{step.title}</div>
            <div className="mt-1 text-[11.5px] leading-relaxed text-[var(--color-text-tertiary)]">{step.body}</div>
          </div>
        ))}
      </div>

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-[1fr_520px]">
        <Section
          title="Installed skills"
          subtitle="Each skill is a folder with a SKILL.md and optional bundled scripts, references, and assets."
          count={details.length}
          action={
            <>
              <Button variant="ghost" size="sm"><Search className="h-3 w-3" />Browse registry</Button>
              <Button variant="outline" size="sm"><Upload className="h-3 w-3" />Install skill</Button>
            </>
          }
        >
          <div className="mb-3 flex flex-wrap gap-1.5">
            {[
              { id: 'all', label: `All · ${details.length}` },
              { id: 'active', label: `Active · ${details.filter((x) => x.status === 'active').length}` },
              { id: 'beta', label: `Beta · ${details.filter((x) => x.status === 'beta').length}` },
              { id: 'org', label: 'Org' },
              { id: 'community', label: 'Community' },
              { id: 'private', label: 'Private' },
            ].map((f) => (
              <button
                key={f.id}
                type="button"
                onClick={() => { setFilter(f.id as typeof filter); setSelected(0); }}
                className="rounded-full border px-2.5 py-1 text-[11px] font-medium"
                style={{
                  background: filter === f.id ? `${color}14` : 'var(--color-surface)',
                  color: filter === f.id ? color : 'var(--color-text-secondary)',
                  borderColor: filter === f.id ? `${color}55` : 'var(--color-border-light)',
                }}
              >
                {f.label}
              </button>
            ))}
          </div>
          <div className="flex flex-col gap-2">
            {filtered.map((skill, i) => {
              const selectedSkill = active?.slug === skill.slug;
              const tone = sourceTone[skill.source];
              return (
                <button
                  key={skill.slug}
                  type="button"
                  onClick={() => setSelected(i)}
                  className="grid items-start gap-3 rounded-[10px] border p-3.5 text-left"
                  style={{ gridTemplateColumns: '32px 1fr auto', background: selectedSkill ? `${color}0a` : 'var(--color-surface)', borderColor: selectedSkill ? color : 'var(--color-border-light)' }}
                >
                  <div className="grid h-8 w-8 place-items-center rounded-[7px]" style={{ background: `${color}14`, color }}><Folder className="h-4 w-4" /></div>
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-[13.5px] font-semibold text-[var(--color-text-primary)]">{skill.displayName}</span>
                      <span className="font-[family-name:var(--font-mono)] text-[10.5px] text-[var(--color-text-tertiary)]">v{skill.version}</span>
                      {skill.status === 'beta' && <span className="rounded px-1.5 py-px text-[9.5px] font-semibold tracking-[0.04em]" style={{ background: '#b4741e1a', color: '#b4741e' }}>BETA</span>}
                      <span className="rounded px-1.5 py-px text-[9.5px] font-semibold uppercase tracking-[0.04em]" style={{ background: `${tone}14`, color: tone }}>{skill.source}</span>
                    </div>
                    <div className="mt-1 text-[12px] leading-relaxed text-[var(--color-text-secondary)]">{skill.desc}</div>
                  </div>
                  <div className="text-right whitespace-nowrap">
                    <div className="font-[family-name:var(--font-mono)] text-[12px] text-[var(--color-text-primary)]">{skill.last24h}</div>
                    <div className="mt-px text-[10px] text-[var(--color-text-tertiary)]">activations · 24h</div>
                  </div>
                </button>
              );
            })}
          </div>
        </Section>

        {active && (
          <div className="flex flex-col gap-4 xl:sticky xl:top-0">
            <CardShell
              title={active.displayName}
              eyebrow={<span className="font-[family-name:var(--font-mono)]">aonik-org/skills/{active.slug} · v{active.version}</span>}
              action={<><Button variant="ghost" size="sm"><Play className="h-3 w-3" />Test</Button><Button variant="outline" size="sm"><Edit3 className="h-3 w-3" />Edit</Button></>}
            >
              <div className="flex flex-col gap-3.5 p-3.5">
                <p className="m-0 text-[12.5px] leading-relaxed text-[var(--color-text-secondary)]">{active.desc}</p>
                <div className="grid grid-cols-3 gap-2">
                  <MiniStat label="Activations · 24h" value={active.last24h.toString()} />
                  <MiniStat label="Hit rate" value={`${Math.round(active.hitRate * 100)}%`} tone={active.hitRate >= 0.95 ? '#1f7a5e' : '#b4741e'} />
                  <MiniStat label="SKILL.md size" value="1.8 KB" />
                </div>
                {active.skillMd && (
                  <div>
                    <SmallLabel>SKILL.md · loaded on activation</SmallLabel>
                    <pre className="max-h-[260px] overflow-auto rounded-lg bg-[#1a1d21] p-3 font-[family-name:var(--font-mono)] text-[11px] leading-[1.55] text-[#d8dde2]">
                      {active.skillMd}
                    </pre>
                  </div>
                )}
                <div>
                  <SmallLabel>Bundle</SmallLabel>
                  <div className="rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-2 font-[family-name:var(--font-mono)] text-[11.5px] text-[var(--color-text-secondary)]">
                    {['SKILL.md', 'scripts/score.py', 'references/policy.md', 'assets/proposal.tmpl'].map((file) => (
                      <div key={file} className="flex items-center gap-2 rounded px-2 py-1">
                        <FileText className="h-3 w-3" style={{ color }} />
                        {file}
                      </div>
                    ))}
                  </div>
                </div>
                <div className="flex flex-wrap gap-x-4 gap-y-1 border-t border-[var(--color-border-light)] pt-2.5 text-[11px] text-[var(--color-text-tertiary)]">
                  <span>installed <b className="text-[var(--color-text-secondary)]">{active.installed}</b></span>
                  <span>visibility <b className="text-[var(--color-text-secondary)]">{active.source}</b></span>
                </div>
              </div>
            </CardShell>
          </div>
        )}
      </div>
    </div>
  );
}

// ── MCP tab ───────────────────────────────────────────────────────

function McpTab() {
  const servers = DEMO_MCP.map((server, index) => ({
    ...server,
    resources: index + 2,
    lastSync: server.status === 'connecting' ? 'in progress' : server.status === 'error' ? 'failed 18m ago' : index === 0 ? '12s ago' : '2m ago',
    err: server.err ? 'TLS handshake failed · cert chain incomplete' : undefined,
  }));
  const stTone = { connected: '#1f7a5e', connecting: '#b4741e', error: '#c44536' } as const;

  return (
    <Section
      title="MCP Servers"
      subtitle="Model Context Protocol servers this agent connects to. Each server exposes typed tools, resources, and prompt templates over a tenant-authenticated channel."
      count={servers.length}
      action={
        <>
          <Button variant="ghost" size="sm"><Search className="h-3 w-3" />Browse marketplace</Button>
          <Button variant="outline" size="sm"><Plus className="h-3 w-3" />Connect server</Button>
        </>
      }
    >
      <div className="flex flex-col gap-2.5">
        {servers.map((server) => {
          const color = stTone[server.status];
          return (
            <div key={server.name} className="overflow-hidden rounded-xl border bg-[var(--color-surface)]" style={{ borderColor: server.status === 'error' ? '#c4453633' : 'var(--color-border-light)' }}>
              <div className="grid items-center gap-3.5 px-4 py-3.5" style={{ gridTemplateColumns: '40px 1fr 100px 100px 100px 100px' }}>
                <div className="relative grid h-9 w-9 place-items-center rounded-lg bg-[var(--color-surface-inset)]">
                  <Server className="h-4 w-4 text-[var(--color-text-secondary)]" />
                  <span className="absolute -bottom-0.5 -right-0.5 h-2.5 w-2.5 rounded-full border-2 border-[var(--color-surface)]" style={{ background: color }} />
                </div>
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="text-[13.5px] font-semibold text-[var(--color-text-primary)]">{server.name}</span>
                    {server.native && <span className="rounded px-1.5 py-px text-[9.5px] font-semibold tracking-[0.04em] text-[var(--color-brand-primary)] bg-[var(--color-brand-primary-10)]">NATIVE</span>}
                    <span className="rounded bg-[var(--color-surface-inset)] px-1.5 py-px font-[family-name:var(--font-mono)] text-[9.5px] font-semibold tracking-[0.04em] text-[var(--color-text-tertiary)]">{server.auth}</span>
                  </div>
                  <div className="mt-0.5 truncate font-[family-name:var(--font-mono)] text-[10.5px] text-[var(--color-text-tertiary)]">{server.url}</div>
                </div>
                <McpMetric value={server.tools.toString()} label="tools" />
                <McpMetric value={server.resources.toString()} label="resources" />
                <McpMetric value={server.latency} label="latency" />
                <div className="flex justify-end gap-1.5">
                  <Button variant="ghost" size="sm" className="h-[26px] px-2"><RefreshCw className="h-3 w-3" /></Button>
                  <Button variant="outline" size="sm" className="h-[26px] px-2.5 text-[11px]">Manage</Button>
                </div>
              </div>
              {server.err && (
                <div className="flex items-center gap-2 border-t border-[#c4453633] bg-[#c4453608] px-4 py-2.5 text-[11.5px] text-[#c44536]">
                  <AlertTriangle className="h-3 w-3" />
                  {server.err}
                  <div className="flex-1" />
                  <Button variant="ghost" size="sm" className="h-[22px] px-2 text-[11px] text-[#c44536]">Retry</Button>
                  <Button variant="ghost" size="sm" className="h-[22px] px-2 text-[11px]">View logs</Button>
                </div>
              )}
              <div className="flex items-center gap-3 border-t border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-4 py-2 text-[11px] text-[var(--color-text-tertiary)]">
                <span>last sync · <b className="text-[var(--color-text-secondary)]">{server.lastSync}</b></span>
                <span>·</span>
                <span className="font-[family-name:var(--font-mono)]">v1.4.2</span>
              </div>
            </div>
          );
        })}
      </div>
    </Section>
  );
}

function McpMetric({ value, label }: { value: string; label: string }) {
  return (
    <div>
      <div className="font-[family-name:var(--font-mono)] text-[12px] text-[var(--color-text-primary)]">{value}</div>
      <div className="mt-px text-[10px] text-[var(--color-text-tertiary)]">{label}</div>
    </div>
  );
}

// ── Activity tab ──────────────────────────────────────────────────

function ActivityTab({
  agent,
  runs,
  runsLoading,
}: {
  agent: AgentConfigurationResponse;
  runs: PagedResult<AgentRunSummary> | null;
  runsLoading: boolean;
}) {
  const displayRuns = runs?.items.length
    ? runs.items.slice(0, 8).map((r) => ({
        id: r.id.slice(0, 8),
        op: r.goal || 'agent_run',
        status: mapRunStatus(r.status),
        dur: `${r.stepCount} steps`,
        t: formatRelativeTime(r.updatedAt ?? r.createdAt),
        txn: '—',
        tool: r.linkedAiRunCount,
        sub: 0,
      }))
    : DEMO_RUNS.map((r, i) => ({ ...r, id: `run_5a${i}`, tool: 4 + i, sub: i % 3 === 0 ? 1 : 0 }));
  const tone = { ok: '#1f7a5e', held: '#b4741e', err: '#c44536' } as const;

  return (
    <div className="grid grid-cols-1 gap-6 xl:grid-cols-[1fr_380px]">
      <Section
        title="Recent runs"
        subtitle={`Each row is a complete invocation dispatched to ${agent.name}. Click to open the trace.`}
        count={runs?.totalCount ?? '318 / 24h'}
        action={
          <>
            <Button variant="ghost" size="sm"><Filter className="h-3 w-3" />Status</Button>
            <Button variant="ghost" size="sm"><Calendar className="h-3 w-3" />24h</Button>
            <Button variant="outline" size="sm">Export CSV</Button>
          </>
        }
      >
        <div className="overflow-hidden rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)]">
          <div className="grid gap-3.5 border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-4 py-2.5" style={{ gridTemplateColumns: '50px 100px 1fr 90px 70px 60px 60px' }}>
            {['', 'Run', 'Operation', 'Subject', 'Tools', 'Dur', 'Age'].map((h, i) => (
              <div key={`${h}-${i}`} className={cn('text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]', i >= 4 && 'text-right')}>{h}</div>
            ))}
          </div>
          {runsLoading && (!runs || runs.items.length === 0) && <div className="px-4 py-6 text-center text-[12px] text-[var(--color-text-tertiary)]">Loading…</div>}
          {displayRuns.map((r, i) => (
            <div key={`${r.id}-${i}`} className={cn('grid cursor-pointer items-center gap-3.5 px-4 py-3', i < displayRuns.length - 1 && 'border-b border-[var(--color-border-light)]')} style={{ gridTemplateColumns: '50px 100px 1fr 90px 70px 60px 60px' }}>
              <span className="rounded px-1.5 py-[2px] text-center font-[family-name:var(--font-mono)] text-[9.5px] font-semibold uppercase" style={{ background: `${tone[r.status]}1a`, color: tone[r.status] }}>{r.status}</span>
              <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-secondary)]">{r.id}</span>
              <span className="truncate font-[family-name:var(--font-mono)] text-[12.5px] text-[var(--color-text-primary)]">{r.op}</span>
              <span className="font-[family-name:var(--font-mono)] text-[11.5px] text-[var(--color-text-secondary)]">{r.txn}</span>
              <span className="text-right font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">{r.tool}{r.sub > 0 && <span className="text-[var(--color-brand-primary)]"> +{r.sub}sub</span>}</span>
              <span className="text-right font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">{r.dur}</span>
              <span className="text-right font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">{r.t}</span>
            </div>
          ))}
        </div>
      </Section>

      <CardShell title="Live log stream" eyebrow="streaming · last 30s">
        <div className="max-h-[360px] overflow-auto bg-[#0e1620] p-3 font-[family-name:var(--font-mono)] text-[10.5px] leading-[1.7] text-[#d4dbe5]">
          {[
            { t: '12:04:18.214', l: 'I', m: 'agent.start  trace=tr_8c12' },
            { t: '12:04:18.301', l: 'I', m: 'tool.call    list_bank_transactions(window=72h)' },
            { t: '12:04:18.519', l: 'I', m: 'tool.return  rows=18' },
            { t: '12:04:18.731', l: 'I', m: 'tool.return  best_score=0.97 candidate=BNK-7741' },
            { t: '12:04:18.820', l: 'I', m: 'sub.call     ledger.apply_journal_entry' },
            { t: '12:04:19.055', l: 'I', m: 'tool.call    display_proposal_card(...)' },
            { t: '12:04:19.063', l: 'I', m: 'agent.end    status=ok dur=3.14s' },
          ].map((row, i) => (
            <div key={`${row.t}-${i}`} className="grid gap-2" style={{ gridTemplateColumns: '90px 14px 1fr' }}>
              <span className="text-[#6b7a8c]">{row.t}</span>
              <span className="text-[#5b9dd6]">{row.l}</span>
              <span>{row.m}</span>
            </div>
          ))}
        </div>
      </CardShell>
    </div>
  );
}

// ── Settings tab ──────────────────────────────────────────────────

function SettingsTab({
  agent,
  onEdit,
}: {
  agent: AgentConfigurationResponse;
  onEdit: () => void;
}) {
  const autoApply = deriveAutoApply(agent.riskTier);

  return (
    <div className="grid grid-cols-1 gap-6 xl:grid-cols-[1fr_360px]">
      <div className="flex flex-col gap-5">
        <CardShell title="General" action={<Button size="sm" onClick={onEdit}><Edit3 className="h-3 w-3" />Edit</Button>}>
          <div className="flex flex-col gap-3.5 p-4">
            <SettingLine label="Status" description="Pause this agent globally. It won't run, but its config stays put."><Pill tone={agent.isActive ? 'success' : 'warning'} dot size="sm">{agent.isActive ? 'Running' : 'Paused'}</Pill></SettingLine>
            <Divider />
            <SettingLine label="Auto-apply" description="Skip the proposal step when confidence and amount policies allow it."><SwitchPill on={autoApply} /></SettingLine>
            <Divider />
            <SettingLine label="Confidence threshold" description="Below this, the agent always asks for human review."><span className="font-[family-name:var(--font-mono)] font-semibold">0.95</span></SettingLine>
            <Divider />
            <SettingLine label="Amount ceiling" description="Hard cap above which dual approval is mandatory."><span className="font-[family-name:var(--font-mono)] font-semibold">£50,000</span></SettingLine>
          </div>
        </CardShell>

        <CardShell title="Routing">
          <div className="flex flex-col gap-3.5 p-4">
            <SettingLine label="Inbox" description="Where unresolved proposals land for human review."><span className="text-[12px] font-semibold">Treasury inbox</span></SettingLine>
            <Divider />
            <SettingLine label="Approver group" description="Two of these must sign off above the ceiling."><span className="text-[12px] font-semibold">Finance · 4 members</span></SettingLine>
            <Divider />
            <SettingLine label="Notification" description="Channel pinged for held items."><span className="font-[family-name:var(--font-mono)] text-[12px]">#fin-ops-alerts</span></SettingLine>
          </div>
        </CardShell>

        <CardShell title="Danger zone">
          <div className="flex flex-col gap-3 p-4">
            <DangerRow title="Reset memory" description="Forget conversation memory across all users. Cannot be undone." cta="Reset" />
            <DangerRow title="Disable agent" description="Stop running and hide from new chats. Existing traces remain." cta="Disable" />
            <DangerRow title="Delete agent" description="Permanently remove this agent and all its skills, schedules, and triggers." cta="Delete" destructive />
          </div>
        </CardShell>
      </div>

      <div className="flex flex-col gap-5">
        <CardShell title="Versioning" eyebrow="v0.42.1 · deployed 12d">
          <div className="flex flex-col gap-2 p-3.5">
            {[
              { v: 'v0.42.1', t: '12 days ago', by: 'maria', note: 'Tightened approval guardrails', active: true },
              { v: 'v0.42.0', t: '18 days ago', by: 'maria', note: 'Added FX reconciliation skill' },
              { v: 'v0.41.4', t: '1 month ago', by: 'aaron', note: 'Bumped match threshold' },
              { v: 'v0.41.0', t: '2 months ago', by: 'maria', note: 'Initial release' },
            ].map((version) => (
              <div key={version.v} className="grid items-center gap-2.5 rounded-lg border p-2.5" style={{ gridTemplateColumns: '70px 1fr auto', background: version.active ? 'var(--color-brand-primary-10)' : 'var(--color-surface-inset)', borderColor: version.active ? 'var(--color-brand-primary-20)' : 'var(--color-border-light)' }}>
                <span className="font-[family-name:var(--font-mono)] text-[11px] font-semibold text-[var(--color-text-primary)]">{version.v}</span>
                <div className="min-w-0">
                  <div className="text-[11.5px] text-[var(--color-text-primary)]">{version.note}</div>
                  <div className="mt-px text-[10px] text-[var(--color-text-tertiary)]">{version.t} · @{version.by}</div>
                </div>
                <Button variant="ghost" size="sm" className="h-[22px] px-2 text-[10.5px]">{version.active ? 'live' : 'roll back'}</Button>
              </div>
            ))}
          </div>
        </CardShell>

        <CardShell title="Current config" eyebrow={agent.id.slice(0, 12)}>
          <div className="grid gap-2 p-3.5">
            <SettingsRow label="Domain" value={agent.domain || '—'} />
            <SettingsRow label="Model" value={agent.modelName ?? '—'} mono />
            <SettingsRow label="Risk tier" value={agent.riskTier ?? '—'} />
            <SettingsRow label="Tools" value={countTools(agent.toolsetIdsJson).toString()} mono />
          </div>
        </CardShell>
      </div>
    </div>
  );
}

function SettingLine({ label, description, children }: { label: string; description?: string; children: React.ReactNode }) {
  return (
    <div className="flex items-center gap-3.5">
      <div className="flex-1">
        <div className="text-[12.5px] font-medium text-[var(--color-text-primary)]">{label}</div>
        {description && <div className="mt-0.5 text-[11.5px] text-[var(--color-text-secondary)]">{description}</div>}
      </div>
      <div className="text-[12px] text-[var(--color-text-primary)]">{children}</div>
    </div>
  );
}

function SwitchPill({ on }: { on: boolean }) {
  return (
    <span className="inline-flex h-[17px] w-[30px] items-center rounded-full p-0.5" style={{ background: on ? 'var(--color-brand-primary)' : 'var(--color-gray-300)' }}>
      <span className="h-[13px] w-[13px] rounded-full bg-white transition-transform" style={{ transform: on ? 'translateX(13px)' : 'translateX(0)' }} />
    </span>
  );
}

function Divider() {
  return <div className="h-px bg-[var(--color-border-light)]" />;
}

function DangerRow({ title, description, cta, destructive }: { title: string; description: string; cta: string; destructive?: boolean }) {
  return (
    <div className="grid items-center gap-3.5 rounded-lg border p-3" style={{ gridTemplateColumns: '1fr auto', borderColor: destructive ? '#c4453633' : 'var(--color-border-light)' }}>
      <div>
        <div className="text-[12.5px] font-medium" style={{ color: destructive ? '#c44536' : 'var(--color-text-primary)' }}>{title}</div>
        <div className="mt-0.5 text-[11.5px] text-[var(--color-text-secondary)]">{description}</div>
      </div>
      <Button variant="outline" size="sm" className={destructive ? 'border-[#c4453666] text-[#c44536]' : undefined}>{cta}</Button>
    </div>
  );
}

function SettingsRow({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="flex flex-col gap-1 rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface)] px-3.5 py-3">
      <span className="text-[10px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
        {label}
      </span>
      <span
        className={cn(
          'text-[13px] font-medium text-[var(--color-text-primary)]',
          mono && 'font-[family-name:var(--font-mono)]',
        )}
      >
        {value}
      </span>
    </div>
  );
}

// ── Shared sub-components ─────────────────────────────────────────

function Section({
  title,
  subtitle,
  count,
  action,
  children,
}: {
  title: string;
  subtitle?: string;
  count?: React.ReactNode;
  action?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <section>
      <div className="mb-3 flex flex-wrap items-end justify-between gap-3">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <h2 className="m-0 font-[family-name:var(--font-brand)] text-[18px] tracking-[-0.01em] text-[var(--color-text-primary)]">
              {title}
            </h2>
            {count != null && (
              <span className="rounded-full bg-[var(--color-surface-inset)] px-2 py-0.5 font-[family-name:var(--font-mono)] text-[11px] font-semibold text-[var(--color-text-tertiary)]">
                {count}
              </span>
            )}
          </div>
          {subtitle && (
            <p className="mt-1 max-w-[720px] text-[12.5px] leading-relaxed text-[var(--color-text-secondary)]">
              {subtitle}
            </p>
          )}
        </div>
        {action && <div className="flex items-center gap-2">{action}</div>}
      </div>
      {children}
    </section>
  );
}

function CardShell({
  title,
  eyebrow,
  action,
  children,
}: {
  title?: string;
  eyebrow?: React.ReactNode;
  action?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <div className="overflow-hidden rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)]">
      {(title || eyebrow) && (
        <div className="flex items-center gap-2 border-b border-[var(--color-border-light)] px-3.5 py-3">
          <div className="min-w-0 flex-1">
            {title && <div className="text-[13px] font-semibold text-[var(--color-text-primary)]">{title}</div>}
            {eyebrow && (
              <div className="mt-0.5 font-[family-name:var(--font-mono)] text-[10.5px] text-[var(--color-text-tertiary)]">
                {eyebrow}
              </div>
            )}
          </div>
          {action}
        </div>
      )}
      {children}
    </div>
  );
}

function SmallLabel({ children }: { children: React.ReactNode }) {
  return (
    <div className="mb-2 text-[10.5px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
      {children}
    </div>
  );
}
