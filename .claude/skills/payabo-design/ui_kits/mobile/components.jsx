// Payabo core UI primitives — buttons, fields, chips, rows, cards
// Reflects lib/shared/widgets/payabo_* from the mobile codebase.

const PAY = {
  orange: '#F37920', orangeHover: '#D55F0B',
  ink: '#1A1C20',
  warm050: '#FFFCF9', warm100: '#FFFBF7', warm150: '#F7EEE4',
  warm200: '#F4ECDE', warm300: '#DCCDB7', warm500: '#D7A14E',
  warm600: '#9B7A43', warm800: '#77594A', warm900: '#4D3120',
  n100: '#F2F4F4', n200: '#E5E9EA', n500: '#B4BFC3',
  navBorder: '#F0E7DA', navSelected: '#C29752', navUnselected: '#99958F',
  success: '#4ACB64', success050: '#ECFAEF',
  warning: '#FF9E15', danger: '#E60037', info: '#2465E8',
  heroTop: '#242223', heroMid: '#191718', heroBot: '#0F0D0E',
  chatTop: '#261C16', chatMid: '#1A130F', chatBot: '#120D0A',
  font: '"Open Sans", -apple-system, system-ui, sans-serif',
};

const payHero = `linear-gradient(180deg, ${PAY.heroTop} 0%, ${PAY.heroMid} 46%, ${PAY.heroBot} 100%)`;
const payChatHero = `linear-gradient(180deg, ${PAY.chatTop} 0%, ${PAY.chatMid} 46%, ${PAY.chatBot} 100%)`;
const payWarmScreen = `linear-gradient(180deg, ${PAY.warm050} 0%, ${PAY.warm150} 100%)`;
const payChatScreen = 'linear-gradient(135deg, #FBF5EE 0%, #F2DEC8 100%)';
const paySafe = 'linear-gradient(135deg, #122C1C 0%, #285634 100%)';

// ── Button (matches payabo_button.dart: uppercase, 48h default, 4px radius) ──
function PayButton({ children, variant = 'primary', size = 'md', leading, trailing, full, onClick, disabled, style = {} }) {
  const h = size === 'sm' ? 40 : size === 'lg' ? 52 : 48;
  const base = {
    border: 'none', borderRadius: 4, height: h, padding: '0 16px',
    font: `700 12px/16px ${PAY.font}`, letterSpacing: 0.1, textTransform: 'uppercase',
    display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 8,
    cursor: disabled ? 'not-allowed' : 'pointer',
    width: full ? '100%' : undefined, transition: 'all 120ms', ...style,
  };
  const variants = {
    primary: { background: disabled ? PAY.n100 : PAY.orange, color: disabled ? PAY.n500 : 'white' },
    secondary: { background: 'transparent', color: PAY.orange, border: `2px solid ${PAY.orange}`, height: h - 4 },
    link: {
      background: 'white', color: PAY.orange, border: `1px solid ${PAY.orange}`,
      fontWeight: 400, fontSize: 14, textTransform: 'none',
    },
    ghost: { background: 'transparent', color: PAY.ink },
  };
  return <button onClick={onClick} disabled={disabled} style={{ ...base, ...variants[variant] }}>
    {leading}{children}{trailing}
  </button>;
}

// ── Text field (Material-style underline; active = orange, label shrinks) ──
function PayField({ label, value, onChange, placeholder, error, type = 'text', prefix }) {
  const [focused, setFocused] = React.useState(false);
  const hasContent = value && value.length > 0;
  const active = focused || hasContent;
  const borderColor = error ? PAY.danger : active ? PAY.orange : PAY.n200;
  return (
    <div style={{ paddingTop: 4 }}>
      <div style={{
        font: active ? `400 12px/16px ${PAY.font}` : `600 14px/20px ${PAY.font}`,
        color: error ? PAY.danger : active ? PAY.orange : PAY.n500,
        transition: 'all 120ms',
      }}>{label}</div>
      <div style={{
        display: 'flex', alignItems: 'center', gap: 8,
        borderBottom: `1px solid ${borderColor}`, paddingBottom: 10, marginTop: active ? 4 : -2,
      }}>
        {prefix}
        <input
          type={type} value={value || ''} placeholder={placeholder}
          onFocus={() => setFocused(true)} onBlur={() => setFocused(false)}
          onChange={e => onChange && onChange(e.target.value)}
          style={{
            border: 0, outline: 'none', background: 'transparent',
            font: `400 14px/20px ${PAY.font}`, color: PAY.ink,
            flex: 1, width: '100%', padding: 0,
          }}
        />
      </div>
      {error && <div style={{ font: `400 11px/16px ${PAY.font}`, color: PAY.danger, marginTop: 6 }}>{error}</div>}
    </div>
  );
}

