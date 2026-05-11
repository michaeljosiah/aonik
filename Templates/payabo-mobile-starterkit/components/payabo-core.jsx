// Payabo core: tokens, icons, primitives.
// Pushes the existing language by adding: orange glow orbs, dotted compass rings,
// expressive numerics, marquee strips, and a floating Simi presence.

const PAY = {
  orange: '#F37920', orangeHover: '#D55F0B', orangeSoft: '#F3A85C',
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
const paySafe = 'linear-gradient(135deg, #122C1C 0%, #285634 100%)';

// ── Icons (Lucide-shaped, 2px stroke, rounded caps) ───────────────────────
function Icon({ name, size = 22, color = 'currentColor', strokeWidth = 2 }) {
  const p = {
    home: <path d="M3 11l9-8 9 8v10a2 2 0 01-2 2h-4v-7H9v7H5a2 2 0 01-2-2V11z"/>,
    pay: <><circle cx="12" cy="12" r="9"/><path d="M8 12h8M12 8l4 4-4 4"/></>,
    spending: <><rect x="3" y="7" width="18" height="13" rx="2"/><path d="M16 7V4a2 2 0 00-2-2h-4a2 2 0 00-2 2v3"/></>,
    chat: <path d="M21 15a2 2 0 01-2 2H7l-4 4V5a2 2 0 012-2h14a2 2 0 012 2z"/>,
    bell: <><path d="M6 8a6 6 0 1112 0c0 7 3 7 3 9H3c0-2 3-2 3-9z"/><path d="M10 19a2 2 0 104 0"/></>,
    back: <path d="M15 18l-6-6 6-6"/>,
    close: <path d="M18 6L6 18M6 6l12 12"/>,
    add: <path d="M12 5v14M5 12h14"/>,
    chev: <path d="M9 18l6-6-6-6"/>,
    chevDown: <path d="M6 9l6 6 6-6"/>,
    chevUp: <path d="M6 15l6-6 6 6"/>,
    send: <path d="M22 2L11 13M22 2l-7 20-4-9-9-4z"/>,
    star: <path d="M12 2l2.9 6.2 6.6.7-4.9 4.7 1.4 6.6L12 17l-5.9 3.2 1.4-6.6L2.6 8.9l6.6-.7z"/>,
    shield: <path d="M12 2l8 4v6c0 5-3.5 8.5-8 10-4.5-1.5-8-5-8-10V6z"/>,
    compass: <><circle cx="12" cy="12" r="9"/><path d="M16 8l-2 6-6 2 2-6z"/></>,
    target: <><circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="5"/><circle cx="12" cy="12" r="1.5" fill="currentColor"/></>,
    spark: <path d="M12 3v4M12 17v4M3 12h4M17 12h4M5.6 5.6l2.8 2.8M15.6 15.6l2.8 2.8M5.6 18.4l2.8-2.8M15.6 8.4l2.8-2.8"/>,
    mic: <><rect x="9" y="3" width="6" height="12" rx="3"/><path d="M5 11a7 7 0 0014 0M12 18v3M8 21h8"/></>,
    speaker: <><path d="M4 9v6h4l5 4V5L8 9H4z"/><path d="M16.5 8.5a5 5 0 010 7M19.5 5.5a9 9 0 010 13"/></>,
    receipt: <><path d="M5 3h14v18l-3-2-3 2-3-2-3 2-2-2z"/><path d="M8 8h8M8 12h8M8 16h5"/></>,
    transfer: <><path d="M3 8h14M14 5l3 3-3 3"/><path d="M21 16H7M10 19l-3-3 3-3"/></>,
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
    arrowUpRight: <path d="M7 17L17 7M7 7h10v10"/>,
    arrowDownLeft: <path d="M17 7L7 17M17 17H7V7"/>,
    mic: <><rect x="9" y="3" width="6" height="11" rx="3"/><path d="M5 11a7 7 0 0014 0M12 18v3"/></>,
    sparkle: <path d="M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8L12 3z"/>,
    check: <path d="M20 6L9 17l-5-5"/>,
    settings: <><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 11-2.83 2.83l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 11-4 0v-.09a1.65 1.65 0 00-1-1.51 1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 11-2.83-2.83l.06-.06A1.65 1.65 0 004.6 15a1.65 1.65 0 00-1.51-1H3a2 2 0 110-4h.09A1.51 1.51 0 004.6 9"/></>,
    globe: <><circle cx="12" cy="12" r="10"/><path d="M2 12h20M12 2a15 15 0 010 20 15 15 0 010-20"/></>,
    filter: <path d="M3 6h18M6 12h12M10 18h4"/>,
    bolt: <path d="M13 2L3 14h7l-1 8 10-12h-7l1-8z"/>,
    bullseye: <><circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="5"/><circle cx="12" cy="12" r="1.5" fill={color}/></>,
    target: <><circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="5"/><circle cx="12" cy="12" r="1.5"/></>,
    trend: <path d="M3 17l6-6 4 4 8-8M14 7h7v7"/>,
    waveform: <path d="M2 12h2M6 8v8M10 4v16M14 8v8M18 10v4M22 12h0"/>,
    pause: <><rect x="6" y="5" width="4" height="14" rx="1"/><rect x="14" y="5" width="4" height="14" rx="1"/></>,
    play: <path d="M6 4l14 8-14 8V4z"/>,
    download: <path d="M12 3v12m0 0l-4-4m4 4l4-4M5 21h14"/>,
    share: <><circle cx="18" cy="5" r="3"/><circle cx="6" cy="12" r="3"/><circle cx="18" cy="19" r="3"/><path d="M8.6 13.5l6.9 4M15.5 6.5l-6.9 4"/></>,
    flame: <path d="M12 2s4 4 4 8a4 4 0 11-8 0c0-1 .5-2 1-3 .5 1 1 2 2 2 0-3-1-5 1-7zM6 14c0 4 3 7 6 7s6-3 6-7c0-2-1-3-2-4-.5 2-1.5 3-3 3-1 0-2-1-2-2-1 1-2 2-3 2s-2-1-2 1z"/>,
    coffee: <><path d="M3 8h14v8a4 4 0 01-4 4H7a4 4 0 01-4-4V8z"/><path d="M17 11h2a2 2 0 010 4h-2M7 2v3M11 2v3M15 2v3"/></>,
    wifi: <><path d="M2 8.5a16 16 0 0120 0M5 12a11 11 0 0114 0M8.5 15.5a6 6 0 017 0"/><circle cx="12" cy="19" r="1" fill={color}/></>,
    zap: <path d="M13 2L4 14h7l-1 8 9-12h-7l1-8z"/>,
    lock: <><rect x="4" y="11" width="16" height="10" rx="2"/><path d="M8 11V7a4 4 0 018 0v4"/></>,
    moon: <path d="M21 12.8A9 9 0 1111.2 3a7 7 0 009.8 9.8z"/>,
    sun: <><circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/></>,
  };
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color}
      strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round">
      {p[name] || <circle cx="12" cy="12" r="10"/>}
    </svg>
  );
}

