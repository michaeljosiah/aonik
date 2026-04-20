// Payabo mobile — Pay dashboard + Spending overview + Simi chat + Txn detail
// All screens are rooted in the real codebase (features/payments, features/spending,
// features/chat) — dark gradient hero up top, warm-cream sheet below with cards,
// matching PayaboBottomNav labels (Home · Pay · Spending · Simi).

// ─── PAY DASHBOARD ─────────────────────────────────────────────────────────
// Matches pay_dashboard_screen.dart: dark hero with "Support your family,
// wherever they are." marketing copy + profile / bell, then warm sheet with
// two big action cards (Pay a bill, Send money), Quick send avatar strip,
// and Recent activity.
function PayScreen() {
  return (
    <div style={{ background: payHero, height: '100%', position: 'relative', overflow: 'hidden' }}>
      {/* header */}
      <div style={{ padding: '12px 20px 0', display: 'flex', alignItems: 'center', gap: 12 }}>
        <div style={{
          width: 40, height: 40, borderRadius: 50,
          backgroundImage: "url('../../assets/demo_profile.jpg')", backgroundSize: 'cover', backgroundPosition: 'center',
          border: `1.5px solid rgba(243,121,32,0.6)`,
        }}/>
        <div style={{ flex: 1 }}>
          <div style={{ font: `500 11px/14px ${PAY.font}`, color: 'rgba(255,255,255,0.55)' }}>Welcome back</div>
          <div style={{ font: `700 14px/18px ${PAY.font}`, color: 'white' }}>Kwame Mensah</div>
        </div>
        <div style={{
          width: 40, height: 40, borderRadius: 50, background: 'rgba(255,255,255,0.06)',
          border: '1px solid rgba(255,255,255,0.08)', display: 'flex', alignItems: 'center', justifyContent: 'center',
          color: 'rgba(255,255,255,0.8)',
        }}>
          <Icon name="bell" size={18}/>
        </div>
      </div>

      <div style={{ padding: '36px 24px 28px', maxWidth: 360 }}>
        <div style={{ font: `700 28px/32px ${PAY.font}`, color: 'white', letterSpacing: -0.3, whiteSpace: 'pre-line' }}>
          {'Support your family,\nwherever they are.'}
        </div>
        <div style={{ font: `400 14px/20px ${PAY.font}`, color: 'rgba(255,255,255,0.6)', marginTop: 12 }}>
          Send money, pay bills, track everything.
        </div>
      </div>

      {/* warm sheet */}
      <div style={{
        position: 'absolute', left: 0, right: 0, bottom: 0, top: '40%',
        background: PAY.warm100, borderRadius: '28px 28px 0 0',
        boxShadow: '0 -8px 24px rgba(0,0,0,0.18)', overflow: 'auto',
        padding: '18px 16px 20px',
      }}>
        <div style={{ width: 44, height: 4, borderRadius: 4, background: '#DCCDB7', margin: '0 auto 16px' }}/>

        {/* action cards */}
        <ActionCard
          icon="bill" iconBg="#FFEFE3" iconFg="#7A3211"
          title="Pay a bill"
          subtitle="Utilities, TV, airtime and household essentials."
          action="Start"
        />
        <ActionCard
          icon="send" iconBg="#E8EFFF" iconFg="#1E4AB5"
          title="Send money"
          subtitle="Transfer funds to family and friends in a few taps."
          action="Start"
        />

        {/* quick send */}
        <div style={{ display: 'flex', alignItems: 'baseline', padding: '18px 4px 10px' }}>
          <div style={{ font: `700 15px/20px ${PAY.font}`, color: PAY.warm900, flex: 1 }}>Quick send</div>
          <div style={{ font: `600 12px/16px ${PAY.font}`, color: PAY.orange, textTransform: 'uppercase', letterSpacing: 0.3 }}>View all activity</div>
        </div>
        <div style={{ display: 'flex', gap: 14, overflowX: 'auto', paddingBottom: 4, marginBottom: 12 }}>
          <QuickSend in_="" nm="New" isNew/>
          {[['AS','Ama'],['KO','Kofi'],['NA','Nana'],['YA','Yaa'],['EB','Ebo']].map(([i,n])=>
            <QuickSend key={n} in_={i} nm={n}/>
          )}
        </div>

        {/* recent */}
        <div style={{ padding: '8px 4px 10px', font: `700 15px/20px ${PAY.font}`, color: PAY.warm900 }}>
          Recent activity
        </div>
        <div style={{ background: 'white', borderRadius: 16, border: `1px solid #F1E5D1`, padding: '2px 16px' }}>
          <PayTxnRow avatar="AS" title="Ama Serwaa" sub="Today, 09:42 · Ghana" amount="-£28.00"/>
          <PayTxnRow avatar="EB" title="Ebo Bonsu" sub="Yesterday · Nigeria" amount="-£45.00"/>
          <PayTxnRow avatar="SK" avatarBg="#E8F0FF" avatarFg="#1E4AB5" title="Sky Broadband" sub="Mon · Direct debit" amount="-£42.99"/>
        </div>
      </div>
    </div>
  );
}