// ── Chip / status pill ──
function PayChip({ children, tone = 'neutral', leading }) {
  const tones = {
    success: { bg: PAY.success050, fg: '#1B7030', bd: PAY.success },
    warning: { bg: '#FFF3E0', fg: '#8A5100', bd: PAY.warning },
    danger: { bg: '#FCEBEE', fg: '#8A0022', bd: PAY.danger },
    info: { bg: '#E8F0FF', fg: '#123a8f', bd: PAY.info },
    neutral: { bg: PAY.warm200, fg: PAY.warm900, bd: 'transparent' },
    warm: { bg: '#FFF3E8', fg: '#7A3211', bd: '#F1DEC9' },
  };
  const t = tones[tone];
  return <span style={{
    display: 'inline-flex', alignItems: 'center', gap: 6,
    padding: '4px 10px', borderRadius: 50,
    background: t.bg, color: t.fg, border: `1px solid ${t.bd}`,
    font: `600 11px/14px ${PAY.font}`, letterSpacing: 0.1,
  }}>{leading}{children}</span>;
}

// ── Card — white with subtle warm shadow ──
function PayCard({ children, variant = 'white', style = {}, onClick }) {
  const vs = {
    white: { background: 'white', border: `1px solid rgba(180,191,195,0.4)`, boxShadow: '0 4px 14px rgba(77,49,32,0.07), 0 1px 2px rgba(77,49,32,0.05)' },
    warm: { background: '#FFFBF8', border: `1px solid #F1DEC9` },
    dark: { background: 'rgba(255,255,255,0.06)', border: '1px solid rgba(255,255,255,0.08)', color: 'white' },
    flat: { background: 'white', border: `1px solid ${PAY.n200}` },
  };
  return <div onClick={onClick} style={{
    borderRadius: 4, padding: 16, ...vs[variant], cursor: onClick ? 'pointer' : 'default', ...style,
  }}>{children}</div>;
}

// ── Icons — simple outline stroke, match Material Rounded feel ──
function Icon({ name, size = 22, color = 'currentColor', strokeWidth = 2 }) {
  const paths = {
    home: <path d="M3 11l9-8 9 8v10a2 2 0 01-2 2h-4v-7H9v7H5a2 2 0 01-2-2V11z"/>,
    pay: <><circle cx="12" cy="12" r="9"/><path d="M8 12h8M12 8l4 4-4 4"/></>,
    spending: <><rect x="3" y="7" width="18" height="13" rx="2"/><path d="M16 7V4a2 2 0 00-2-2h-4a2 2 0 00-2 2v3"/></>,
    chat: <path d="M21 15a2 2 0 01-2 2H7l-4 4V5a2 2 0 012-2h14a2 2 0 012 2z"/>,
    bell: <><path d="M6 8a6 6 0 1112 0c0 7 3 7 3 9H3c0-2 3-2 3-9z"/><path d="M10 19a2 2 0 104 0"/></>,
    account: <><circle cx="12" cy="8" r="4"/><path d="M4 21c0-4.4 3.6-8 8-8s8 3.6 8 8"/></>,
    back: <path d="M15 18l-6-6 6-6"/>,
    close: <path d="M18 6L6 18M6 6l12 12"/>,
    add: <path d="M12 5v14M5 12h14"/>,
    chev: <path d="M9 18l6-6-6-6"/>,
    send: <path d="M22 2L11 13M22 2l-7 20-4-9-9-4z"/>,
    eye: <><path d="M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12z"/><circle cx="12" cy="12" r="3"/></>,
    eyeOff: <><path d="M17.94 17.94A10.07 10.07 0 0112 20c-6 0-10-8-10-8a18.45 18.45 0 014.09-5.52M9.9 4.24A10.08 10.08 0 0112 4c6 0 10 8 10 8a18.5 18.5 0 01-2.16 3.19"/><path d="M1 1l22 22"/></>,
    search: <><circle cx="11" cy="11" r="7"/><path d="M21 21l-4.35-4.35"/></>,
    scan: <><path d="M4 8V4h4M16 4h4v4M20 16v4h-4M8 20H4v-4"/><path d="M7 12h10"/></>,
    qr: <><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><path d="M14 14h3v3h-3zM19 14h2M14 19h2v2M19 17v4"/></>,
    card: <><rect x="2" y="5" width="20" height="14" rx="2"/><path d="M2 10h20"/></>,
    bill: <path d="M3 10l2-6h14l2 6M3 10v10h18V10M3 10h18"/>,
    user: <><circle cx="12" cy="8" r="4"/><path d="M4 21c0-4.4 3.6-8 8-8s8 3.6 8 8"/></>,
    plus: <path d="M12 5v14M5 12h14"/>,
    arrowUp: <path d="M12 19V5M5 12l7-7 7 7"/>,
    arrowDown: <path d="M12 5v14M19 12l-7 7-7-7"/>,
    mic: <><rect x="9" y="3" width="6" height="11" rx="3"/><path d="M5 11a7 7 0 0014 0M12 18v3"/></>,
    sparkle: <path d="M12 3l2 5 5 2-5 2-2 5-2-5-5-2 5-2 2-5zM19 14l1 2.5 2.5 1-2.5 1-1 2.5-1-2.5L15.5 17l2.5-1 1-2.5z"/>,
    check: <path d="M20 6L9 17l-5-5"/>,
    settings: <><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 11-2.83 2.83l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 11-4 0v-.09a1.65 1.65 0 00-1-1.51 1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 11-2.83-2.83l.06-.06A1.65 1.65 0 004.6 15a1.65 1.65 0 00-1.51-1H3a2 2 0 110-4h.09A1.51 1.51 0 004.6 9"/></>,
    filter: <path d="M3 6h18M6 12h12M10 18h4"/>,
  };
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color}
      strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round">
      {paths[name] || <circle cx="12" cy="12" r="10"/>}
    </svg>
  );
}