// ── Button ────────────────────────────────────────────────────────────────
function PayButton({ children, variant = 'primary', size = 'md', leading, trailing, full, onClick, disabled, style = {} }) {
  const h = size === 'sm' ? 40 : size === 'lg' ? 52 : 48;
  const base = {
    border: 'none', borderRadius: 4, height: h, padding: '0 18px',
    font: `700 12px/16px ${PAY.font}`, letterSpacing: 0.4, textTransform: 'uppercase',
    display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 8,
    cursor: disabled ? 'not-allowed' : 'pointer',
    width: full ? '100%' : undefined, transition: 'all 120ms', ...style,
  };
  const variants = {
    primary: { background: disabled ? PAY.n100 : PAY.orange, color: disabled ? PAY.n500 : 'white' },
    secondary: { background: 'transparent', color: PAY.orange, border: `2px solid ${PAY.orange}`, height: h - 4 },
    link: { background: 'white', color: PAY.orange, border: `1px solid ${PAY.orange}`, fontWeight: 400, fontSize: 14, textTransform: 'none' },
    ghost: { background: 'transparent', color: PAY.ink },
    dark: { background: 'rgba(255,255,255,0.08)', color: 'white', border: '1px solid rgba(255,255,255,0.12)' },
  };
  return <button onClick={onClick} disabled={disabled} style={{ ...base, ...variants[variant] }}>
    {leading}{children}{trailing}
  </button>;
}