function ActionCard({ icon, iconBg, iconFg, title, subtitle, action }) {
  return (
    <div style={{
      background: 'white', borderRadius: 20, padding: 18,
      border: `1px solid #F1E5D1`, boxShadow: '0 2px 10px rgba(77,49,32,0.05)',
      marginBottom: 10, display: 'flex', alignItems: 'center', gap: 14,
    }}>
      <div style={{
        width: 48, height: 48, borderRadius: 14,
        background: iconBg, color: iconFg,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
      }}>
        <Icon name={icon} size={22}/>
      </div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ font: `700 15px/20px ${PAY.font}`, color: PAY.ink }}>{title}</div>
        <div style={{ font: `400 12px/17px ${PAY.font}`, color: PAY.warm800, marginTop: 2 }}>{subtitle}</div>
      </div>
      <div style={{
        font: `700 11px/14px ${PAY.font}`, color: PAY.orange,
        textTransform: 'uppercase', letterSpacing: 0.6,
      }}>{action}</div>
    </div>
  );
}

function QuickSend({ in_, nm, isNew }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6, flex: 'none' }}>
      <div style={{
        width: 52, height: 52, borderRadius: 50,
        background: isNew ? 'white' : '#FFEFE3',
        color: isNew ? PAY.warm800 : '#7A3211',
        border: isNew ? `1.5px dashed ${PAY.warm500}` : 'none',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        font: `700 14px/18px ${PAY.font}`,
      }}>{isNew ? <Icon name="add" size={20}/> : in_}</div>
      <div style={{ font: `500 11px/14px ${PAY.font}`, color: PAY.warm900 }}>{nm}</div>
    </div>
  );
}

