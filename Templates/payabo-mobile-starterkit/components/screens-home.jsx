// Payabo home & navigation chrome.

function PayHeader({ dark = true, name = 'Kwame', onBell, onProfile, bellCount = 3 }) {
  const fg = dark ? 'white' : PAY.warm900;
  const sub = dark ? 'rgba(255,255,255,0.55)' : PAY.warm800;
  const icBg = dark ? 'rgba(255,255,255,0.06)' : PAY.warm050;
  const icBd = dark ? 'rgba(255,255,255,0.08)' : '#F1E5D1';
  return (
    <div style={{ padding: '8px 20px 0', display: 'flex', alignItems: 'center', gap: 12, position: 'relative', zIndex: 2 }}>
      <div onClick={onProfile} style={{
        width: 40, height: 40, borderRadius: 50, cursor: 'pointer',
        backgroundImage: "url('assets/demo_profile.jpg')", backgroundSize: 'cover', backgroundPosition: 'center',
        border: `1.5px solid rgba(243,121,32,0.6)`, flex: 'none',
      }}/>
      <div style={{ flex: 1 }}>
        <div style={{ font: `500 10px/12px ${PAY.font}`, color: sub, letterSpacing: 0.4, textTransform: 'uppercase' }}>Welcome back</div>
        <div style={{ font: `700 14px/18px ${PAY.font}`, color: fg, marginTop: 2 }}>{name} Mensah</div>
      </div>
      <FlagChip cc="gb" label="GBP" dark={dark}/>
      <div onClick={onBell} style={{
        width: 40, height: 40, borderRadius: 50, background: icBg,
        border: `1px solid ${icBd}`, display: 'flex', alignItems: 'center', justifyContent: 'center',
        position: 'relative', color: fg, cursor: 'pointer', flex: 'none',
      }}>
        <Icon name="bell" size={18}/>
        {bellCount > 0 && <div style={{
          position: 'absolute', top: -3, right: -3,
          minWidth: 18, height: 18, padding: '0 5px', borderRadius: 50,
          background: PAY.orange, color: 'white', font: `700 10px/18px ${PAY.font}`,
          textAlign: 'center', border: `2px solid ${dark ? PAY.heroTop : PAY.warm050}`,
        }}>{bellCount}</div>}
      </div>
    </div>
  );
}

// Simi badge — floating mini-orb that opens chat
function SimiBadge({ onClick, presence = 'copilot' }) {
  if (presence === 'hidden') return null;
  return (
    <div onClick={onClick} style={{
      position: 'absolute', right: 16, bottom: 100, zIndex: 30,
      width: 56, height: 56, borderRadius: 50, cursor: 'pointer',
      background: 'radial-gradient(circle at 35% 30%, #FFD3A4 0%, #F37920 55%, #C95F0B 100%)',
      boxShadow: '0 8px 24px rgba(243,121,32,0.45), 0 0 0 4px rgba(255,255,255,0.9)',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      animation: 'payFloat 4s ease-in-out infinite',
    }}>
      <div style={{
        position: 'absolute', inset: -8, borderRadius: 50,
        background: 'radial-gradient(circle, rgba(243,121,32,0.3) 0%, transparent 70%)',
        animation: 'payPulse 2.4s ease-in-out infinite',
      }}/>
      <div style={{
        width: 36, height: 36, borderRadius: 50,
        backgroundImage: "url('assets/simi.png')",
        backgroundSize: 'cover', backgroundPosition: '50% 25%',
        border: '2px solid white', position: 'relative', zIndex: 1,
      }}/>
    </div>
  );
}

