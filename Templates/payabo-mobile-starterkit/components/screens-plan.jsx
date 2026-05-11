// Plan — the Compass surface. Four-layer guidance loop:
//   Position → Direction → Roadmap → Daily guidance.
// Visual signature: a compass-ring "north star" goal, a horizon timeline,
// and Simi-authored action cards with explicit "why this" reasoning.

function PlanScreen({ tweaks, onSimi, onTxn }) {
  const dark = tweaks.heroMode === 'dark';
  const heroBg = dark
    ? 'linear-gradient(180deg,#1B1816 0%, #131110 46%, #0B0A09 100%)'
    : 'linear-gradient(180deg,#2A1B14 0%, #1A1411 46%, #100B09 100%)';
  const [openAction, setOpenAction] = React.useState(0);
  const [horizon, setHorizon] = React.useState('week'); // week | month | quarter

  return (
    <div style={{ background: '#F8F0E4', height: '100%', overflow: 'auto', color: PAY.ink }}>
      {/* ── Hero: POSITION ─────────────────────────────────────────────── */}
      <div style={{
        background: heroBg, color: 'white', position: 'relative', overflow: 'hidden',
        padding: '14px 20px 24px',
      }}>
        <GlowOrb size={260} top={-60} right={-90} opacity={0.32}/>
        <GlowOrb size={180} top={120} left={-80} color="#D7A14E" opacity={0.18} blur={70}/>

        {/* mini-header */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '8px 0 18px', position: 'relative', zIndex: 2 }}>
          <div style={{ flex: 1 }}>
            <div style={{ font: `400 10px/14px ${PAY.font}`, color: 'rgba(255,255,255,0.5)', letterSpacing: 1.4, textTransform: 'uppercase' }}>Compass</div>
            <div style={{ font: `700 22px/26px ${PAY.font}`, letterSpacing: -0.3, marginTop: 2 }}>Plan</div>
          </div>
          <div onClick={onSimi} style={{
            display: 'flex', alignItems: 'center', gap: 8, padding: '6px 10px 6px 6px',
            borderRadius: 50, background: 'rgba(255,255,255,0.08)',
            border: '1px solid rgba(255,255,255,0.12)', cursor: 'pointer',
          }}>
            <div style={{
              width: 26, height: 26, borderRadius: 50,
              backgroundImage: "url('assets/simi.png')",
              backgroundSize: 'cover', backgroundPosition: '50% 25%',
              border: '1.5px solid rgba(243,121,32,0.5)',
            }}/>
            <span style={{ font: `600 11px/14px ${PAY.font}`, letterSpacing: 0.3 }}>Ask Simi</span>
          </div>
        </div>

        {/* Direction reading */}
        <div style={{ position: 'relative', zIndex: 2, display: 'flex', alignItems: 'flex-end', gap: 16 }}>
          <CompassDial heading={62} status="on course"/>
          <div style={{ flex: 1, paddingBottom: 8 }}>
            <div style={{ font: `400 10px/14px ${PAY.font}`, color: 'rgba(255,255,255,0.5)', letterSpacing: 1.2, textTransform: 'uppercase' }}>This month · May</div>
            <div style={{ font: `700 26px/30px ${PAY.font}`, color: 'white', letterSpacing: -0.4, marginTop: 6, textWrap: 'pretty' }}>
              You're <span style={{ color: PAY.orangeSoft }}>on course</span> for your June goal.
            </div>
            <div style={{ font: `400 12px/17px ${PAY.font}`, color: 'rgba(255,255,255,0.65)', marginTop: 6 }}>
              Tracking 4 days ahead · £215 buffer above your floor.
            </div>
          </div>
        </div>

        {/* Position metrics — four pillars */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 8, marginTop: 18, position: 'relative', zIndex: 2 }}>
          <PositionPillar label="Safe" value="£1,840" trend="↑"/>
          <PositionPillar label="Reserve" value="£3,200" trend="↑"/>
          <PositionPillar label="Owed" value="£480" trend="↓"/>
          <PositionPillar label="Goal" value="68%" trend="↑" accent/>
        </div>
      </div>

      {/* sheet pull */}
      <div style={{ marginTop: -14, padding: '0 0 16px', background: '#F8F0E4', borderRadius: '24px 24px 0 0', position: 'relative', zIndex: 3 }}>
        <div style={{ width: 44, height: 4, borderRadius: 4, background: '#DCCDB7', margin: '12px auto 4px' }}/>

        {/* ── DIRECTION: North-star goal ───────────────────────────────── */}
        <div style={{ padding: '14px 16px 0' }}>
          <SectionHeader kicker="Direction" title="Your north star" action="Adjust"/>
          <NorthStarGoal/>
        </div>

        {/* ── Secondary goals row ──────────────────────────────────────── */}
        <div style={{ padding: '14px 16px 0' }}>
          <div style={{ display: 'flex', gap: 10, overflowX: 'auto', paddingBottom: 4, margin: '0 -4px', padding: '0 4px 6px' }}>
            <MiniGoal title="Family support" value="£400/mo" pct={92} tone="warm" sub="On track"/>
            <MiniGoal title="Mum's birthday" value="£250 by Jul" pct={48} tone="ok" sub="6 weeks"/>
            <MiniGoal title="Clear card" value="£480" pct={32} tone="alert" sub="Behind"/>
            <MiniGoal title="+ Add goal" pct={0} tone="add"/>
          </div>
        </div>

        {/* ── ROADMAP: horizon timeline ────────────────────────────────── */}
        <div style={{ padding: '14px 16px 0' }}>
          <SectionHeader kicker="Roadmap" title="Path forward"
            action={
              <div style={{ display: 'inline-flex', gap: 4, padding: 2, borderRadius: 50, background: '#EFE2CD' }}>
                {['week','month','quarter'].map(h => (
                  <div key={h} onClick={() => setHorizon(h)} style={{
                    padding: '4px 10px', borderRadius: 50,
                    background: horizon === h ? PAY.warm900 : 'transparent',
                    color: horizon === h ? 'white' : PAY.warm800,
                    font: `700 9px/12px ${PAY.font}`, letterSpacing: 0.6, textTransform: 'uppercase',
                    cursor: 'pointer', transition: 'all 160ms',
                  }}>{h}</div>
                ))}
              </div>
            }/>
          <HorizonTimeline horizon={horizon}/>
        </div>

        {/* ── DAILY GUIDANCE: Simi's calls ─────────────────────────────── */}
        <div style={{ padding: '18px 16px 0' }}>
          <SectionHeader kicker="Today" title="Simi's calls" action="3 new"/>

          <ActionProposal
            open={openAction === 0}
            onToggle={() => setOpenAction(openAction === 0 ? -1 : 0)}
            kind="opportunity"
            title="Move £120 to Mum's birthday pot"
            whenLabel="Pay day · Friday"
            why="You're £215 ahead of your floor this month. Locking £120 now puts the gift goal back on track without touching your safe-to-spend."
            impact={[
              { label: 'Safe to spend', from: '£1,840', to: '£1,720', dir: 'down' },
              { label: 'Mum\u2019s birthday', from: '48%', to: '96%', dir: 'up' },
            ]}
            requiresApproval/>

          <ActionProposal
            open={openAction === 1}
            onToggle={() => setOpenAction(openAction === 1 ? -1 : 1)}
            kind="watch"
            title="Sky Broadband renews £42.99 on Mon"
            whenLabel="In 3 days"
            why="You used the line under an hour a day last month. There's a £24/mo plan with the same speed cap — worth a look before it auto-renews."
            actionLabel="Review plan"
            secondaryLabel="Keep current"/>

          <ActionProposal
            open={openAction === 2}
            onToggle={() => setOpenAction(openAction === 2 ? -1 : 2)}
            kind="recover"
            title="Card balance crept up £85 this week"
            whenLabel="Plan adjust"
            why="Two unplanned dinners pushed the clear-card goal behind pace. Shifting £40 from next month's flex room keeps the May 31 date intact."
            actionLabel="Apply adjustment"
            secondaryLabel="Not now"/>
        </div>

        {/* ── Insight strip ────────────────────────────────────────────── */}
        <div style={{ padding: '18px 16px 0' }}>
          <SectionHeader kicker="Signals" title="What changed"/>
          <SignalRow tone="up" title="Income landed 2 days early"
            sub="HMRC refund £142.40 cleared this morning."/>
          <SignalRow tone="flat" title="Spend pattern matches April"
            sub="Groceries & transport on rhythm — no surprises."/>
          <SignalRow tone="down" title="Eating out up 38% vs your usual"
            sub="£86 over 4 nights · mostly Friday + Saturday."/>
        </div>

        <div style={{ height: 24 }}/>
      </div>
    </div>
  );
}