// ─── SPENDING OVERVIEW ─────────────────────────────────────────────────────
// Matches spending_overview_screen.dart: warm-cream screen titled "Spend",
// section pills (Overview / Budget / Bills / Accounts), then Snapshot,
// Monthly breakdown (donut), Quick insights, Recent transactions.
function SpendingScreen() {
  const [tab, setTab] = React.useState('Overview');
  return (
    <div style={{ background: PAY.warm100, height: '100%', overflow: 'auto' }}>
      {/* header */}
      <div style={{ padding: '4px 20px 8px', display: 'flex', alignItems: 'center' }}>
        <div style={{ flex: 1, font: `700 28px/34px ${PAY.font}`, color: PAY.warm900, letterSpacing: -0.3 }}>Spend</div>
        <div style={{
          width: 40, height: 40, borderRadius: 50, background: 'white',
          border: `1px solid #F1E5D1`, display: 'flex', alignItems: 'center', justifyContent: 'center',
          color: PAY.warm800,
        }}>
          <Icon name="search" size={18}/>
        </div>
      </div>

      {/* pills */}
      <div style={{ padding: '0 16px 14px', display: 'flex', gap: 8, overflowX: 'auto' }}>
        {['Overview', 'Budget', 'Bills', 'Accounts'].map(p => (
          <div key={p} onClick={() => setTab(p)} style={{
            padding: '8px 14px', borderRadius: 50, flex: 'none',
            background: tab === p ? PAY.warm900 : 'white',
            color: tab === p ? 'white' : PAY.warm900,
            border: `1px solid ${tab === p ? PAY.warm900 : '#F1E5D1'}`,
            font: `600 12px/16px ${PAY.font}`, cursor: 'pointer',
          }}>{p}</div>
        ))}
      </div>

      {/* Snapshot */}
      <SpendingSection title="Snapshot" subtitle="The numbers that matter this month"/>
      <div style={{ padding: '0 16px 6px' }}>
        <div style={{
          background: 'white', borderRadius: 20, padding: 18,
          border: `1px solid #F1E5D1`, boxShadow: '0 2px 10px rgba(77,49,32,0.05)',
        }}>
          <div style={{ font: `600 13px/16px ${PAY.font}`, color: PAY.warm900 }}>Safe to spend</div>
          <div style={{ font: `700 34px/38px ${PAY.font}`, color: PAY.ink, marginTop: 8, letterSpacing: -0.6 }}>£1,840.00</div>
          <div style={{ font: `400 12px/17px ${PAY.font}`, color: PAY.warm800, marginTop: 4 }}>
            After bills & usual spending this month
          </div>
          <div style={{ height: 6, borderRadius: 3, background: '#F5EADB', marginTop: 14, overflow: 'hidden' }}>
            <div style={{ width: '62%', height: '100%', background: PAY.orange, borderRadius: 3 }}/>
          </div>
          <div style={{ display: 'flex', marginTop: 14, gap: 10 }}>
            <StatTile label="Spent this month" value="£2,980.50"/>
            <StatTile label="Income" value="£4,820.50"/>
          </div>
        </div>
      </div>

      {/* Monthly breakdown */}
      <SpendingSection title="Monthly breakdown" subtitle="Where this month is going so far"/>
      <div style={{ padding: '0 16px' }}>
        <div style={{
          background: 'white', borderRadius: 20, padding: 18,
          border: `1px solid #F1E5D1`, boxShadow: '0 2px 10px rgba(77,49,32,0.05)',
          display: 'flex', alignItems: 'center', gap: 18,
        }}>
          <div style={{
            width: 108, height: 108, borderRadius: 50, flex: 'none',
            background: `conic-gradient(${PAY.orange} 0 38%, #F3A85C 38% 62%, #E5C18A 62% 82%, #DCCDB7 82% 100%)`,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
          }}>
            <div style={{
              width: 74, height: 74, borderRadius: 50, background: 'white',
              display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
            }}>
              <div style={{ font: `700 16px/20px ${PAY.font}`, color: PAY.ink }}>£2.98k</div>
              <div style={{ font: `400 10px/12px ${PAY.font}`, color: PAY.warm800 }}>spent</div>
            </div>
          </div>
          <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 8 }}>
            {[
              [PAY.orange, 'Bills', '38%'],
              ['#F3A85C', 'Groceries', '24%'],
              ['#E5C18A', 'Transport', '20%'],
              ['#DCCDB7', 'Other', '18%'],
            ].map(([c, l, v]) => (
              <div key={l} style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <div style={{ width: 10, height: 10, borderRadius: 2, background: c }}/>
                <div style={{ flex: 1, font: `500 13px/16px ${PAY.font}`, color: PAY.ink }}>{l}</div>
                <div style={{ font: `700 13px/16px ${PAY.font}`, color: PAY.ink }}>{v}</div>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Quick insights */}
      <SpendingSection title="Quick insights" subtitle="AI-generated nudges from your spending patterns"/>
      <div style={{ padding: '0 16px 4px' }}>
        <div style={{
          borderRadius: 20, padding: 18,
          background: 'linear-gradient(135deg, #FFE2C5 0%, #FFF2E3 100%)',
          border: `1px solid #F1DEC9`,
        }}>
          <div style={{
            display: 'inline-flex', alignItems: 'center', gap: 6,
            padding: '4px 10px', borderRadius: 50, background: 'white', color: '#7A3211',
            font: `700 10px/14px ${PAY.font}`, letterSpacing: 0.6, textTransform: 'uppercase',
          }}>
            <Icon name="sparkle" size={12} color="#7A3211" strokeWidth={2.4}/> AI insight
          </div>
          <div style={{ font: `600 14px/20px ${PAY.font}`, color: PAY.warm900, marginTop: 10 }}>
            You're spending 22% more on transport than usual — mostly weekend rides.
          </div>
          <div style={{ font: `400 12px/17px ${PAY.font}`, color: PAY.warm800, marginTop: 4 }}>
            Ask Simi to set a weekend travel cap?
          </div>
        </div>
      </div>

      {/* Recent transactions */}
      <SpendingSection title="Recent transactions" subtitle="A quick preview before you dive into everything"/>
      <div style={{ padding: '0 16px 20px' }}>
        <div style={{ background: 'white', borderRadius: 16, border: `1px solid #F1E5D1`, padding: '2px 16px' }}>
          <PayTxnRow avatar="TF" avatarBg="#E8EFFF" avatarFg="#005EB8" title="TfL Travel" sub="Today · Transport" amount="-£5.40"/>
          <PayTxnRow avatar="TE" avatarBg="#FFEDEB" avatarFg="#8A0022" title="Tesco Express" sub="Today · Groceries" amount="-£18.22"/>
          <PayTxnRow avatar="SK" avatarBg="#E8F0FF" avatarFg="#1E4AB5" title="Sky Broadband" sub="Mon · Bills" amount="-£42.99"/>
        </div>
        <div style={{
          textAlign: 'center', padding: '14px 0 4px',
          font: `700 11px/14px ${PAY.font}`, color: PAY.orange,
          textTransform: 'uppercase', letterSpacing: 0.8,
        }}>View all transactions</div>
      </div>
    </div>
  );
}