// ── Bottom nav ─────────────────────────────────────────────────────────────
function PayBottomNav({ current = 'home', onChange, onFabClick }) {
  const items = [
    { id: 'home', icon: 'home', label: 'Home' },
    { id: 'plan', icon: 'compass', label: 'Plan' },
    { id: 'spending', icon: 'spending', label: 'Spend' },
    { id: 'chat', icon: 'chat', label: 'Simi' },
  ];
  return <div style={{
    position: 'relative', background: 'white', borderTop: `1px solid ${PAY.navBorder}`,
    height: 74, display: 'flex', alignItems: 'center', justifyContent: 'space-around',
    boxShadow: '0 -1px 10px rgba(0,0,0,0.07)', flex: 'none', zIndex: 20,
  }}>
    {items.slice(0, 2).map(it => <NavItem key={it.id} {...it} current={current} onChange={onChange}/>)}
    <div style={{ width: 72 }}/>
    {items.slice(2).map(it => <NavItem key={it.id} {...it} current={current} onChange={onChange}/>)}
    <div onClick={onFabClick} style={{
      position: 'absolute', top: -18, left: '50%', transform: 'translateX(-50%)',
      width: 58, height: 58, borderRadius: 50, background: PAY.orange, color: 'white',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      boxShadow: '0 4px 12px rgba(243,121,32,0.45)', border: '4px solid white', cursor: 'pointer',
      transition: 'transform 200ms',
    }}
    onMouseDown={e => e.currentTarget.style.transform = 'translateX(-50%) scale(0.94)'}
    onMouseUp={e => e.currentTarget.style.transform = 'translateX(-50%) scale(1)'}
    onMouseLeave={e => e.currentTarget.style.transform = 'translateX(-50%) scale(1)'}
    ><Icon name="add" size={24} strokeWidth={2.6} color="white"/></div>
  </div>;
}
function NavItem({ id, icon, label, current, onChange }) {
  const on = current === id;
  return <div onClick={() => onChange && onChange(id)} style={{
    display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4, width: 60, cursor: 'pointer',
    color: on ? PAY.orange : PAY.navUnselected,
    font: `${on ? 700 : 500} 10px/14px ${PAY.font}`, letterSpacing: 0.2,
    transition: 'color 160ms',
  }}>
    <div style={{ position: 'relative' }}>
      <Icon name={icon} size={22} strokeWidth={on ? 2.2 : 1.8}/>
      {on && <div style={{ position: 'absolute', bottom: -4, left: '50%', transform: 'translateX(-50%)', width: 4, height: 4, borderRadius: 50, background: PAY.orange }}/>}
    </div>
    {label}
  </div>;
}