// ── Transaction row ──
function PayTxnRow({ avatar, avatarBg = '#FFEFE3', avatarFg = '#7A3211', title, sub, amount, chip, onClick }) {
  return <div onClick={onClick} style={{
    display: 'flex', alignItems: 'center', gap: 12, padding: '12px 0',
    borderBottom: `1px solid ${PAY.n200}`, cursor: onClick ? 'pointer' : 'default',
  }}>
    <div style={{
      width: 40, height: 40, borderRadius: 50, flex: 'none',
      background: avatarBg, color: avatarFg, display: 'flex', alignItems: 'center', justifyContent: 'center',
      font: `700 13px/18px ${PAY.font}`,
    }}>{avatar}</div>
    <div style={{ flex: 1, minWidth: 0 }}>
      <div style={{ font: `600 13px/18px ${PAY.font}`, color: PAY.ink, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{title}</div>
      <div style={{ font: `400 11px/16px ${PAY.font}`, color: PAY.n500 }}>{sub}</div>
    </div>
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 4 }}>
      <div style={{ font: `600 13px/18px ${PAY.font}`, color: PAY.ink }}>{amount}</div>
      {chip}
    </div>
  </div>;
}

// ── Bottom nav bar with center FAB ──
function PayBottomNav({ current = 'home', onChange, onFabClick }) {
  const items = [
    { id: 'home', icon: 'home', label: 'Home' },
    { id: 'pay', icon: 'pay', label: 'Pay' },
    { id: 'spending', icon: 'spending', label: 'Spending' },
    { id: 'chat', icon: 'chat', label: 'Chat' },
  ];
  return <div style={{
    position: 'relative', background: 'white', borderTop: `1px solid ${PAY.navBorder}`,
    height: 74, display: 'flex', alignItems: 'center', justifyContent: 'space-around',
    boxShadow: '0 -1px 10px rgba(0,0,0,0.07)', flex: 'none',
  }}>
    {items.slice(0, 2).map(it => <NavItem key={it.id} {...it} current={current} onChange={onChange}/>)}
    <div style={{ width: 72 }}/>
    {items.slice(2).map(it => <NavItem key={it.id} {...it} current={current} onChange={onChange}/>)}
    <div onClick={onFabClick} style={{
      position: 'absolute', top: -18, left: '50%', transform: 'translateX(-50%)',
      width: 58, height: 58, borderRadius: 50, background: PAY.orange, color: 'white',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      boxShadow: '0 4px 12px rgba(0,0,0,0.15)', border: '4px solid white', cursor: 'pointer',
    }}><Icon name="add" size={22} strokeWidth={2.5} color="white"/></div>
  </div>;
}
function NavItem({ id, icon, label, current, onChange }) {
  const on = current === id;
  return <div onClick={() => onChange && onChange(id)} style={{
    display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4, width: 60,
    color: on ? PAY.navSelected : PAY.navUnselected,
    font: `${on ? 600 : 400} 11px/14px ${PAY.font}`, cursor: 'pointer',
  }}><Icon name={icon} size={22}/>{label}</div>;
}

Object.assign(window, {
  PAY, payHero, payChatHero, payWarmScreen, payChatScreen, paySafe,
  PayButton, PayField, PayChip, PayCard, Icon, PayTxnRow, PayBottomNav,
});