// ── Chip ─────────────────────────────────────────────────────────────────
function PayChip({ children, tone = 'neutral', leading, style = {} }) {
  const tones = {
    success: { bg: PAY.success050, fg: '#1B7030', bd: 'rgba(74,203,100,0.4)' },
    warning: { bg: '#FFF3E0', fg: '#8A5100', bd: 'rgba(255,158,21,0.35)' },
    danger: { bg: '#FCEBEE', fg: '#8A0022', bd: 'rgba(230,0,55,0.3)' },
    info: { bg: '#E8F0FF', fg: '#123a8f', bd: 'rgba(36,101,232,0.3)' },
    neutral: { bg: PAY.warm200, fg: PAY.warm900, bd: 'transparent' },
    warm: { bg: '#FFF3E8', fg: '#7A3211', bd: '#F1DEC9' },
    dark: { bg: 'rgba(255,255,255,0.08)', fg: 'rgba(255,255,255,0.85)', bd: 'rgba(255,255,255,0.12)' },
  };
  const t = tones[tone];
  return <span style={{
    display: 'inline-flex', alignItems: 'center', gap: 6,
    padding: '4px 10px', borderRadius: 50,
    background: t.bg, color: t.fg, border: `1px solid ${t.bd}`,
    font: `600 11px/14px ${PAY.font}`, letterSpacing: 0.3, ...style,
  }}>{leading}{children}</span>;
}

// ── Flag chip with svg flag ────────────────────────────────────────────────
function FlagChip({ cc, label, onClick, dark }) {
  return (
    <div onClick={onClick} style={{
      display: 'inline-flex', alignItems: 'center', gap: 8,
      padding: '6px 12px 6px 6px', borderRadius: 50,
      background: dark ? 'rgba(255,255,255,0.06)' : 'white',
      border: `1px solid ${dark ? 'rgba(255,255,255,0.1)' : '#F1E5D1'}`,
      color: dark ? 'white' : PAY.warm900,
      font: `600 12px/16px ${PAY.font}`, cursor: 'pointer',
    }}>
      <div style={{
        width: 22, height: 22, borderRadius: 50, overflow: 'hidden',
        backgroundImage: `url('assets/flags/${cc}.svg')`,
        backgroundSize: 'cover', backgroundPosition: 'center',
        boxShadow: dark ? '0 0 0 1px rgba(255,255,255,0.15)' : '0 0 0 1px #F1E5D1',
      }}/>
      {label}
    </div>
  );
}

// ── The orange glow orb — used in hero compositions ───────────────────────
function GlowOrb({ size = 240, color = PAY.orange, opacity = 0.35, top, left, right, bottom, blur = 60 }) {
  return <div style={{
    position: 'absolute', top, left, right, bottom,
    width: size, height: size, borderRadius: '50%',
    background: color, opacity, filter: `blur(${blur}px)`, pointerEvents: 'none',
  }}/>;
}

// ── Section header ────────────────────────────────────────────────────────
function SectionHeader({ title, action, onAction, kicker, style = {} }) {
  return (
    <div style={{ display: 'flex', alignItems: 'flex-end', padding: '0 4px 10px', ...style }}>
      <div style={{ flex: 1 }}>
        {kicker && <div style={{
          font: `700 10px/14px ${PAY.font}`, color: PAY.warm600,
          letterSpacing: 1.2, textTransform: 'uppercase', marginBottom: 2,
        }}>{kicker}</div>}
        <div style={{ font: `700 17px/22px ${PAY.font}`, color: PAY.warm900 }}>{title}</div>
      </div>
      {action && <div onClick={onAction} style={{
        font: `700 11px/14px ${PAY.font}`, color: PAY.orange,
        textTransform: 'uppercase', letterSpacing: 0.8, cursor: 'pointer',
      }}>{action}</div>}
    </div>
  );
}