// ── Home screen ────────────────────────────────────────────────────────────
function HomeScreen({ tweaks, onTxn, onSimi, onBell, onPay }) {
  if (tweaks.dataMode === 'fresh') return <HomeFreshScreen tweaks={tweaks} onBell={onBell} onSimi={onSimi}/>;
  const dark = tweaks.heroMode === 'dark';
  const heroBg = dark ? payHero : 'linear-gradient(180deg, #2A1B14 0%, #1A1411 46%, #100B09 100%)';
  const compact = tweaks.density === 'compact';
  const cardP = compact ? 14 : 18;
  const cardR = tweaks.heroLayout === 'expressive' ? 24 : 20;

  return (
    <div style={{ background: heroBg, height: '100%', position: 'relative', overflow: 'hidden', color: 'white' }}>
      {/* atmospheric orbs */}
      <GlowOrb size={300} top={-80} right={-100} opacity={dark ? 0.32 : 0.42} />
      <GlowOrb size={220} top={120} left={-80} color="#D7A14E" opacity={0.18} blur={70}/>
      <CompassRings size={420} color="rgba(243,121,32,0.08)"/>

      <PayHeader dark name="Kwame" onBell={onBell}/>

      {/* Greeting + story (expressive layout = big numerics; minimal = single paragraph) */}
      {tweaks.heroLayout === 'expressive' ? (
        <ExpressiveHero/>
      ) : tweaks.heroLayout === 'split' ? (
        <SplitHero/>
      ) : (
        <ClassicHero/>
      )}

      {/* warm sheet */}
      <div style={{
        position: 'absolute', left: 0, right: 0, bottom: 0,
        top: tweaks.heroLayout === 'expressive' ? '46%' : '40%',
        background: PAY.warm100, borderRadius: '28px 28px 0 0',
        boxShadow: '0 -8px 24px rgba(0,0,0,0.18)', overflow: 'auto',
        padding: '14px 16px 24px', color: PAY.ink,
      }}>
        <div style={{ width: 44, height: 4, borderRadius: 4, background: '#DCCDB7', margin: '0 auto 14px' }}/>

        {/* available to spend — with Simi nudge */}
        <div style={{
          background: 'white', borderRadius: cardR, padding: cardP,
          border: `1px solid #F1E5D1`, boxShadow: '0 2px 10px rgba(77,49,32,0.05)',
          marginBottom: 10, position: 'relative', overflow: 'hidden',
        }}>
          <div style={{
            position: 'absolute', right: -40, top: -40, width: 140, height: 140,
            borderRadius: '50%', background: 'radial-gradient(circle, rgba(243,121,32,0.12) 0%, transparent 70%)',
          }}/>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <div style={{ font: `600 11px/14px ${PAY.font}`, color: PAY.warm800, letterSpacing: 0.8, textTransform: 'uppercase' }}>Available to spend</div>
            <div style={{ flex: 1 }}/>
            <PayChip tone="success" style={{ fontSize: 10 }}>48% free</PayChip>
          </div>
          <div style={{ font: `700 36px/40px ${PAY.font}`, color: PAY.ink, marginTop: 8, letterSpacing: -0.8 }}>£2,184.<span style={{ opacity: 0.45, fontSize: 24 }}>60</span></div>
          <div style={{ font: `400 12px/17px ${PAY.font}`, color: PAY.warm800, marginTop: 4 }}>
            You still have room for planned spending this month.
          </div>
          <div style={{ height: 6, borderRadius: 3, background: '#F5EADB', marginTop: 14, overflow: 'hidden', display: 'flex' }}>
            <div style={{ width: '38%', height: '100%', background: PAY.orange }}/>
            <div style={{ width: '14%', height: '100%', background: PAY.orangeSoft }}/>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 6 }}>
            <div style={{ font: `400 10px/14px ${PAY.font}`, color: PAY.warm800 }}>£1,648 spent · £268 planned</div>
            <div style={{ font: `600 10px/14px ${PAY.font}`, color: PAY.warm900 }}>of £4,100</div>
          </div>
        </div>

        {/* Simi insight card */}
        {tweaks.simiPresence !== 'hidden' && (
          <div style={{
            borderRadius: cardR, padding: cardP,
            background: 'linear-gradient(135deg, #FFE2C5 0%, #FFF2E3 100%)',
            border: `1px solid #F1DEC9`, marginBottom: 10, position: 'relative', overflow: 'hidden',
          }}>
            <div style={{ position: 'absolute', right: -20, bottom: -20, width: 100, height: 100, borderRadius: '50%',
              background: 'radial-gradient(circle, rgba(243,121,32,0.3) 0%, transparent 70%)' }}/>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 8 }}>
              <div style={{
                width: 28, height: 28, borderRadius: 50,
                backgroundImage: "url('assets/simi.png')",
                backgroundSize: 'cover', backgroundPosition: '50% 25%',
                border: '1.5px solid rgba(243,121,32,0.6)',
              }}/>
              <div style={{ font: `700 10px/14px ${PAY.font}`, color: '#7A3211', letterSpacing: 1, textTransform: 'uppercase' }}>Simi · this morning</div>
              <div style={{ flex: 1 }}/>
              <PulseDot color="#7A3211" size={6}/>
            </div>
            <div style={{ font: `600 14px/20px ${PAY.font}`, color: PAY.warm900 }}>
              <Typewriter text="Sky broadband charges £42.99 on Thursday. Want me to keep £50 aside?" speed={18}/>
            </div>
            <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
              <PayButton variant="primary" size="sm" onClick={onSimi}>Yes, set aside</PayButton>
              <PayButton variant="link" size="sm">Not now</PayButton>
            </div>
          </div>
        )}

        {/* Family support — diaspora signature */}
        <SectionHeader kicker="Wherever they are" title="Family back home" action="Send"
          onAction={onPay}/>
        <div style={{ display: 'flex', gap: 10, marginBottom: 18 }}>
          <FamilyTile name="Ama" rel="Mum · Accra" cc="gh" amount="GHS 2,500" next="Sent Nov 1" onClick={onTxn}/>
          <FamilyTile name="Ebo" rel="Brother · Lagos" cc="ng" amount="NGN 95k" next="Due Nov 28"/>
        </div>

        {/* Upcoming bills */}
        <SectionHeader title="Upcoming bills" action="See all"/>
        <div style={{ background: 'white', borderRadius: 16, border: `1px solid #F1E5D1`, padding: '4px 0', marginBottom: 14 }}>
          <BillRow logoBg="#0B1A4A" logoFg="white" logoText="SKY" name="Sky Broadband" due="Due Thu · £42.99" tone="warning"/>
          <BillRow logoBg="#E60028" logoFg="white" logoText="V" name="Vodafone" due="Due Sat · £28.00" tone="warning"/>
          <BillRow logoBg="#FFD400" logoFg="#111" logoText="EE" name="EE Mobile" due="Dec 02 · £21.00" tone="info" divider={false}/>
        </div>

        {/* Net worth */}
        <SectionHeader title="Net worth" action="Details" kicker="Across linked accounts"/>
        <div style={{
          background: 'white', borderRadius: cardR, padding: cardP,
          border: `1px solid #F1E5D1`, boxShadow: '0 2px 10px rgba(77,49,32,0.05)',
          marginBottom: 14,
        }}>
          <div style={{ display: 'flex', alignItems: 'flex-end', gap: 12 }}>
            <div>
              <div style={{ font: `700 28px/32px ${PAY.font}`, color: PAY.ink, letterSpacing: -0.4 }}>£18,642.90</div>
              <div style={{ font: `600 11px/14px ${PAY.font}`, color: '#1B7030', marginTop: 4 }}>↑ +£412.80 this month</div>
            </div>
            <div style={{ flex: 1 }}/>
            <Sparkline/>
          </div>
          <div style={{ display: 'flex', gap: 8, marginTop: 14 }}>
            <StatTile tone="warm" label="Assets" value="£21,205"/>
            <StatTile tone="warm" label="Bills due" value="£2,562"/>
          </div>
        </div>

        {/* This-month rhythm */}
        <SectionHeader title="This month at a glance" kicker="Where the money moves"/>
        <div style={{
          background: 'linear-gradient(180deg,#FFFCF7 0%, #FFF6EB 100%)',
          borderRadius: cardR, padding: cardP, border: `1px solid #F1E5D1`, marginBottom: 8,
        }}>
          <RhythmStrip/>
        </div>

        <div style={{ height: 20 }}/>
      </div>
    </div>
  );
}

