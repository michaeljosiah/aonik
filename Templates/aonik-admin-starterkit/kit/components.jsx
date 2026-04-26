// AONIK Admin UI — shared component primitives.
// All components exported to window so sibling Babel scripts can use them.

// ─── Icon (thin stroke, 1.5px, Tabler/Lucide-ish) ────────────────────────
function Icon({ name, size = 18, color = 'currentColor' }) {
  const paths = ICONS[name] || '';
  return (
    <svg
      width={size} height={size} viewBox="0 0 24 24"
      fill="none" stroke={color} strokeWidth="1.75"
      strokeLinecap="round" strokeLinejoin="round"
      style={{ flex: 'none', display: 'inline-block', verticalAlign: '-3px' }}
      dangerouslySetInnerHTML={{ __html: paths }}
    />
  );
}

const ICONS = {
  home:       '<path d="M3 11l9-7 9 7"/><path d="M5 10v10h14V10"/>',
  dashboard:  '<rect x="3" y="3" width="7" height="9"/><rect x="14" y="3" width="7" height="5"/><rect x="14" y="12" width="7" height="9"/><rect x="3" y="16" width="7" height="5"/>',
  ledger:     '<path d="M4 4h13a2 2 0 0 1 2 2v14H6a2 2 0 0 1-2-2V4z"/><path d="M4 4v14a2 2 0 0 0 2 2"/><path d="M8 8h8M8 12h8M8 16h5"/>',
  invoice:    '<path d="M6 3h9l4 4v14H6z"/><path d="M14 3v5h5"/><path d="M9 13h6M9 17h6"/>',
  bank:       '<path d="M3 10l9-6 9 6"/><path d="M5 10v7M10 10v7M14 10v7M19 10v7"/><path d="M3 20h18"/>',
  payout:     '<path d="M3 12h14"/><path d="M13 6l6 6-6 6"/><circle cx="4" cy="12" r="1.5"/>',
  shield:     '<path d="M12 3l8 3v6c0 5-3.5 8.5-8 9-4.5-.5-8-4-8-9V6z"/>',
  chart:      '<path d="M3 20h18"/><rect x="5" y="12" width="3" height="6"/><rect x="11" y="7" width="3" height="11"/><rect x="17" y="3" width="3" height="15"/>',
  users:      '<circle cx="9" cy="8" r="3"/><path d="M3 20c0-3 3-5 6-5s6 2 6 5"/><circle cx="17" cy="8" r="2.5"/><path d="M14 20c0-2.5 2.5-4 4-4"/>',
  settings:   '<circle cx="12" cy="12" r="3"/><path d="M19 12a7 7 0 0 0-.2-1.6l2-1.6-2-3.4-2.4.9a7 7 0 0 0-2.8-1.6L13 2h-4l-.6 2.7a7 7 0 0 0-2.8 1.6l-2.4-.9-2 3.4 2 1.6A7 7 0 0 0 3 12c0 .5.1 1.1.2 1.6l-2 1.6 2 3.4 2.4-.9a7 7 0 0 0 2.8 1.6L9 22h4l.6-2.7a7 7 0 0 0 2.8-1.6l2.4.9 2-3.4-2-1.6c.1-.5.2-1.1.2-1.6z"/>',
  search:     '<circle cx="11" cy="11" r="7"/><path d="m21 21-4.3-4.3"/>',
  bell:       '<path d="M6 8a6 6 0 0 1 12 0c0 7 3 8 3 8H3s3-1 3-8"/><path d="M10 20a2 2 0 0 0 4 0"/>',
  chevron:    '<path d="m9 6 6 6-6 6"/>',
  chevdown:   '<path d="m6 9 6 6 6-6"/>',
  chevup:     '<path d="m6 15 6-6 6 6"/>',
  plus:       '<path d="M12 5v14M5 12h14"/>',
  more:       '<circle cx="5" cy="12" r="1.5"/><circle cx="12" cy="12" r="1.5"/><circle cx="19" cy="12" r="1.5"/>',
  check:      '<path d="M5 12l5 5L20 7"/>',
  close:      '<path d="M6 6l12 12M18 6L6 18"/>',
  x:          '<path d="M6 6l12 12M18 6L6 18"/>',
  sparkles:   '<path d="M12 3l1.5 4.5L18 9l-4.5 1.5L12 15l-1.5-4.5L6 9l4.5-1.5z"/><path d="M19 15l.8 2.2L22 18l-2.2.8L19 21l-.8-2.2L16 18l2.2-.8z"/>',
  bot:        '<rect x="4" y="8" width="16" height="12" rx="3"/><path d="M12 3v5"/><circle cx="9" cy="14" r="1"/><circle cx="15" cy="14" r="1"/><path d="M2 14h2M20 14h2"/>',
  zap:        '<path d="M13 3L4 14h7l-1 7 9-11h-7z"/>',
  arrowup:    '<path d="M12 19V5M5 12l7-7 7 7"/>',
  arrowdown:  '<path d="M12 5v14M5 12l7 7 7-7"/>',
  arrowright: '<path d="M5 12h14M12 5l7 7-7 7"/>',
  filter:     '<path d="M3 5h18l-7 9v6l-4-2v-4z"/>',
  calendar:   '<rect x="3" y="5" width="18" height="16" rx="2"/><path d="M3 10h18M8 3v4M16 3v4"/>',
  download:   '<path d="M12 3v13"/><path d="m7 11 5 5 5-5"/><path d="M4 20h16"/>',
  upload:     '<path d="M12 20V7"/><path d="m7 12 5-5 5 5"/><path d="M4 4h16"/>',
  refresh:    '<path d="M4 12a8 8 0 0 1 14-5.3L21 9"/><path d="M21 4v5h-5"/><path d="M20 12a8 8 0 0 1-14 5.3L3 15"/><path d="M3 20v-5h5"/>',
  clock:      '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
  warn:       '<path d="M12 3 2 21h20z"/><path d="M12 10v5M12 18v.01"/>',
  dot:        '<circle cx="12" cy="12" r="4" fill="currentColor"/>',
  link:       '<path d="M10 13a5 5 0 0 0 7 0l3-3a5 5 0 0 0-7-7l-1 1"/><path d="M14 11a5 5 0 0 0-7 0l-3 3a5 5 0 0 0 7 7l1-1"/>',
  star:       '<path d="m12 3 3 6 6 .9-4.5 4.3 1 6.3L12 17.8l-5.5 2.7 1-6.3L3 9.9 9 9z"/>',
  trend:      '<path d="M3 17l6-6 4 4 8-8"/><path d="M14 7h7v7"/>',
  globe:      '<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3a14 14 0 0 1 0 18M12 3a14 14 0 0 0 0 18"/>',
  logo:       '<rect x="3" y="3" width="18" height="18" rx="4" fill="currentColor" stroke="none"/><circle cx="17" cy="7" r="2" fill="#e8a838" stroke="none"/>',
  send:       '<path d="m3 20 18-8L3 4l2 8-2 8z"/><path d="m5 12 16 0"/>',
  at:         '<circle cx="12" cy="12" r="4"/><path d="M16 8v5a3 3 0 0 0 6 0v-1a10 10 0 1 0-4 8"/>',
  minus:      '<path d="M5 12h14"/>',
};