// ── Stat tile ────────────────────────────────────────────────────────────
function StatTile({ label, value, tone = 'warm', trend }) {
  const tones = {
    warm: { bg: PAY.warm150, bd: '#ECD9BE', fg: PAY.ink, lab: PAY.warm800 },
    dark: { bg: 'rgba(255,255,255,0.05)', bd: 'rgba(255,255,255,0.08)', fg: 'white', lab: 'rgba(255,255,255,0.6)' },
    accent: { bg: '#FFF3E8', bd: '#F1DEC9', fg: '#7A3211', lab: '#9B5121' },
  };
  const t = tones[tone];
  return (
    <div style={{
      flex: 1, padding: 14, borderRadius: 14,
      background: t.bg, border: `1px solid ${t.bd}`,
    }}>
      <div style={{ font: `500 11px/14px ${PAY.font}`, color: t.lab, letterSpacing: 0.2 }}>{label}</div>
      <div style={{ font: `700 17px/22px ${PAY.font}`, color: t.fg, marginTop: 4, letterSpacing: -0.2 }}>{value}</div>
      {trend && <div style={{ font: `600 10px/14px ${PAY.font}`, color: trend.startsWith('+') ? '#1B7030' : '#8A0022', marginTop: 2 }}>{trend}</div>}
    </div>
  );
}

// ── Compass rings — decorative dotted concentric rings (push the language) ─
function CompassRings({ size = 320, color = 'rgba(243,121,32,0.18)', opacity = 1 }) {
  return (
    <svg width={size} height={size} viewBox="0 0 320 320" style={{ position: 'absolute', opacity }}>
      {[40, 80, 120, 156].map((r, i) => (
        <circle key={r} cx="160" cy="160" r={r}
          fill="none" stroke={color}
          strokeDasharray={i % 2 === 0 ? "1 4" : "2 6"}
          strokeWidth="1"/>
      ))}
      <circle cx="160" cy="160" r="2" fill={color}/>
    </svg>
  );
}

// ── Pulsing dot ──────────────────────────────────────────────────────────
function PulseDot({ color = PAY.orange, size = 8 }) {
  return (
    <span style={{ position: 'relative', display: 'inline-block', width: size, height: size }}>
      <span style={{
        position: 'absolute', inset: 0, borderRadius: '50%', background: color,
        animation: 'payPulse 1.6s ease-out infinite', opacity: 0.6,
      }}/>
      <span style={{ position: 'absolute', inset: 0, borderRadius: '50%', background: color }}/>
    </span>
  );
}

// ── Typewriter ────────────────────────────────────────────────────────────
function Typewriter({ text, speed = 22, onDone, cursor = true, style = {} }) {
  const [n, setN] = React.useState(0);
  React.useEffect(() => {
    if (n < text.length) {
      const t = setTimeout(() => setN(n + 1), speed);
      return () => clearTimeout(t);
    } else if (onDone) onDone();
  }, [n, text]);
  React.useEffect(() => { setN(0); }, [text]);
  return <span style={style}>
    {text.slice(0, n)}
    {cursor && n < text.length && <span style={{ display: 'inline-block', width: 6, height: '0.85em', background: 'currentColor', verticalAlign: '-1px', marginLeft: 2, animation: 'payBlink 0.9s steps(2) infinite' }}/>}
  </span>;
}