function ClassicHero() {
  return (
    <div style={{ padding: '32px 24px 28px', maxWidth: 360, position: 'relative', zIndex: 2 }}>
      <div style={{ font: `700 30px/34px ${PAY.font}`, color: 'white', letterSpacing: -0.4 }}>Good morning, Kwame.</div>
      <div style={{ font: `400 14px/22px ${PAY.font}`, color: 'rgba(255,255,255,0.85)', marginTop: 14 }}>
        You have <Emph>£2,184.60</Emph> available to spend, <Emph>3 bills</Emph> due this week, and <Emph>+£412.80</Emph> added to your net worth.
      </div>
    </div>
  );
}
function ExpressiveHero() {
  return (
    <div style={{ padding: '20px 24px 24px', position: 'relative', zIndex: 2 }}>
      <div style={{ font: `400 11px/14px ${PAY.font}`, color: 'rgba(255,255,255,0.5)', letterSpacing: 1.4, textTransform: 'uppercase' }}>Tuesday, May 11 · Good morning</div>
      <div style={{ font: `800 44px/46px ${PAY.font}`, color: 'white', letterSpacing: -1.2, marginTop: 8 }}>
        Hello,<br/><span style={{ color: PAY.orange }}>Kwame.</span>
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 14 }}>
        <PulseDot/>
        <div style={{ font: `500 12px/16px ${PAY.font}`, color: 'rgba(255,255,255,0.7)' }}>Simi is watching <Emph>3 bills</Emph> and <Emph>2 transfers</Emph> for you.</div>
      </div>
    </div>
  );
}
function SplitHero() {
  return (
    <div style={{ padding: '24px 24px 24px', position: 'relative', zIndex: 2, display: 'flex', alignItems: 'center', gap: 16 }}>
      <div style={{ flex: 1 }}>
        <div style={{ font: `400 11px/14px ${PAY.font}`, color: 'rgba(255,255,255,0.5)', letterSpacing: 1.2, textTransform: 'uppercase' }}>Good morning</div>
        <div style={{ font: `700 26px/30px ${PAY.font}`, color: 'white', letterSpacing: -0.4, marginTop: 6 }}>Kwame, you're on track.</div>
        <div style={{ font: `400 12px/17px ${PAY.font}`, color: 'rgba(255,255,255,0.7)', marginTop: 10 }}>£2,184.60 safe · 3 bills queued · net worth ↑ £412.80</div>
      </div>
      <div style={{
        width: 86, height: 86, borderRadius: 50, flex: 'none',
        background: `conic-gradient(${PAY.orange} 0 62%, rgba(255,255,255,0.12) 62% 100%)`,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
      }}>
        <div style={{ width: 66, height: 66, borderRadius: 50, background: PAY.heroTop, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center' }}>
          <div style={{ font: `700 18px/20px ${PAY.font}`, color: 'white' }}>62%</div>
          <div style={{ font: `400 9px/12px ${PAY.font}`, color: 'rgba(255,255,255,0.6)' }}>of month</div>
        </div>
      </div>
    </div>
  );
}
function Emph({ children }) { return <span style={{ color: PAY.orangeSoft, fontWeight: 700 }}>{children}</span>; }

function FamilyTile({ name, rel, cc, amount, next, onClick }) {
  return (
    <div onClick={onClick} style={{
      flex: 1, background: 'white', borderRadius: 16, padding: 12, cursor: 'pointer',
      border: '1px solid #F1E5D1', boxShadow: '0 2px 10px rgba(77,49,32,0.05)',
      position: 'relative', overflow: 'hidden',
    }}>
      <div style={{
        position: 'absolute', top: 10, right: 10, width: 22, height: 22, borderRadius: 50,
        backgroundImage: `url('assets/flags/${cc}.svg')`, backgroundSize: 'cover',
        boxShadow: '0 0 0 1.5px white',
      }}/>
      <div style={{
        width: 36, height: 36, borderRadius: 50, background: '#FFEFE3', color: '#7A3211',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        font: `700 13px/18px ${PAY.font}`,
      }}>{name.slice(0,2).toUpperCase()}</div>
      <div style={{ font: `700 13px/16px ${PAY.font}`, color: PAY.ink, marginTop: 10 }}>{name}</div>
      <div style={{ font: `400 10px/14px ${PAY.font}`, color: PAY.warm800 }}>{rel}</div>
      <div style={{ font: `700 13px/16px ${PAY.font}`, color: PAY.orange, marginTop: 8 }}>{amount}</div>
      <div style={{ font: `400 10px/14px ${PAY.font}`, color: PAY.n500, marginTop: 2 }}>{next}</div>
    </div>
  );
}

function BillRow({ logoBg, logoFg, logoText, name, due, tone, divider = true, onClick }) {
  return (
    <div onClick={onClick} style={{
      display: 'flex', alignItems: 'center', gap: 12, padding: '12px 16px',
      borderBottom: divider ? '1px solid #F5EADB' : 'none',
      cursor: onClick ? 'pointer' : 'default',
    }}>
      <div style={{
        width: 34, height: 34, borderRadius: 8, background: logoBg, color: logoFg,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        font: `700 10px/14px ${PAY.font}`,
      }}>{logoText}</div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ font: `600 13px/18px ${PAY.font}`, color: PAY.ink }}>{name}</div>
        <div style={{ font: `400 11px/16px ${PAY.font}`, color: PAY.n500 }}>{due}</div>
      </div>
      <Icon name="chev" size={16} color={PAY.warm800}/>
    </div>
  );
}

