// Solid-colour party avatar — port of `PartyAvatar` from
// templates/aonik-admin-starterkit/screens/orders.jsx.
//
// The template hard-codes a colour per demo party. Production parties don't
// carry a brand colour, so we hash the name into the categorical chart
// palette (--chart-1..5) and render the full hue as the background with
// white initials. This is
// distinct from the workspace's `AgentAvatar`, which uses a 13% tint with
// coloured text — that style is for AI/system rows, not party rows.

const PALETTE = [
  'var(--chart-1)',
  'var(--chart-2)',
  'var(--chart-3)',
  'var(--chart-4)',
  'var(--chart-5)',
];

function hash(value: string): number {
  let h = 0;
  for (let i = 0; i < value.length; i += 1) {
    h = (h * 31 + value.charCodeAt(i)) >>> 0;
  }
  return h;
}

function deriveInitials(name: string): string {
  return (name || '?')
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0])
    .join('')
    .toUpperCase();
}

export interface PartyAvatarProps {
  name: string;
  size?: number;
  /** Optional explicit colour override (otherwise hashed from the name). */
  color?: string;
}

export function PartyAvatar({ name, size = 32, color }: PartyAvatarProps) {
  const bg = color ?? PALETTE[hash(name) % PALETTE.length];
  return (
    <span
      className="inline-flex flex-none items-center justify-center font-bold leading-none text-white"
      style={{
        width: size,
        height: size,
        borderRadius: Math.round(size * 0.28),
        background: bg,
        fontSize: Math.round(size * 0.38),
      }}
      aria-hidden
    >
      {deriveInitials(name)}
    </span>
  );
}