// ── COMPASS DIAL ───────────────────────────────────────────────────────────
// The signature visual: a real compass that points at the user's current heading.
function CompassDial({ heading = 62, status = 'on course' }) {
  // ring of dots — emphasize the "needle" arc
  const dots = Array.from({ length: 40 }, (_, i) => i);
  return (
    <div style={{ position: 'relative', width: 124, height: 124 }}>
      <CompassRings size={124} count={3} opacity={0.18}/>
      {/* tick ring */}
      <svg viewBox="0 0 124 124" style={{ position: 'absolute', inset: 0 }}>
        {dots.map(i => {
          const a = (i / dots.length) * Math.PI * 2 - Math.PI / 2;
          const r1 = 54, r2 = i % 5 === 0 ? 48 : 51;
          const x1 = 62 + Math.cos(a) * r1, y1 = 62 + Math.sin(a) * r1;
          const x2 = 62 + Math.cos(a) * r2, y2 = 62 + Math.sin(a) * r2;
          return <line key={i} x1={x1} y1={y1} x2={x2} y2={y2}
            stroke={i % 5 === 0 ? 'rgba(255,255,255,0.6)' : 'rgba(255,255,255,0.22)'}
            strokeWidth={i % 5 === 0 ? 1.4 : 1} strokeLinecap="round"/>;
        })}
        {/* heading arc — orange */}
        <circle cx="62" cy="62" r="44" fill="none"
          stroke="rgba(243,121,32,0.25)" strokeWidth="2"
          strokeDasharray={`${(heading/100) * 276} 1000`}
          transform="rotate(-90 62 62)"/>
        <circle cx="62" cy="62" r="44" fill="none"
          stroke="#F37920" strokeWidth="2"
          strokeDasharray={`${(heading/100) * 276} 1000`}
          strokeLinecap="round"
          transform="rotate(-90 62 62)"/>
      </svg>
      {/* needle */}
      <div style={{
        position: 'absolute', inset: 0, display: 'flex',
        alignItems: 'center', justifyContent: 'center',
        transform: `rotate(${(heading/100) * 360 - 20}deg)`,
        transition: 'transform 800ms cubic-bezier(.2,.8,.2,1)',
      }}>
        <svg width="124" height="124" viewBox="0 0 124 124">
          <polygon points="62,20 58,62 66,62" fill="#F37920"/>
          <polygon points="62,104 58,62 66,62" fill="rgba(255,255,255,0.35)"/>
        </svg>
      </div>
      {/* center dial */}
      <div style={{
        position: 'absolute', top: '50%', left: '50%',
        transform: 'translate(-50%,-50%)',
        width: 56, height: 56, borderRadius: 50,
        background: 'radial-gradient(circle at 50% 35%, rgba(255,255,255,0.12) 0%, rgba(0,0,0,0.35) 80%)',
        border: '1px solid rgba(255,255,255,0.18)',
        display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
      }}>
        <div style={{ font: `700 18px/20px ${PAY.font}`, color: 'white', letterSpacing: -0.3 }}>{heading}°</div>
        <div style={{ font: `600 7px/10px ${PAY.font}`, color: PAY.orangeSoft, letterSpacing: 1, textTransform: 'uppercase' }}>{status}</div>
      </div>
    </div>
  );
}