function SpendingSection({ title, subtitle }) {
  return (
    <div style={{ padding: '12px 20px 10px' }}>
      <div style={{ font: `700 17px/22px ${PAY.font}`, color: PAY.warm900 }}>{title}</div>
      <div style={{ font: `400 12px/17px ${PAY.font}`, color: PAY.warm800, marginTop: 2 }}>{subtitle}</div>
    </div>
  );
}

// ─── SIMI CHAT (unchanged — user said they like it) ────────────────────────
function ChatScreen() {
  const msgs = [
    { who: 'simi', t: "Good morning, Kwame. Here's what I'm watching for you today." },
    { who: 'simi', t: 'Your Sky Broadband bill of £42.99 is due Thursday. Want me to schedule it?' },
    { who: 'you', t: 'Yes please — from my main account.' },
    { who: 'simi', t: "Done. I'll pay Sky on Thursday from your GBP account. I'll also nudge you if your balance drops below £300." },
  ];
  return (
    <div style={{ background: payChatHero, color: 'white', height: '100%', display: 'flex', flexDirection: 'column' }}>
      <div style={{ padding: '16px 20px 12px', display: 'flex', alignItems: 'center', gap: 12 }}>
        <div style={{
          width: 40, height: 40, borderRadius: 50,
          backgroundImage: "url('../../assets/simi.png')", backgroundSize: 'cover', backgroundPosition: '50% 20%',
          boxShadow: '0 0 0 1.5px rgba(243,121,32,0.5)',
        }}/>
        <div style={{ flex: 1 }}>
          <div style={{ font: `700 15px/20px ${PAY.font}`, color: 'white' }}>Simi</div>
          <div style={{ font: `400 11px/14px ${PAY.font}`, color: 'rgba(255,255,255,0.6)' }}>AI companion · always listening</div>
        </div>
        <Icon name="settings" size={20} color="rgba(255,255,255,0.6)"/>
      </div>
      <div style={{ flex: 1, padding: '10px 16px 12px', display: 'flex', flexDirection: 'column', gap: 10, overflow: 'auto' }}>
        {msgs.map((m, i) => (
          <div key={i} style={{ display: 'flex', justifyContent: m.who === 'you' ? 'flex-end' : 'flex-start' }}>
            <div style={{
              maxWidth: '78%', padding: '10px 14px', borderRadius: 16,
              background: m.who === 'you' ? PAY.orange : 'rgba(255,255,255,0.08)',
              border: m.who === 'you' ? 'none' : '1px solid rgba(255,255,255,0.08)',
              font: `400 13px/18px ${PAY.font}`, color: 'white',
            }}>{m.t}</div>
          </div>
        ))}
      </div>
      <div style={{ padding: '10px 16px 12px', display: 'flex', gap: 8, alignItems: 'center' }}>
        <div style={{ flex: 1, padding: '10px 14px', borderRadius: 50, background: 'rgba(255,255,255,0.08)', border: '1px solid rgba(255,255,255,0.1)', font: `400 13px/18px ${PAY.font}`, color: 'rgba(255,255,255,0.6)' }}>Ask Simi anything…</div>
        <div style={{ width: 40, height: 40, borderRadius: 50, background: PAY.orange, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
          <Icon name="mic" size={18} color="white"/>
        </div>
      </div>
    </div>
  );
}

// ─── TXN DETAIL ────────────────────────────────────────────────────────────
function TxnDetailScreen({ onBack }) {
  return (
    <div style={{ background: PAY.warm100, height: '100%', display: 'flex', flexDirection: 'column' }}>
      <div style={{ padding: '12px 20px', display: 'flex', alignItems: 'center', gap: 14 }}>
        <div onClick={onBack} style={{ cursor: 'pointer', color: PAY.warm900 }}><Icon name="back" size={22}/></div>
        <div style={{ font: `700 17px/22px ${PAY.font}`, color: PAY.warm900 }}>Transaction</div>
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', padding: '24px 20px 32px' }}>
        <div style={{ width: 72, height: 72, borderRadius: 50, background: '#FFEFE3', color: '#7A3211', display: 'flex', alignItems: 'center', justifyContent: 'center', font: `700 22px/28px ${PAY.font}` }}>AS</div>
        <div style={{ font: `600 15px/20px ${PAY.font}`, color: PAY.warm900, marginTop: 12 }}>Ama Serwaa</div>
        <div style={{ font: `700 30px/36px ${PAY.font}`, color: PAY.ink, marginTop: 10, letterSpacing: -0.3 }}>-£120.00</div>
        <div style={{ marginTop: 10 }}><PayChip tone="success"><Icon name="check" size={12} color="#1B7030"/>Completed</PayChip></div>
      </div>
      <div style={{ padding: '0 16px', flex: 1 }}>
        <div style={{ background: 'white', borderRadius: 20, border: `1px solid #F1E5D1`, padding: '4px 16px' }}>
          {[
            ['Date', 'Today, 09:42 AM'],
            ['From', 'GBP main account'],
            ['To', 'Ama Serwaa · MTN MoMo'],
            ['Reference', 'Family support'],
            ['Exchange rate', '£1 = GHS 19.42'],
            ['Fee', '£1.50'],
          ].map(([k, v], i, a) => (
            <div key={k} style={{
              display: 'flex', justifyContent: 'space-between', padding: '12px 0',
              borderBottom: i === a.length - 1 ? 'none' : `1px solid #F5EADB`,
            }}>
              <div style={{ font: `400 13px/18px ${PAY.font}`, color: PAY.warm800 }}>{k}</div>
              <div style={{ font: `600 13px/18px ${PAY.font}`, color: PAY.ink }}>{v}</div>
            </div>
          ))}
        </div>
        <div style={{ display: 'flex', gap: 10, marginTop: 16 }}>
          <PayButton variant="secondary" full>SEND AGAIN</PayButton>
          <PayButton variant="link" full>Get receipt</PayButton>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { PayScreen, SpendingScreen, ChatScreen, TxnDetailScreen });