// ─── Logo / wordmark ─────────────────────────────────────────────────────
// "A" inside a rounded teal square + gold dot in the top-right corner — the
// Aonik library glyph as defined in the design system loaders artifact.
function AonikMark({ size = 22, color = 'var(--brand-primary)', letterColor = '#fff' }) {
  const radius = Math.round(size * 0.25);
  const dotSize = Math.max(5, Math.round(size * 0.22));
  const dotInset = Math.max(2, Math.round(size * 0.12));
  return (
    <span style={{
      position: 'relative', display: 'inline-flex',
      alignItems: 'center', justifyContent: 'center',
      width: size, height: size,
      borderRadius: radius,
      background: color,
      color: letterColor,
      fontFamily: 'var(--font-brand)',
      fontWeight: 700,
      fontSize: Math.round(size * 0.6),
      letterSpacing: '-0.04em',
      lineHeight: 1,
      flex: 'none',
    }}>
      <span style={{ marginTop: -1, position: 'relative', zIndex: 1 }}>A</span>
      <span style={{
        position: 'absolute',
        top: dotInset, right: dotInset,
        width: dotSize, height: dotSize,
        borderRadius: '50%',
        background: 'var(--brand-mark-dot)',
        zIndex: 2,
      }}/>
    </span>
  );
}