// ── POSITION PILLAR ────────────────────────────────────────────────────────
function PositionPillar({ label, value, trend, accent }) {
  return (
    <div style={{
      padding: '10px 8px', borderRadius: 12,
      background: accent ? 'rgba(243,121,32,0.14)' : 'rgba(255,255,255,0.04)',
      border: `1px solid ${accent ? 'rgba(243,121,32,0.35)' : 'rgba(255,255,255,0.08)'}`,
      textAlign: 'left',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ font: `500 9px/12px ${PAY.font}`, color: accent ? PAY.orangeSoft : 'rgba(255,255,255,0.6)', letterSpacing: 0.8, textTransform: 'uppercase' }}>{label}</div>
        <div style={{ font: `700 10px/12px ${PAY.font}`, color: trend === '↑' ? '#7CE0A0' : '#E07C8E' }}>{trend}</div>
      </div>
      <div style={{ font: `700 15px/18px ${PAY.font}`, color: 'white', marginTop: 4, letterSpacing: -0.2 }}>{value}</div>
    </div>
  );
}

// ── NORTH STAR GOAL ────────────────────────────────────────────────────────
function NorthStarGoal() {
  const pct = 68;
  return (
    <div style={{
      borderRadius: 24, padding: 18, marginTop: 10,
      background: 'linear-gradient(135deg, #FFFFFF 0%, #FFF6EA 100%)',
      border: '1px solid #F1DEC9',
      boxShadow: '0 6px 18px rgba(243,121,32,0.08), 0 1px 0 rgba(255,255,255,0.8) inset',
      display: 'flex', gap: 16, alignItems: 'center',
    }}>
      <RadialGoal pct={pct} size={88}/>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <Icon name="star" size={12} color="#F37920"/>
          <div style={{ font: `700 10px/14px ${PAY.font}`, color: '#7A3211', letterSpacing: 1, textTransform: 'uppercase' }}>3-month buffer</div>
        </div>
        <div style={{ font: `700 18px/22px ${PAY.font}`, color: PAY.warm900, marginTop: 4, letterSpacing: -0.3 }}>
          £4,800 emergency fund
        </div>
        <div style={{ font: `400 11px/15px ${PAY.font}`, color: PAY.warm800, marginTop: 4 }}>
          <strong style={{ color: PAY.warm900 }}>£3,264 saved</strong> · est. ready by Aug 14
        </div>
        <div style={{ display: 'flex', gap: 6, marginTop: 10, flexWrap: 'wrap' }}>
          <Pill>+£135/wk</Pill>
          <Pill tone="up">4 wks ahead</Pill>
        </div>
      </div>
    </div>
  );
}