// ── Voice orb — Simi's realtime pulse
//   Mirrors realtime_voice_stage.dart: one 1.8s pulse drives N concentric rings
//   sampled at phase offsets; an inner core breathes via sin(2π·pulse).
//   Intensity is gated on `speaker`: bot → strongest, user → calm listening,
//   none → gentle breath.
function VoiceOrb({ size = 220, speaker = 'bot', phase: voicePhase }) {
  // intensity multiplier — drives halo opacity & breath amplitude
  const intensity = speaker === 'bot' ? 1.0 : speaker === 'user' ? 0.6 : 0.4;
  const [pulse, setPulse] = React.useState(0);
  React.useEffect(() => {
    let raf;
    const period = 2600;
    const t0 = performance.now();
    const tick = (now) => {
      setPulse(((now - t0) % period) / period);
      raf = requestAnimationFrame(tick);
    };
    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, []);
  // breath: sin(2π·pulse) → [-1,1] → [0,1] — single smooth cycle
  const breath = (Math.sin(pulse * 2 * Math.PI) + 1) / 2;
  const coreSize = size * 0.55 + (breath * 6 * intensity);
  const innerGlow = 0.78 + (breath * 0.12 * intensity);
  // single circular halo, scales gently with the breath
  const haloScale = 1 + breath * 0.06 * intensity;
  const haloOpacity = (0.16 + breath * 0.08) * intensity;
  return (
    <div style={{ position: 'relative', width: size, height: size, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
      {/* Soft circular halo — single subtle ring */}
      <div style={{
        position: 'absolute', width: size * 0.92, height: size * 0.92, borderRadius: '50%',
        background: `radial-gradient(circle, rgba(243,121,32,${haloOpacity}) 0%, rgba(243,121,32,0) 62%)`,
        transform: `scale(${haloScale})`,
        transition: 'transform 80ms linear',
      }}/>
      {/* Faint outline ring */}
      <div style={{
        position: 'absolute', width: size * 0.78, height: size * 0.78, borderRadius: '50%',
        border: `1px solid rgba(243,121,32,${0.18 * intensity + breath * 0.05})`,
      }}/>
      {/* Breathing core */}
      <div style={{
        width: coreSize, height: coreSize, borderRadius: '50%',
        background: 'radial-gradient(circle at 35% 30%, #FFD3A4 0%, #F37920 55%, #C95F0B 100%)',
        boxShadow: `0 0 ${18 + breath * 10 * intensity}px rgba(243,121,32,${0.28 * innerGlow})`,
        position: 'relative', overflow: 'hidden',
      }}>
        {/* Inner sheen */}
        <div style={{
          position: 'absolute', inset: 0, borderRadius: 50,
          background: `radial-gradient(circle at 35% 25%, rgba(255,255,255,${0.38 * innerGlow}) 0%, transparent 50%)`,
        }}/>
        {/* Phase icon */}
        {voicePhase && (
          <div key={voicePhase} style={{
            position: 'absolute', inset: 0,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            color: 'white', animation: 'payRise 280ms ease-out',
          }}>
            <Icon
              name={voicePhase === 'listening' ? 'mic' : voicePhase === 'thinking' ? 'spark' : 'speaker'}
              size={size * 0.28}
              strokeWidth={2.2}
              color="white"
            />
          </div>
        )}
        {/* Simi portrait (only when no phase icon) */}
        {!voicePhase && (<div style={{
          position: 'absolute', inset: '14%', borderRadius: 50,
          backgroundImage: "url('assets/simi.png')",
          backgroundSize: 'cover', backgroundPosition: '50% 22%',
          opacity: 0.92,
          mixBlendMode: 'luminosity',
        }}/>)}
      </div>
    </div>
  );
}

// (legacy placeholder kept for back-compat)
function _LegacyVoiceOrb({ active = true, size = 180 }) {
  return (
    <div style={{ position: 'relative', width: size, height: size, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
      <div style={{
        position: 'absolute', inset: 0, borderRadius: '50%',
        background: 'radial-gradient(circle at 50% 45%, rgba(243,168,92,0.45) 0%, rgba(243,121,32,0.0) 60%)',
        animation: active ? 'payOrb 3.2s ease-in-out infinite' : 'none',
      }}/>
      <div style={{
        width: size * 0.6, height: size * 0.6, borderRadius: '50%',
        background: 'radial-gradient(circle at 45% 35%, #FFC993 0%, #F37920 55%, #C95F0B 100%)',
        boxShadow: '0 0 60px rgba(243,121,32,0.5), inset 0 -20px 40px rgba(0,0,0,0.2)',
        animation: active ? 'payOrbInner 3.2s ease-in-out infinite' : 'none',
      }}/>
    </div>
  );
}

Object.assign(window, {
  PAY, payHero, payChatHero, payWarmScreen, paySafe,
  Icon, PayButton, PayChip, FlagChip, GlowOrb, SectionHeader,
  StatTile, CompassRings, PulseDot, Typewriter, VoiceOrb,
});
