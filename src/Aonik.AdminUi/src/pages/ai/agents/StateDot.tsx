// Small status indicator — port of `StateDot` from
// templates/aonik-admin-starterkit/screens/agents-page.jsx.
//
// Colour-coded dot with a label. The "running" state gets a soft
// shadow ring around the dot to read as live.

import type { AgentDisplayState } from './agentMeta';

const STATE_META: Record<AgentDisplayState, { color: string; label: string }> = {
  running: { color: 'var(--color-success)', label: 'Running' },
  idle: { color: 'var(--color-gray-400)', label: 'Idle' },
  paused: { color: 'var(--color-warning)', label: 'Paused' },
};

export interface StateDotProps {
  state: AgentDisplayState;
  /** Hide the text label and render only the dot (for table cells / row tails). */
  iconOnly?: boolean;
  /** Override the rendered label (e.g. show the raw status string). */
  label?: string;
}

export function StateDot({ state, iconOnly, label }: StateDotProps) {
  const meta = STATE_META[state];
  return (
    <span className="inline-flex items-center gap-1.5">
      <span
        className="rounded-full"
        style={{
          width: 7,
          height: 7,
          background: meta.color,
          boxShadow: state === 'running' ? `0 0 0 3px ${meta.color}33` : 'none',
        }}
      />
      {!iconOnly && (
        <span className="text-[11px] font-medium text-[var(--color-text-secondary)]">
          {label ?? meta.label}
        </span>
      )}
    </span>
  );
}