function RadialGoal({ pct = 0, size = 88 }) {
  const r = size / 2 - 8, c = 2 * Math.PI * r;
  return (
    <div style={{ position: 'relative', width: size, height: size, flex: 'none' }}>
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
        <circle cx={size/2} cy={size/2} r={r} fill="none" stroke="#F4E2C8" strokeWidth="6"/>
        <circle cx={size/2} cy={size/2} r={r} fill="none" stroke="url(#nsGrad)" strokeWidth="6"
          strokeDasharray={`${(pct/100)*c} ${c}`}
          strokeLinecap="round"
          transform={`rotate(-90 ${size/2} ${size/2})`}/>
        <defs>
          <linearGradient id="nsGrad" x1="0" y1="0" x2="1" y2="1">
            <stop offset="0%" stopColor="#F37920"/>
            <stop offset="100%" stopColor="#F3A85C"/>
          </linearGradient>
        </defs>
      </svg>
      <div style={{
        position: 'absolute', inset: 0,
        display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
      }}>
        <div style={{ font: `800 22px/24px ${PAY.font}`, color: PAY.warm900, letterSpacing: -0.6 }}>{pct}%</div>
        <div style={{ font: `600 8px/10px ${PAY.font}`, color: PAY.warm800, letterSpacing: 0.8, textTransform: 'uppercase' }}>of goal</div>
      </div>
    </div>
  );
}

