// Animated hero — port of `AgentDetailHero` + `HeroOrbs` from
// templates/aonik-admin-starterkit/screens/agent-detail.jsx.
//
// Renders a tinted gradient field with floating colour orbs, an animated
// orbital portrait at left, the agent identity in the centre, and a
// configuration card at right. The portrait spins, drifts gently, and
// shows a pulse if the agent has live runs.

import { Edit3, MoreHorizontal, Pause, Play, Terminal } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Pill, type PillTone } from '@/components/layout/aonik';
import type { AgentConfigurationResponse } from '@/types/ai';

import { AgentPortrait } from './AgentPortrait';
import {
  deriveAgentColor,
  deriveAgentGlyph,
  deriveAgentState,
  deriveAutoApply,
  deriveKindLabel,
  formatRelativeTime,
} from './agentMeta';

const HERO_KEYFRAMES = `
@keyframes agt-spin-slow { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
@keyframes agt-spin-rev  { from { transform: rotate(360deg); } to { transform: rotate(0deg); } }
@keyframes agt-float     { 0%, 100% { transform: translateY(0); } 50% { transform: translateY(-6px); } }
@keyframes agt-pulse     { 0% { transform: scale(1); opacity: 0.5; } 100% { transform: scale(2.4); opacity: 0; } }
@keyframes agt-drift     { 0%, 100% { transform: translate(0, 0); } 50% { transform: translate(20px, 14px); } }
`;

const STATE_PILL: Record<ReturnType<typeof deriveAgentState>, { tone: PillTone; label: string }> = {
  running: { tone: 'success', label: 'Running' },
  idle: { tone: 'muted', label: 'Idle' },
  paused: { tone: 'warning', label: 'Paused' },
};

export interface AgentDetailHeroProps {
  agent: AgentConfigurationResponse;
  /** Whether the agent has had any recent runs (drives "Running" badge + pulse). */
  hasRecentRun?: boolean;
  /** Most recent run timestamp, used for the "deployed N ago" line. */
  lastRunAt?: string | null;
  onEdit: () => void;
  onOpenPlayground?: () => void;
  onOpenTraces?: () => void;
}

