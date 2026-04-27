import { useMemo } from 'react';
import { cn } from '@/lib/utils';

export interface AgentAvatarProps {
  /** Display name — initials are derived from the first two words. */
  name: string;
  /** Pixel size of the square. Border radius scales with size. */
  size?: number;
  /** Override the auto-derived background tint. */
  color?: string;
  /** Override the foreground letter colour. */
  textColor?: string;
  className?: string;
}

const AGENT_PALETTE = [
  '#055a60', // brand teal
  '#eb5c37', // coral
  '#3ab795', // mint
  '#7b76b6', // violet
  '#0097a9', // cyan
  '#5facbd', // sky
];

function hashName(name: string): number {
  let h = 0;
  for (let i = 0; i < name.length; i += 1) {
    h = (h * 31 + name.charCodeAt(i)) >>> 0;
  }
  return h;
}

function deriveInitials(name: string): string {
  return (name || '?')
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => word[0])
    .join('')
    .toUpperCase();
}

/**
 * Square avatar (rounded corners) with auto-tinted background derived from
 * the name. Mirrors the template's `Avatar` helper used across customer,
 * order, and party rows. The foreground colour is the full hue, the
 * background is a 13% (`22` hex alpha) tint of the same.
 */
export function AgentAvatar({
  name,
  size = 26,
  color,
  textColor,
  className,
}: AgentAvatarProps) {
  const palette = useMemo(() => {
    if (color || textColor) {
      return { bg: color, fg: textColor ?? color };
    }
    const hue = AGENT_PALETTE[hashName(name) % AGENT_PALETTE.length];
    return { bg: `${hue}22`, fg: hue };
  }, [name, color, textColor]);

  const radius = Math.round(size * 0.28);
  const fontSize = Math.round(size * 0.42);
  const initials = useMemo(() => deriveInitials(name), [name]);

  return (
    <span
      className={cn(
        'inline-flex flex-none items-center justify-center font-[family-name:var(--font-brand)] font-bold leading-none',
        className,
      )}
      style={{
        width: size,
        height: size,
        borderRadius: radius,
        background: palette.bg,
        color: palette.fg,
        fontSize,
      }}
      aria-hidden
    >
      {initials}
    </span>
  );
}