function Sparkline() {
  return (
    <svg width="120" height="40" viewBox="0 0 120 40">
      <defs>
        <linearGradient id="sparkFill" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor="#F37920" stopOpacity="0.25"/>
          <stop offset="100%" stopColor="#F37920" stopOpacity="0"/>
        </linearGradient>
      </defs>
      <path d="M0 30 L15 26 L30 28 L45 20 L60 22 L75 14 L90 18 L105 10 L120 6 L120 40 L0 40 Z" fill="url(#sparkFill)"/>
      <path d="M0 30 L15 26 L30 28 L45 20 L60 22 L75 14 L90 18 L105 10 L120 6" fill="none" stroke={PAY.orange} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
    </svg>
  );
}

// ── Fresh (empty) data state — DemoDataMode.fresh from app code ─────────
function HomeFreshScreen({ tweaks, onBell, onSimi }) {
  const dark = tweaks.heroMode === 'dark';
  const heroBg = dark ? payHero : 'linear-gradient(180deg, #2A1B14 0%, #1A1411 46%, #100B09 100%)';
  return (
    <div style={{ background: heroBg, height: '100%', position: 'relative', overflow: 'hidden', color: 'white' }}>
      <GlowOrb size={320} top={-80} right={-100} opacity={0.32}/>
      <GlowOrb size={240} top={140} left={-100} color="#D7A14E" opacity={0.18} blur={70}/>
      <CompassRings size={420} color="rgba(243,121,32,0.10)"/>

      <PayHeader dark name="Kwame" onBell={onBell} bellCount={0}/>

      <div style={{ padding: '20px 24px 18px', position: 'relative', zIndex: 2 }}>
        <div style={{ font: `400 11px/14px ${PAY.font}`, color: 'rgba(255,255,255,0.5)', letterSpacing: 1.4, textTransform: 'uppercase' }}>Welcome to Payabo</div>
        <div style={{ font: `800 32px/36px ${PAY.font}`, color: 'white', letterSpacing: -1, marginTop: 8 }}>Let's set up<br/><span style={{ color: PAY.orange }}>your money.</span></div>
        <div style={{ font: `400 13px/20px ${PAY.font}`, color: 'rgba(255,255,255,0.7)', marginTop: 12, maxWidth: 320 }}>
          Link an account so Simi can keep an eye on your bills, spending, and people back home.
        </div>
      </div>

      <div style={{
        position: 'absolute', left: 0, right: 0, bottom: 0, top: '38%',
        background: PAY.warm100, borderRadius: '28px 28px 0 0',
        boxShadow: '0 -8px 24px rgba(0,0,0,0.18)', overflow: 'auto',
        padding: '14px 16px 24px', color: PAY.ink,
      }}>
        <div style={{ width: 44, height: 4, borderRadius: 4, background: '#DCCDB7', margin: '0 auto 18px' }}/>

        {/* Setup checklist — 0 of 4 done */}
        <div style={{
          background: 'white', borderRadius: 20, padding: 18, border: '1px solid #F1E5D1',
          boxShadow: '0 2px 10px rgba(77,49,32,0.05)', position: 'relative', overflow: 'hidden',
        }}>
          <div style={{ position: 'absolute', right: -30, top: -30, width: 130, height: 130, borderRadius: '50%',
            background: 'radial-gradient(circle, rgba(243,121,32,0.12) 0%, transparent 70%)' }}/>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <div style={{ font: `700 10px/14px ${PAY.font}`, color: PAY.warm800, letterSpacing: 1, textTransform: 'uppercase' }}>Get started</div>
            <div style={{ flex: 1 }}/>
            <div style={{ font: `600 11px/14px ${PAY.font}`, color: PAY.warm800 }}>0 of 4</div>
          </div>
          <div style={{ font: `700 18px/22px ${PAY.font}`, color: PAY.warm900, marginTop: 6, letterSpacing: -0.3 }}>You're 4 steps from a clear picture.</div>

          <div style={{ height: 6, borderRadius: 3, background: '#F5EADB', marginTop: 14, overflow: 'hidden' }}>
            <div style={{ width: '0%', height: '100%', background: PAY.orange }}/>
          </div>

          <div style={{ marginTop: 14 }}>
            {[
              { ic: 'card', t: 'Link your main account', s: 'Plaid · UK & EU banks', cta: true },
              { ic: 'globe', t: 'Add your home country', s: 'Pick a destination for transfers' },
              { ic: 'user', t: 'Tell Simi about your family', s: 'Set up support obligations' },
              { ic: 'bell', t: 'Turn on bill reminders', s: 'Optional but recommended' },
            ].map((it, i) => (
              <div key={i} style={{
                display: 'flex', alignItems: 'center', gap: 12, padding: '10px 0',
                borderTop: i === 0 ? '1px solid #F5EADB' : 'none',
                borderBottom: '1px solid #F5EADB',
              }}>
                <div style={{
                  width: 32, height: 32, borderRadius: 50, background: it.cta ? '#FFEFE3' : '#F7EEE4',
                  display: 'flex', alignItems: 'center', justifyContent: 'center', color: it.cta ? '#7A3211' : PAY.warm800,
                }}>
                  <Icon name={it.ic} size={16}/>
                </div>
                <div style={{ flex: 1 }}>
                  <div style={{ font: `600 13px/17px ${PAY.font}`, color: PAY.warm900 }}>{it.t}</div>
                  <div style={{ font: `400 11px/14px ${PAY.font}`, color: PAY.warm800, marginTop: 2 }}>{it.s}</div>
                </div>
                {it.cta
                  ? <div style={{ padding: '6px 12px', borderRadius: 50, background: PAY.orange, color: 'white', font: `700 10px/14px ${PAY.font}`, letterSpacing: 0.5, textTransform: 'uppercase' }}>Start</div>
                  : <Icon name="chev" size={16} color={PAY.warm800}/>}
              </div>
            ))}
          </div>
        </div>

        {/* Simi intro */}
        <div style={{
          marginTop: 14, padding: 16, borderRadius: 20,
          background: 'linear-gradient(135deg, #FFE2C5 0%, #FFF2E3 100%)',
          border: '1px solid #F1DEC9', display: 'flex', gap: 12, alignItems: 'flex-start',
        }}>
          <div style={{
            width: 40, height: 40, borderRadius: 50, flex: 'none',
            backgroundImage: "url('assets/simi.png')",
            backgroundSize: 'cover', backgroundPosition: '50% 22%',
            border: '1.5px solid rgba(243,121,32,0.6)',
          }}/>
          <div style={{ flex: 1 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <div style={{ font: `700 10px/14px ${PAY.font}`, color: '#7A3211', letterSpacing: 1, textTransform: 'uppercase' }}>Simi · just now</div>
              <PulseDot color="#7A3211" size={5}/>
            </div>
            <div style={{ font: `500 13px/19px ${PAY.font}`, color: PAY.warm900, marginTop: 4 }}>
              <Typewriter text="I'm here when you're ready. Link an account and I'll start watching for bills and unusual spending." speed={16}/>
            </div>
            <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
              <PayButton variant="primary" size="sm" onClick={onSimi}>Ask Simi</PayButton>
            </div>
          </div>
        </div>

        {/* Empty placeholders for the usual sections */}
        <div style={{ marginTop: 18 }}>
          <SectionHeader title="Family back home" kicker="Wherever they are"/>
          <EmptyTile icon="user" title="No one added yet" body="Add the people you support and Simi will remind you."/>
        </div>
        <div style={{ marginTop: 14 }}>
          <SectionHeader title="Upcoming bills"/>
          <EmptyTile icon="bill" title="No bills tracked yet" body="Link an account so we can pull bills automatically."/>
        </div>

        <div style={{ height: 18 }}/>
      </div>
    </div>
  );
}

function EmptyTile({ icon, title, body }) {
  return (
    <div style={{
      background: 'white', borderRadius: 16, padding: 18, border: '1px dashed #DCCDB7',
      display: 'flex', alignItems: 'center', gap: 14,
    }}>
      <div style={{
        width: 44, height: 44, borderRadius: 50, background: '#F7EEE4',
        display: 'flex', alignItems: 'center', justifyContent: 'center', color: PAY.warm800,
      }}><Icon name={icon} size={20}/></div>
      <div style={{ flex: 1 }}>
        <div style={{ font: `700 13px/17px ${PAY.font}`, color: PAY.warm900 }}>{title}</div>
        <div style={{ font: `400 11px/15px ${PAY.font}`, color: PAY.warm800, marginTop: 2 }}>{body}</div>
      </div>
    </div>
  );
}

function RhythmStrip() {
  // 12 bars representing weekly spending
  const heights = [22, 36, 28, 14, 40, 30, 18, 26, 44, 22, 32, 16];
  const today = 8;
  return (
    <div>
      <div style={{ display: 'flex', alignItems: 'flex-end', gap: 4, height: 56 }}>
        {heights.map((h, i) => (
          <div key={i} style={{
            flex: 1, height: h, borderRadius: 3,
            background: i === today ? PAY.orange : i < today ? '#F3A85C' : '#ECD9BE',
            opacity: i > today ? 0.5 : 1,
          }}/>
        ))}
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 10 }}>
        <div style={{ font: `600 12px/16px ${PAY.font}`, color: PAY.warm900 }}>£68 today</div>
        <div style={{ font: `400 11px/14px ${PAY.font}`, color: PAY.warm800 }}>· avg £52</div>
        <div style={{ flex: 1 }}/>
        <div style={{ font: `600 10px/14px ${PAY.font}`, color: PAY.orange, textTransform: 'uppercase', letterSpacing: 0.6 }}>Last 12 days</div>
      </div>
    </div>
  );
}

Object.assign(window, { HomeScreen, HomeFreshScreen, PayHeader, SimiBadge, PayBottomNav, FamilyTile, BillRow, Sparkline, RhythmStrip });