function Pill({ children, tone = 'neutral' }) {
  const tones = {
    neutral: { bg: '#F5E7CF', fg: '#7A3211' },
    up: { bg: '#DBF3E3', fg: '#1B7030' },
  }[tone];
  return <span style={{
    padding: '3px 8px', borderRadius: 50, background: tones.bg, color: tones.fg,
    font: `700 9px/12px ${PAY.font}`, letterSpacing: 0.6, textTransform: 'uppercase',
  }}>{children}</span>;
}

// ── MINI GOAL ──────────────────────────────────────────────────────────────
function MiniGoal({ title, value, pct = 0, tone = 'warm', sub }) {
  if (tone === 'add') {
    return (
      <div style={{
        flex: 'none', width: 140, padding: 12, borderRadius: 16,
        border: '1.5px dashed #DCCDB7', background: 'transparent',
        display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
        gap: 6, color: PAY.warm800, cursor: 'pointer',
      }}>
        <div style={{ width: 28, height: 28, borderRadius: 50, background: '#EFE2CD', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
          <Icon name="add" size={14}/>
        </div>
        <div style={{ font: `600 11px/14px ${PAY.font}`, color: PAY.warm800 }}>{title}</div>
      </div>
    );
  }
  const toneMap = {
    warm: { ring: '#F37920', fill: '#FFF6EA', border: '#F1DEC9', label: '#7A3211' },
    ok:   { ring: '#4ACB64', fill: '#F0FBF3', border: '#CDE9D5', label: '#1B7030' },
    alert:{ ring: '#E55B6A', fill: '#FDF1F2', border: '#F2D6D9', label: '#8A0022' },
  }[tone];
  return (
    <div style={{
      flex: 'none', width: 152, padding: 12, borderRadius: 16,
      background: toneMap.fill, border: `1px solid ${toneMap.border}`,
      display: 'flex', flexDirection: 'column', gap: 8,
    }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ font: `700 11px/14px ${PAY.font}`, color: PAY.ink, letterSpacing: -0.1 }}>{title}</div>
        <div style={{ font: `700 11px/14px ${PAY.font}`, color: toneMap.ring }}>{pct}%</div>
      </div>
      <div style={{ height: 5, borderRadius: 3, background: 'rgba(0,0,0,0.06)', overflow: 'hidden' }}>
        <div style={{ width: `${pct}%`, height: '100%', background: toneMap.ring, borderRadius: 3 }}/>
      </div>
      <div style={{ font: `700 13px/16px ${PAY.font}`, color: PAY.warm900, letterSpacing: -0.2 }}>{value}</div>
      <div style={{ font: `500 10px/13px ${PAY.font}`, color: toneMap.label, letterSpacing: 0.3, textTransform: 'uppercase' }}>{sub}</div>
    </div>
  );
}

// ── HORIZON TIMELINE ───────────────────────────────────────────────────────
// A horizontal "path forward" with milestones. The line bends to reflect
// pressure (income in, bills out). Today is anchored to the left.
function HorizonTimeline({ horizon }) {
  const data = {
    week: [
      { day: 'Mon', label: 'Today', y: 0.55, kind: 'now' },
      { day: 'Tue', y: 0.5 },
      { day: 'Wed', label: 'Card £85', y: 0.65, kind: 'out', amount: '-£85' },
      { day: 'Thu', y: 0.6 },
      { day: 'Fri', label: 'Pay day', y: 0.3, kind: 'in', amount: '+£2,410' },
      { day: 'Sat', label: 'Send Mum', y: 0.42, kind: 'plan', amount: '-£100' },
      { day: 'Sun', label: 'On rhythm', y: 0.4, kind: 'mark' },
    ],
    month: [
      { day: 'Wk 1', y: 0.5, kind: 'now', label: 'Now' },
      { day: 'Wk 2', y: 0.35, label: 'Pay day', kind: 'in' },
      { day: 'Wk 3', y: 0.55, label: 'Bills due', kind: 'out' },
      { day: 'Wk 4', y: 0.42, label: 'Goal hit', kind: 'mark' },
      { day: 'Wk 5', y: 0.3, label: 'Buffer', kind: 'in' },
    ],
    quarter: [
      { day: 'May', y: 0.5, kind: 'now', label: 'Now' },
      { day: 'Jun', y: 0.4, label: 'Card cleared', kind: 'mark' },
      { day: 'Jul', y: 0.32, label: 'Mum gift', kind: 'plan' },
      { day: 'Aug', y: 0.22, label: 'Fund ready', kind: 'mark', highlight: true },
      { day: 'Sep', y: 0.18, label: 'Next phase', kind: 'in' },
    ],
  }[horizon];

  const W = 340, H = 150;
  const pts = data.map((d, i) => ({ x: (i / (data.length - 1)) * (W - 24) + 12, y: d.y * (H - 30) + 18, ...d }));
  const pathD = pts.map((p, i) => i === 0 ? `M${p.x} ${p.y}` : `S${(pts[i-1].x + p.x)/2} ${p.y}, ${p.x} ${p.y}`).join(' ');
  const fillD = `${pathD} L${pts[pts.length-1].x} ${H} L${pts[0].x} ${H} Z`;

  return (
    <div style={{
      borderRadius: 20, padding: 14, marginTop: 10, background: '#1A1411',
      border: '1px solid rgba(255,255,255,0.06)', position: 'relative', overflow: 'hidden',
    }}>
      <GlowOrb size={160} top={-40} right={-40} opacity={0.18}/>
      <div style={{ position: 'relative', zIndex: 1 }}>
        <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', marginBottom: 4 }}>
          <div style={{ font: `400 10px/14px ${PAY.font}`, color: 'rgba(255,255,255,0.5)', letterSpacing: 1, textTransform: 'uppercase' }}>Cash horizon</div>
          <div style={{ font: `600 10px/14px ${PAY.font}`, color: PAY.orangeSoft }}>
            <span style={{ display: 'inline-block', width: 6, height: 6, borderRadius: 50, background: '#7CE0A0', marginRight: 5 }}/>
            healthy buffer
          </div>
        </div>
        <svg viewBox={`0 0 ${W} ${H}`} preserveAspectRatio="none" style={{ width: '100%', height: 130, display: 'block' }}>
          <defs>
            <linearGradient id="horizonFill" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="#F37920" stopOpacity="0.35"/>
              <stop offset="100%" stopColor="#F37920" stopOpacity="0"/>
            </linearGradient>
          </defs>
          <path d={fillD} fill="url(#horizonFill)"/>
          <path d={pathD} fill="none" stroke="#F37920" strokeWidth="2" strokeLinecap="round"/>
          {/* grid line for "floor" */}
          <line x1="0" y1={H - 28} x2={W} y2={H - 28} stroke="rgba(255,255,255,0.08)" strokeDasharray="3 4"/>
          <text x="6" y={H - 32} fill="rgba(255,255,255,0.35)" style={{ font: `500 8px/10px ${PAY.font}`, letterSpacing: 0.6, textTransform: 'uppercase' }}>floor</text>
          {/* points */}
          {pts.map((p, i) => {
            const isNow = p.kind === 'now';
            const color = p.kind === 'in' ? '#7CE0A0' : p.kind === 'out' ? '#E07C8E' : p.highlight ? '#F37920' : 'white';
            return (
              <g key={i}>
                {isNow && <circle cx={p.x} cy={p.y} r="10" fill="rgba(243,121,32,0.25)"/>}
                <circle cx={p.x} cy={p.y} r={isNow ? 5 : 4} fill={color} stroke={isNow ? '#F37920' : 'rgba(0,0,0,0.2)'} strokeWidth={isNow ? 2 : 0}/>
                {p.label && <text x={p.x} y={p.y - 10} textAnchor="middle"
                  fill={p.highlight ? '#F37920' : 'rgba(255,255,255,0.85)'}
                  style={{ font: `600 8px/10px ${PAY.font}` }}>{p.label}</text>}
                {p.amount && <text x={p.x} y={p.y - 20} textAnchor="middle"
                  fill={color}
                  style={{ font: `700 8px/10px ${PAY.font}` }}>{p.amount}</text>}
              </g>
            );
          })}
        </svg>
        <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 4, padding: '0 4px' }}>
          {pts.map((p, i) => (
            <div key={i} style={{ font: `600 9px/12px ${PAY.font}`, color: p.kind === 'now' ? PAY.orangeSoft : 'rgba(255,255,255,0.5)', letterSpacing: 0.4, textTransform: 'uppercase' }}>{p.day}</div>
          ))}
        </div>
      </div>
    </div>
  );
}