function AonikWordmark({ size = 20 }) {
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 8,
      fontFamily: 'var(--font-brand)', fontWeight: 700,
      fontSize: size, letterSpacing: '-0.01em', color: 'var(--text-primary)'
    }}>
      <AonikMark size={Math.round(size * 1.1)}/>
      aonik
    </span>
  );
}

// ─── Avatar ──────────────────────────────────────────────────────────────
function Avatar({ name, color = 'var(--brand-primary-10)', textColor = 'var(--brand-primary)', size = 32 }) {
  const initials = (name || '?').split(' ').map(w => w[0]).slice(0, 2).join('').toUpperCase();
  return (
    <span style={{
      width: size, height: size, flex: 'none',
      borderRadius: Math.round(size * 0.28),
      background: color, color: textColor,
      display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
      fontFamily: 'var(--font-brand)', fontWeight: 700,
      fontSize: Math.round(size * 0.42),
    }}>{initials}</span>
  );
}

// ─── Pill ────────────────────────────────────────────────────────────────
function Pill({ tone = 'default', children, dot = false, size }) {
  const cls = {
    default: 'pill',
    tint: 'pill pill-tint',
    muted: 'pill',
    success: 'pill pill-success',
    warning: 'pill pill-warning',
    danger: 'pill pill-danger',
    pending: 'pill pill-pending',
  }[tone] || 'pill';
  const style = size === 'sm' ? { fontSize: 10, padding: '1px 7px', gap: 4 } : undefined;
  return (
    <span className={cls} style={style}>
      {dot && <span style={{
        width: size === 'sm' ? 5 : 6, height: size === 'sm' ? 5 : 6,
        borderRadius: 999, background: 'currentColor', flex: 'none'
      }}/>}
      {children}
    </span>
  );
}

// ─── KPI tile ────────────────────────────────────────────────────────────
function KPI({ label, value, delta, deltaTone = 'up', spark, sparkColor = '#055a60' }) {
  return (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 12, padding: 20, display: 'flex', flexDirection: 'column', gap: 14,
    }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
        <div style={{ fontSize: 13, color: 'var(--text-secondary)', fontWeight: 500 }}>{label}</div>
        {delta && (
          <span style={{
            fontSize: 11, fontWeight: 600, padding: '2px 8px', borderRadius: 999,
            background: deltaTone === 'up' ? 'var(--success-light)' : 'var(--danger-light)',
            color:      deltaTone === 'up' ? 'var(--success)'       : 'var(--danger)',
            display: 'inline-flex', alignItems: 'center', gap: 4,
          }}>
            <Icon name={deltaTone === 'up' ? 'arrowup' : 'arrowdown'} size={10}/>
            {delta}
          </span>
        )}
      </div>
      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 26, fontWeight: 600, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>
        {value}
      </div>
      {spark && (
        <svg viewBox="0 0 100 30" preserveAspectRatio="none" style={{ width: '100%', height: 32, display: 'block' }}>
          <defs>
            <linearGradient id={`sg-${label.replace(/\s/g,'')}`} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor={sparkColor} stopOpacity="0.25"/>
              <stop offset="100%" stopColor={sparkColor} stopOpacity="0"/>
            </linearGradient>
          </defs>
          <polygon fill={`url(#sg-${label.replace(/\s/g,'')})`} points={`0,30 ${spark} 100,30`}/>
          <polyline fill="none" stroke={sparkColor} strokeWidth="1.5" points={spark}/>
        </svg>
      )}
    </div>
  );
}

// ─── Card ────────────────────────────────────────────────────────────────
function Card({ title, subtitle, action, children, padding = 20, style }) {
  return (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 12, ...style,
    }}>
      {(title || action) && (
        <div style={{
          display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between',
          padding: `${padding}px ${padding}px 0`, gap: 16,
        }}>
          <div>
            {title && <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>{title}</div>}
            {subtitle && <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 2 }}>{subtitle}</div>}
          </div>
          {action}
        </div>
      )}
      <div style={{ padding }}>{children}</div>
    </div>
  );
}

// ─── Export ──────────────────────────────────────────────────────────────
Object.assign(window, {
  Icon, ICONS, AonikMark, AonikWordmark, Avatar, Pill, KPI, Card,
});