export function AgentDetailHero({
  agent,
  hasRecentRun,
  lastRunAt,
  onEdit,
  onOpenPlayground,
  onOpenTraces,
}: AgentDetailHeroProps) {
  const color = deriveAgentColor(agent.name);
  const glyph = deriveAgentGlyph(agent.name);
  // Trust isActive — when an agent is active in production, show "Running"
  // even without a recent-run signal so the live state reads correctly. The
  // template's hero treats active = running.
  const state = deriveAgentState(agent.isActive, agent.isActive || (hasRecentRun ?? false));
  const autoApply = deriveAutoApply(agent.riskTier);
  const pillMeta = STATE_PILL[state];

  return (
    <div
      className="relative overflow-hidden border-b border-[var(--color-border-light)]"
      style={{
        padding: '36px 32px 32px',
        background: `linear-gradient(135deg, ${color}1a 0%, ${color}08 60%, transparent 100%)`,
      }}
    >
      <style>{HERO_KEYFRAMES}</style>

      {/* Floating orbs */}
      <span
        className="pointer-events-none absolute"
        style={{
          top: -80,
          right: '18%',
          width: 220,
          height: 220,
          borderRadius: '50%',
          background: `radial-gradient(circle, ${color}1f 0%, transparent 65%)`,
          animation: 'agt-drift 18s ease-in-out infinite',
        }}
      />
      <span
        className="pointer-events-none absolute"
        style={{
          bottom: -60,
          left: '8%',
          width: 180,
          height: 180,
          borderRadius: '50%',
          background: `radial-gradient(circle, ${color}14 0%, transparent 65%)`,
          animation: 'agt-drift 22s ease-in-out infinite reverse',
        }}
      />

      <div className="relative mx-auto flex max-w-[1600px] items-start gap-7">
        {/* Animated portrait */}
        <div className="relative flex-none" style={{ width: 144, height: 144 }}>
          <span
            aria-hidden
            className="absolute"
            style={{
              inset: -8,
              borderRadius: '50%',
              border: `1.5px dashed ${color}66`,
              animation: 'agt-spin-slow 28s linear infinite',
            }}
          />
          <span
            aria-hidden
            className="absolute"
            style={{
              inset: -18,
              borderRadius: '50%',
              border: `1px solid ${color}33`,
              animation: 'agt-spin-rev 42s linear infinite',
            }}
          />
          {/* Outer orbital dot */}
          <span
            aria-hidden
            className="absolute"
            style={{ inset: -18, animation: 'agt-spin-rev 42s linear infinite' }}
          >
            <span
              className="absolute"
              style={{
                top: -3,
                left: '50%',
                width: 6,
                height: 6,
                borderRadius: 999,
                background: color,
                boxShadow: `0 0 10px ${color}`,
              }}
            />
          </span>

          {/* Portrait + state pulse */}
          <div
            className="relative"
            style={{
              width: 144,
              height: 144,
              animation: 'agt-float 6s ease-in-out infinite',
              filter: `drop-shadow(0 12px 24px ${color}33)`,
            }}
          >
            <AgentPortrait name={agent.name} color={color} glyph={glyph} size={144} />
            {state === 'running' && (
              <span
                className="absolute"
                style={{
                  bottom: 6,
                  right: 6,
                  width: 14,
                  height: 14,
                  borderRadius: 999,
                  background: 'var(--color-success)',
                  boxShadow: '0 0 0 3px var(--color-surface)',
                }}
              >
                <span
                  className="absolute inset-0"
                  style={{
                    borderRadius: 999,
                    background: 'var(--color-success)',
                    opacity: 0.4,
                    animation: 'agt-pulse 1.6s ease-out infinite',
                  }}
                />
              </span>
            )}
          </div>
        </div>

        {/* Identity */}
        <div className="min-w-0 flex-1 pt-1.5">
          <div className="mb-2.5 flex flex-wrap items-center gap-2.5">
            <span
              className="rounded-[4px] px-2 py-[3px] text-[10.5px] font-semibold uppercase tracking-[0.12em]"
              style={{ color, background: `${color}1a` }}
            >
              {deriveKindLabel(agent.agentType)} Agent
            </span>
            <Pill tone={pillMeta.tone} dot size="sm">
              {pillMeta.label}
            </Pill>
            <span className="font-[family-name:var(--font-mono)] text-[11.5px] text-[var(--color-text-tertiary)]">
              v0.42.1
            </span>
            <span className="text-[11.5px] text-[var(--color-text-tertiary)]">
              · deployed {formatRelativeTime(agent.createdAt)}
            </span>
            {agent.isOverride && (
              <span
                className="rounded-[4px] px-1.5 py-[2px] text-[10px] font-semibold uppercase tracking-[0.08em]"
                style={{ color: 'var(--color-brand-secondary)', background: 'var(--color-brand-secondary-10)' }}
              >
                Override
              </span>
            )}
          </div>

          <h1
            className="m-0 mb-2 font-[family-name:var(--font-brand)] font-bold tracking-[-0.02em] text-[var(--color-text-primary)]"
            style={{ fontSize: 38, lineHeight: 1.05 }}
          >
            {agent.name}
          </h1>

          <p className="m-0 mb-4 max-w-[720px] text-[15px] leading-relaxed text-[var(--color-text-secondary)]">
            {agent.description || 'No description set.'}
          </p>

          <div className="flex items-center gap-2">
            <Button onClick={onEdit}>
              <Edit3 className="h-3.5 w-3.5" />
              Edit agent
            </Button>
            <Button variant="outline" onClick={onOpenPlayground}>
              <Play className="h-3.5 w-3.5" />
              Run in Playground
            </Button>
            <Button variant="ghost" onClick={onOpenTraces}>
              <Terminal className="h-3.5 w-3.5" />
              View traces
            </Button>
            <div className="flex-1" />
            <Button variant="ghost" size="sm" aria-label="Pause">
              <Pause className="h-3.5 w-3.5" />
            </Button>
            <Button variant="ghost" size="sm" aria-label="More">
              <MoreHorizontal className="h-3.5 w-3.5" />
            </Button>
          </div>
        </div>

        {/* Configuration card */}
        <div
          className="flex-none rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4 shadow-[0_4px_16px_-8px_rgba(20,25,30,0.08)]"
          style={{ width: 220 }}
        >
          <div className="mb-2.5 text-[10px] font-semibold uppercase tracking-[0.08em] text-[var(--color-text-tertiary)]">
            Configuration
          </div>
          <div className="flex flex-col gap-2.5">
            <ConfRow label="Model" value={agent.modelName ?? 'claude-sonnet-4.5'} mono />
            <ConfRow label="Temperature" value="0.2" mono />
            <ConfRow label="Owner" value={agent.domain ? capitalize(agent.domain) + ' team' : 'Treasury team'} />
            <ConfRow
              label="Auto-apply"
              value={autoApply ? 'Enabled' : 'Off'}
              accent={autoApply ? 'var(--color-success)' : null}
            />
            <ConfRow label="Region" value="eu-west-2" mono />
            {lastRunAt && <ConfRow label="Last run" value={formatRelativeTime(lastRunAt)} mono />}
          </div>
        </div>
      </div>
    </div>
  );
}

function capitalize(s: string): string {
  if (!s) return s;
  return s[0].toUpperCase() + s.slice(1);
}

function ConfRow({
  label,
  value,
  mono,
  accent,
}: {
  label: string;
  value: string;
  mono?: boolean;
  accent?: string | null;
}) {
  return (
    <div className="flex items-center justify-between gap-2.5">
      <span className="text-[11.5px] text-[var(--color-text-tertiary)]">{label}</span>
      <span
        className="text-[12px] font-semibold"
        style={{
          fontFamily: mono ? 'var(--font-mono)' : 'inherit',
          color: accent ?? 'var(--color-text-primary)',
        }}
      >
        {value}
      </span>
    </div>
  );
}
