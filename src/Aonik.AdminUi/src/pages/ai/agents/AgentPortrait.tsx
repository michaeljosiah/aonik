// 1:1 port of `AgentPortrait` + `PortraitGlyph` from
// templates/aonik-admin-starterkit/screens/agents-page.jsx.
//
// Generative SVG avatar — gradient field tinted with the agent's hue plus
// one of 8 geometric glyphs. We deliberately avoid photo-real / human
// imagery for AI agents: each agent gets a distinctive geometric "face"
// keyed on its name (via deriveAgentColor / deriveAgentGlyph).

import { useId } from 'react';
import type { AgentGlyph } from './agentMeta';

export interface AgentPortraitProps {
  /** Agent name (used to derive the SVG-clip id and aria label). */
  name: string;
  /** Hex colour for the gradient field. */
  color: string;
  /** Geometric glyph to render in the centre. */
  glyph: AgentGlyph;
  /** Pixel size of the square. Border radius scales with size. */
  size?: number;
  /** Render the soft inner ring outline (hidden when used inside swatches). */
  ring?: boolean;
}

function PortraitGlyph({ glyph, color }: { glyph: AgentGlyph; color: string }) {
  const w = '#fff';
  switch (glyph) {
    case 'orbital':
      return (
        <g>
          <ellipse cx={40} cy={40} rx={26} ry={10} fill="none" stroke={w} strokeWidth={1.2} opacity={0.55} />
          <ellipse cx={40} cy={40} rx={26} ry={10} fill="none" stroke={w} strokeWidth={1.2} opacity={0.4} transform="rotate(60 40 40)" />
          <ellipse cx={40} cy={40} rx={26} ry={10} fill="none" stroke={w} strokeWidth={1.2} opacity={0.4} transform="rotate(-60 40 40)" />
          <circle cx={40} cy={40} r={6} fill={w} />
          <circle cx={40} cy={40} r={3} fill={color} />
        </g>
      );
    case 'columns':
      return (
        <g fill={w}>
          <rect x={22} y={26} width={4} height={28} rx={1} opacity={0.95} />
          <rect x={30} y={34} width={4} height={20} rx={1} opacity={0.85} />
          <rect x={38} y={22} width={4} height={32} rx={1} />
          <rect x={46} y={30} width={4} height={24} rx={1} opacity={0.85} />
          <rect x={54} y={38} width={4} height={16} rx={1} opacity={0.7} />
          <line x1={20} y1={58} x2={60} y2={58} stroke={w} strokeWidth={1} opacity={0.5} />
        </g>
      );
    case 'docstack':
      return (
        <g>
          <rect x={22} y={22} width={34} height={40} rx={3} fill={w} opacity={0.18} transform="rotate(-6 39 42)" />
          <rect x={24} y={20} width={34} height={40} rx={3} fill={w} opacity={0.55} transform="rotate(-2 41 40)" />
          <rect x={26} y={20} width={32} height={40} rx={3} fill={w} opacity={0.95} />
          <line x1={30} y1={30} x2={50} y2={30} stroke={color} strokeWidth={1.8} strokeLinecap="round" />
          <line x1={30} y1={36} x2={46} y2={36} stroke={color} strokeWidth={1.8} strokeLinecap="round" opacity={0.7} />
          <line x1={30} y1={42} x2={48} y2={42} stroke={color} strokeWidth={1.8} strokeLinecap="round" opacity={0.7} />
          <line x1={30} y1={48} x2={40} y2={48} stroke={color} strokeWidth={1.8} strokeLinecap="round" opacity={0.5} />
        </g>
      );
    case 'wave':
      return (
        <g fill="none" stroke={w} strokeLinecap="round" strokeWidth={2}>
          <path d="M14 50 Q24 36 32 44 T54 38 Q62 35 66 30" opacity={0.85} />
          <path d="M14 56 Q24 44 32 50 T54 44 Q62 41 66 38" opacity={0.55} />
          <circle cx={54} cy={38} r={3.4} fill={w} stroke="none" />
        </g>
      );
    case 'shield':
      return (
        <g>
          <path d="M40 18 L58 25 L58 42 Q58 56 40 62 Q22 56 22 42 L22 25 Z" fill={w} opacity={0.95} />
          <path d="M32 40 L38 46 L50 32" fill="none" stroke={color} strokeWidth={3} strokeLinecap="round" strokeLinejoin="round" />
        </g>
      );
    case 'rings':
      return (
        <g fill="none" stroke={w} strokeWidth={2}>
          <circle cx={40} cy={40} r={22} opacity={0.35} />
          <circle cx={40} cy={40} r={14} opacity={0.6} />
          <circle cx={40} cy={40} r={6} fill={w} stroke="none" />
          <path d="M40 18 A22 22 0 0 1 62 40" stroke={w} strokeWidth={2.5} strokeLinecap="round" />
        </g>
      );
    case 'envelope':
      return (
        <g>
          <rect x={20} y={26} width={40} height={28} rx={3} fill={w} opacity={0.95} />
          <path d="M20 28 L40 44 L60 28" fill="none" stroke={color} strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" />
          <circle cx={58} cy={50} r={6} fill={color} />
          <circle cx={58} cy={50} r={2.2} fill={w} />
        </g>
      );
    case 'pulse':
      return (
        <g fill="none" stroke={w} strokeWidth={2.4} strokeLinecap="round" strokeLinejoin="round">
          <path d="M14 42 H26 L30 32 L36 52 L42 36 L48 46 L54 42 H66" opacity={0.95} />
          <circle cx={48} cy={46} r={3} fill={w} stroke="none" />
        </g>
      );
    default:
      return <circle cx={40} cy={40} r={14} fill={w} />;
  }
}

export function AgentPortrait({
  name,
  color,
  glyph,
  size = 64,
  ring = true,
}: AgentPortraitProps) {
  const idBase = useId().replace(/:/g, '');
  const radius = size * 0.18;

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 80 80"
      role="img"
      aria-label={`${name} portrait`}
      style={{ flex: 'none', display: 'block', borderRadius: radius, overflow: 'hidden' }}
    >
      <defs>
        <linearGradient id={`${idBase}-bg`} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity={0.95} />
          <stop offset="100%" stopColor={color} stopOpacity={0.55} />
        </linearGradient>
        <radialGradient id={`${idBase}-glow`} cx="0.7" cy="0.25" r="0.7">
          <stop offset="0%" stopColor="#fff" stopOpacity={0.35} />
          <stop offset="100%" stopColor="#fff" stopOpacity={0} />
        </radialGradient>
        <clipPath id={`${idBase}-clip`}>
          <rect x={0} y={0} width={80} height={80} rx={radius * (80 / size)} />
        </clipPath>
      </defs>
      <g clipPath={`url(#${idBase}-clip)`}>
        <rect width={80} height={80} fill={`url(#${idBase}-bg)`} />
        <rect width={80} height={80} fill={`url(#${idBase}-glow)`} />
        {/* subtle grain dots */}
        {Array.from({ length: 18 }).map((_, i) => {
          const x = (i * 37) % 80;
          const y = (i * 59) % 80;
          return <circle key={i} cx={x} cy={y} r={0.4} fill="#fff" opacity={0.15} />;
        })}
        <PortraitGlyph glyph={glyph} color={color} />
      </g>
      {ring && (
        <rect
          x={0.5}
          y={0.5}
          width={79}
          height={79}
          rx={radius * (80 / size)}
          fill="none"
          stroke="#fff"
          strokeOpacity={0.15}
        />
      )}
    </svg>
  );
}
