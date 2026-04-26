// Aonik brand mark + wordmark used in the sidebar shell.
// Matches templates/aonik-admin-starterkit/kit/components.jsx (AonikMark/AonikWordmark):
// rounded teal square with "A", gold dot in the top-right corner; wordmark
// pairs the mark with the lowercase Infra-set "aonik" lockup.

interface AonikMarkProps {
  size?: number;
  color?: string;
  letterColor?: string;
}

export function AonikMark({
  size = 22,
  color = 'var(--color-brand-primary)',
  letterColor = '#fff',
}: AonikMarkProps) {
  const radius = Math.round(size * 0.25);
  const dotSize = Math.max(5, Math.round(size * 0.22));
  const dotInset = Math.max(2, Math.round(size * 0.12));

  return (
    <span
      style={{
        position: 'relative',
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        width: size,
        height: size,
        borderRadius: radius,
        background: color,
        color: letterColor,
        fontFamily: 'var(--font-brand)',
        fontWeight: 700,
        fontSize: Math.round(size * 0.6),
        letterSpacing: '-0.04em',
        lineHeight: 1,
        flex: 'none',
      }}
    >
      <span style={{ marginTop: -1, position: 'relative', zIndex: 1 }}>A</span>
      <span
        style={{
          position: 'absolute',
          top: dotInset,
          right: dotInset,
          width: dotSize,
          height: dotSize,
          borderRadius: '50%',
          background: 'var(--color-brand-mark-dot)',
          zIndex: 2,
        }}
      />
    </span>
  );
}

interface AonikWordmarkProps {
  size?: number;
}

export function AonikWordmark({ size = 19 }: AonikWordmarkProps) {
  return (
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 8,
        fontFamily: 'var(--font-brand)',
        fontWeight: 700,
        fontSize: size,
        letterSpacing: '-0.01em',
        color: 'var(--color-text-primary)',
      }}
    >
      <AonikMark size={Math.round(size * 1.1)} />
      aonik
    </span>
  );
}
