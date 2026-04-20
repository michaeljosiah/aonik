// Payabo mobile — Home
// Matches lib/features/dashboard/presentation/dashboard_screen.dart:
//   1. Dark gradient hero fills the top half.
//   2. Greeting + "This might interest you..." story sentence w/ orange emphasis.
//   3. Warm-cream sheet slid up from the bottom (~60% of screen), containing:
//      - Available to spend card (with progress bar)
//      - Net worth card (with Assets + Bills tiles)
//      - Upcoming bills · Previous orders · Family support sections
function HomeScreen({ onTxn }) {
  return (
    <div style={{ background: payHero, height: '100%', position: 'relative', overflow: 'hidden' }}>
      {/* ── dark hero content ── */}
      <div style={{ padding: '12px 20px 0', display: 'flex', alignItems: 'center', gap: 12 }}>
        <div style={{
          width: 40, height: 40, borderRadius: 50,
          backgroundImage: "url('../../assets/demo_profile.jpg')", backgroundSize: 'cover', backgroundPosition: 'center',
          border: `1.5px solid rgba(243,121,32,0.6)`,
        }}/>
        <div style={{ flex: 1 }}/>
        <div style={{
          width: 40, height: 40, borderRadius: 50, background: 'rgba(255,255,255,0.06)',
          border: '1px solid rgba(255,255,255,0.08)', display: 'flex', alignItems: 'center', justifyContent: 'center',
          position: 'relative', color: 'rgba(255,255,255,0.8)',
        }}>
          <Icon name="bell" size={18}/>
          <div style={{ width: 7, height: 7, borderRadius: 50, background: PAY.orange, position: 'absolute', top: 9, right: 9, border: '1.5px solid ' + PAY.heroTop }}/>
        </div>
      </div>

      <div style={{ padding: '40px 24px 32px', maxWidth: 360 }}>
        <div style={{ font: `700 30px/34px ${PAY.font}`, color: 'white', letterSpacing: -0.4 }}>Good morning, Kwame.</div>
        <div style={{ font: `400 15px/22px ${PAY.font}`, color: 'rgba(255,255,255,0.9)', marginTop: 14 }}>
          This might interest you. You have <Emph>£2,184.60</Emph> available to spend, <Emph>3 bills</Emph> due this week, and <Emph>+£412.80</Emph> added to your net worth this month.
        </div>
      </div>

      {/* ── warm sheet ── */}
      <div style={{
        position: 'absolute', left: 0, right: 0, bottom: 0, top: '42%',
        background: PAY.warm100, borderRadius: '28px 28px 0 0',
        boxShadow: '0 -8px 24px rgba(0,0,0,0.18)', overflow: 'auto',
        padding: '18px 16px 20px',
      }}>
        {/* grabber */}
        <div style={{ width: 44, height: 4, borderRadius: 4, background: '#DCCDB7', margin: '0 auto 14px' }}/>

        {/* Available to spend */}
        <div style={{
          background: 'white', borderRadius: 20, padding: 18,
          border: `1px solid #F1E5D1`, boxShadow: '0 2px 10px rgba(77,49,32,0.05)',
          marginBottom: 10,
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <div style={{ font: `600 13px/16px ${PAY.font}`, color: PAY.warm900 }}>Available to spend</div>
            <div style={{ flex: 1 }}/>
            <span style={{
              font: `700 10px/14px ${PAY.font}`, color: '#1B7030',
              background: '#ECFAEF', padding: '3px 8px', borderRadius: 50,
              textTransform: 'uppercase', letterSpacing: 0.6,
            }}>48% free</span>
          </div>
          <div style={{ font: `700 32px/38px ${PAY.font}`, color: PAY.ink, marginTop: 10, letterSpacing: -0.5 }}>£2,184.60</div>
          <div style={{ font: `400 12px/17px ${PAY.font}`, color: PAY.warm800, marginTop: 4 }}>
            You still have room for planned spending.
          </div>
          <div style={{ height: 6, borderRadius: 3, background: '#F5EADB', marginTop: 14, overflow: 'hidden' }}>
            <div style={{ width: '48%', height: '100%', background: PAY.orange, borderRadius: 3 }}/>
          </div>
        </div>

        {/* Net worth */}
        <div style={{
          background: 'white', borderRadius: 20, padding: 18,
          border: `1px solid #F1E5D1`, boxShadow: '0 2px 10px rgba(77,49,32,0.05)',
          marginBottom: 18,
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <div style={{ font: `600 13px/16px ${PAY.font}`, color: PAY.warm900 }}>Net worth</div>
            <div style={{ flex: 1 }}/>
            <span style={{
              font: `700 10px/14px ${PAY.font}`, color: '#1B7030',
              background: '#ECFAEF', padding: '3px 8px', borderRadius: 50,
              textTransform: 'uppercase', letterSpacing: 0.6,
            }}>↑ trending up</span>
          </div>
          <div style={{ font: `700 26px/32px ${PAY.font}`, color: PAY.ink, marginTop: 8, letterSpacing: -0.3 }}>£18,642.90</div>
          <div style={{ font: `400 12px/17px ${PAY.font}`, color: PAY.warm800, marginTop: 4 }}>
            +£412.80 since last month across your linked balances.
          </div>
          <div style={{ display: 'flex', gap: 10, marginTop: 14 }}>
            <StatTile label="Assets" value="£21,205.00"/>
            <StatTile label="Bills" value="£2,562.10"/>
          </div>
        </div>

        {/* Upcoming bills */}
        <SectionHeader title="Upcoming bills" action="See all"/>
        <div style={{ background: 'white', borderRadius: 16, border: `1px solid #F1E5D1`, padding: '4px 0', marginBottom: 18 }}>
          <BillRow logoBg="#0B1A4A" logoFg="white" logoText="SKY" name="Sky Broadband" due="Due Thu · £42.99" tone="warning"/>
          <BillRow logoBg="#E60028" logoFg="white" logoText="V" name="Vodafone" due="Due Sat · £28.00" tone="warning" divider={false}/>
        </div>

        {/* Previous orders */}
        <SectionHeader title="Previous orders" action="See all"/>
        <div style={{ background: 'white', borderRadius: 16, border: `1px solid #F1E5D1`, padding: '4px 0', marginBottom: 18 }}>
          <BillRow logoBg="#111" logoFg="white" logoText="BG" name="British Gas" due="Paid Nov 12 · £94.20" tone="success"/>
          <BillRow logoBg="#005EB8" logoFg="white" logoText="TF" name="TfL Congestion" due="Paid Nov 08 · £15.00" tone="success" divider={false}/>
        </div>

        {/* Family support */}
        <SectionHeader title="Family support" action="View"/>
        <div onClick={onTxn} style={{
          background: 'white', borderRadius: 16, border: `1px solid #F1E5D1`,
          padding: 14, display: 'flex', alignItems: 'center', gap: 12, cursor: 'pointer',
          marginBottom: 8,
        }}>
          <div style={{
            width: 40, height: 40, borderRadius: 50, background: '#FFEFE3', color: '#7A3211',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            font: `700 13px/18px ${PAY.font}`,
          }}>AS</div>
          <div style={{ flex: 1 }}>
            <div style={{ font: `600 13px/18px ${PAY.font}`, color: PAY.ink }}>Ama Serwaa · Ghana</div>
            <div style={{ font: `400 11px/16px ${PAY.font}`, color: PAY.n500 }}>Monthly · last sent Nov 01</div>
          </div>
          <div style={{ font: `700 13px/18px ${PAY.font}`, color: PAY.ink }}>£120.00</div>
        </div>
      </div>
    </div>
  );
}

function Emph({ children }) {
  return <span style={{ color: '#F3A85C', fontWeight: 700 }}>{children}</span>;
}

function StatTile({ label, value }) {
  return (
    <div style={{
      flex: 1, padding: 12, borderRadius: 16,
      background: PAY.warm150, border: `1px solid #ECD9BE`,
    }}>
      <div style={{ font: `400 11px/14px ${PAY.font}`, color: PAY.warm800 }}>{label}</div>
      <div style={{ font: `700 16px/20px ${PAY.font}`, color: PAY.ink, marginTop: 4 }}>{value}</div>
    </div>
  );
}

function SectionHeader({ title, action }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', padding: '0 4px 8px' }}>
      <div style={{ font: `700 15px/20px ${PAY.font}`, color: PAY.warm900, flex: 1 }}>{title}</div>
      <div style={{ font: `600 12px/16px ${PAY.font}`, color: PAY.orange, textTransform: 'uppercase', letterSpacing: 0.3 }}>{action}</div>
    </div>
  );
}

function BillRow({ logoBg, logoFg, logoText, name, due, tone, divider = true }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 12, padding: '14px 16px',
      borderBottom: divider ? '1px solid #F5EADB' : 'none',
    }}>
      <div style={{
        width: 36, height: 36, borderRadius: 8, background: logoBg, color: logoFg,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        font: `700 11px/14px ${PAY.font}`,
      }}>{logoText}</div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ font: `600 13px/18px ${PAY.font}`, color: PAY.ink }}>{name}</div>
        <div style={{ font: `400 11px/16px ${PAY.font}`, color: PAY.n500 }}>{due}</div>
      </div>
      <Icon name="chev" size={16} color={PAY.warm800}/>
    </div>
  );
}

Object.assign(window, { HomeScreen });