// ── ACTION PROPOSAL ────────────────────────────────────────────────────────
// Compass calls. Always include: a clear next step, why this, and impact.
function ActionProposal({ open, onToggle, kind, title, whenLabel, why, impact, actionLabel, secondaryLabel, requiresApproval }) {
  const kinds = {
    opportunity: { bg: '#FFF6EA', bd: '#F1DEC9', accent: '#F37920', icon: 'star', label: 'Opportunity', labelBg: 'rgba(243,121,32,0.14)', labelFg: '#7A3211' },
    watch:       { bg: 'white',   bd: '#F1E5D1', accent: '#1E4AB5', icon: 'bell', label: 'Watch',       labelBg: '#E8F0FF', labelFg: '#1E4AB5' },
    recover:     { bg: '#FDF6E8', bd: '#F1DEC9', accent: '#B05B12', icon: 'shield', label: 'Recover',   labelBg: '#FFEBE3', labelFg: '#8A2A0F' },
  }[kind];
  return (
    <div style={{
      background: kinds.bg, border: `1px solid ${kinds.bd}`,
      borderRadius: 16, marginBottom: 10, overflow: 'hidden',
      transition: 'box-shadow 200ms',
      boxShadow: open ? '0 8px 22px rgba(77,49,32,0.10)' : '0 2px 8px rgba(77,49,32,0.04)',
    }}>
      <div onClick={onToggle} style={{ padding: 14, display: 'flex', alignItems: 'flex-start', gap: 12, cursor: 'pointer' }}>
        <div style={{
          width: 36, height: 36, borderRadius: 12, flex: 'none',
          background: kinds.labelBg, color: kinds.labelFg,
          display: 'flex', alignItems: 'center', justifyContent: 'center',
        }}><Icon name={kinds.icon} size={18}/></div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <span style={{
              font: `700 9px/12px ${PAY.font}`, color: kinds.labelFg,
              padding: '2px 6px', borderRadius: 50, background: kinds.labelBg,
              letterSpacing: 0.6, textTransform: 'uppercase',
            }}>{kinds.label}</span>
            <span style={{ font: `500 10px/12px ${PAY.font}`, color: PAY.warm800 }}>· {whenLabel}</span>
          </div>
          <div style={{ font: `700 14px/19px ${PAY.font}`, color: PAY.ink, marginTop: 4, textWrap: 'pretty' }}>{title}</div>
        </div>
        <div style={{
          width: 24, height: 24, borderRadius: 50, background: 'rgba(0,0,0,0.04)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          color: PAY.warm800, transform: open ? 'rotate(180deg)' : 'none',
          transition: 'transform 200ms',
        }}><Icon name="chevDown" size={14}/></div>
      </div>

      {open && (
        <div style={{ padding: '0 14px 14px', animation: 'payRise 240ms ease-out' }}>
          {/* Why this */}
          <div style={{
            padding: 12, borderRadius: 12, background: 'rgba(255,255,255,0.6)',
            border: `1px dashed ${kinds.bd}`,
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 4 }}>
              <div style={{
                width: 18, height: 18, borderRadius: 50,
                backgroundImage: "url('assets/simi.png')",
                backgroundSize: 'cover', backgroundPosition: '50% 25%',
              }}/>
              <span style={{ font: `700 9px/12px ${PAY.font}`, color: kinds.accent, letterSpacing: 0.6, textTransform: 'uppercase' }}>Why this</span>
            </div>
            <div style={{ font: `400 12px/17px ${PAY.font}`, color: PAY.ink }}>{why}</div>
          </div>

          {/* Impact */}
          {impact && (
            <div style={{ marginTop: 10, padding: 12, borderRadius: 12, background: 'white', border: `1px solid ${kinds.bd}` }}>
              <div style={{ font: `700 9px/12px ${PAY.font}`, color: PAY.warm800, letterSpacing: 0.6, textTransform: 'uppercase', marginBottom: 8 }}>If approved</div>
              {impact.map((row, i) => (
                <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '6px 0', borderTop: i > 0 ? `1px solid #F5EADB` : 'none' }}>
                  <div style={{ flex: 1, font: `500 12px/15px ${PAY.font}`, color: PAY.warm900 }}>{row.label}</div>
                  <div style={{ font: `600 12px/15px ${PAY.font}`, color: PAY.warm800 }}>{row.from}</div>
                  <div style={{ color: row.dir === 'up' ? '#1B7030' : '#8A2A0F' }}>{row.dir === 'up' ? '↗' : '↘'}</div>
                  <div style={{ font: `700 13px/16px ${PAY.font}`, color: row.dir === 'up' ? '#1B7030' : PAY.ink }}>{row.to}</div>
                </div>
              ))}
            </div>
          )}

          {/* Actions */}
          <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
            <PayButton variant="primary" size="md" style={{ flex: 1 }}>
              {requiresApproval ? 'Approve' : (actionLabel || 'Apply')}
            </PayButton>
            <PayButton variant="secondary" size="md" style={{ flex: 1 }}>
              {secondaryLabel || 'Not now'}
            </PayButton>
          </div>
        </div>
      )}
    </div>
  );
}

// ── SIGNAL ROW ─────────────────────────────────────────────────────────────
function SignalRow({ tone, title, sub }) {
  const tones = {
    up:   { fg: '#1B7030', bg: '#DBF3E3', arrow: '↗' },
    flat: { fg: '#7A3211', bg: '#F5E7CF', arrow: '→' },
    down: { fg: '#8A2A0F', bg: '#FFE3DC', arrow: '↘' },
  }[tone];
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '10px 0', borderTop: '1px solid #EFE2CD' }}>
      <div style={{
        width: 32, height: 32, borderRadius: 50, background: tones.bg, color: tones.fg,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        font: `700 14px/18px ${PAY.font}`,
      }}>{tones.arrow}</div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ font: `600 13px/17px ${PAY.font}`, color: PAY.ink }}>{title}</div>
        <div style={{ font: `400 11px/15px ${PAY.font}`, color: PAY.warm800, marginTop: 1 }}>{sub}</div>
      </div>
    </div>
  );
}

Object.assign(window, { PlanScreen });
