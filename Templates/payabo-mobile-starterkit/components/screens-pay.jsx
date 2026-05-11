// Pay, Spending, Notifications screens.

// ─── PAY ────────────────────────────────────────────────────────────────────
function PayScreen({ tweaks, onSend, onBill, onSimi }) {
  const dark = tweaks.heroMode === 'dark';
  const heroBg = dark ? payHero : 'linear-gradient(180deg,#2A1B14 0%, #1A1411 46%, #100B09 100%)';
  return (
    <div style={{ background: heroBg, height: '100%', position: 'relative', overflow: 'hidden', color: 'white' }}>
      <GlowOrb size={260} top={-60} right={-80} opacity={0.32}/>
      <GlowOrb size={200} top={40} left={-90} color="#D7A14E" opacity={0.16} blur={70}/>
      <PayHeader dark name="Kwame"/>
      <div style={{ padding: '24px 24px 24px', position: 'relative', zIndex: 2 }}>
        <div style={{ font: `400 11px/14px ${PAY.font}`, color: 'rgba(255,255,255,0.5)', letterSpacing: 1.4, textTransform: 'uppercase' }}>Send · Pay · Top up</div>
        <div style={{ font: `700 28px/32px ${PAY.font}`, color: 'white', letterSpacing: -0.3, marginTop: 8 }}>
          Support your family,<br/><span style={{ color: PAY.orangeSoft }}>wherever they are.</span>
        </div>
        <div style={{ display: 'flex', gap: 6, marginTop: 16, flexWrap: 'wrap' }}>
          <FlagChip cc="gb" label="UK" dark/>
          <FlagChip cc="gh" label="Ghana" dark/>
          <FlagChip cc="ng" label="Nigeria" dark/>
          <FlagChip cc="zw" label="Zim" dark/>
        </div>
      </div>

      <div style={{
        position: 'absolute', left: 0, right: 0, bottom: 0, top: '38%',
        background: PAY.warm100, borderRadius: '28px 28px 0 0', overflow: 'auto',
        padding: '14px 16px 24px', color: PAY.ink, boxShadow: '0 -8px 24px rgba(0,0,0,0.18)',
      }}>
        <div style={{ width: 44, height: 4, borderRadius: 4, background: '#DCCDB7', margin: '0 auto 14px' }}/>

        {/* Rate strip — diaspora signature */}
        <div style={{ display: 'flex', gap: 8, marginBottom: 14, overflowX: 'auto', padding: '0 4px 4px' }}>
          <RateCard from="GBP" to="GHS" rate="19.42" trend="+0.3%"/>
          <RateCard from="GBP" to="NGN" rate="2,142" trend="+1.1%"/>
          <RateCard from="GBP" to="ZMW" rate="32.8" trend="-0.2%"/>
        </div>

        <ActionCard icon="send" iconBg="#FFEFE3" iconFg="#7A3211" title="Send money home"
          subtitle="Transfer to family in seconds · mid-market rate."
          action="Start" onClick={onSend}/>
        <ActionCard icon="bill" iconBg="#E8EFFF" iconFg="#1E4AB5" title="Pay a bill"
          subtitle="Utilities, TV, airtime — in the UK or back home."
          action="Start" onClick={onBill}/>
        <ActionCard icon="qr" iconBg="#ECFAEF" iconFg="#1B7030" title="Scan to pay"
          subtitle="Scan a QR for shops, market traders, or invoices."
          action="Scan"/>

        <SectionHeader title="Quick send" action="View all" style={{ marginTop: 12 }}/>
        <div style={{ display: 'flex', gap: 14, overflowX: 'auto', paddingBottom: 6, marginBottom: 14 }}>
          <QuickSend nm="New" isNew/>
          <QuickSend in_="AS" nm="Ama" cc="gh"/>
          <QuickSend in_="KO" nm="Kofi" cc="gh"/>
          <QuickSend in_="NA" nm="Nana" cc="gh"/>
          <QuickSend in_="EB" nm="Ebo" cc="ng"/>
          <QuickSend in_="YA" nm="Yaa" cc="gh"/>
        </div>

        <SectionHeader title="Recent activity" action="See all"/>
        <div style={{ background: 'white', borderRadius: 16, border: `1px solid #F1E5D1`, padding: '2px 16px', marginBottom: 12 }}>
          <TxnRow avatar="AS" cc="gh" title="Ama Serwaa" sub="Today, 09:42 · Family support" amount="-£28.00"/>
          <TxnRow avatar="EB" cc="ng" title="Ebo Bonsu" sub="Yesterday · School fees" amount="-£45.00"/>
          <TxnRow avatar="SK" avatarBg="#E8EFFF" avatarFg="#1E4AB5" title="Sky Broadband" sub="Mon · Direct debit" amount="-£42.99"/>
        </div>

        {tweaks.simiPresence === 'hero' && (
          <div onClick={onSimi} style={{
            borderRadius: 20, padding: 14, marginTop: 4, cursor: 'pointer',
            background: 'linear-gradient(135deg, #2A1B14 0%, #1A1411 100%)',
            color: 'white', display: 'flex', alignItems: 'center', gap: 12,
          }}>
            <div style={{
              width: 38, height: 38, borderRadius: 50,
              backgroundImage: "url('assets/simi.png')",
              backgroundSize: 'cover', backgroundPosition: '50% 25%',
              border: '1.5px solid rgba(243,121,32,0.5)',
            }}/>
            <div style={{ flex: 1 }}>
              <div style={{ font: `700 12px/16px ${PAY.font}`, color: PAY.orangeSoft, letterSpacing: 0.6, textTransform: 'uppercase' }}>Simi suggests</div>
              <div style={{ font: `600 13px/18px ${PAY.font}`, marginTop: 2 }}>Send £100 to Mum — same amount as last month?</div>
            </div>
            <Icon name="chev" size={18}/>
          </div>
        )}
        <div style={{ height: 20 }}/>
      </div>
    </div>
  );
}

function RateCard({ from, to, rate, trend }) {
  const up = trend.startsWith('+');
  return (
    <div style={{
      flex: 'none', minWidth: 140, padding: '10px 12px', borderRadius: 14,
      background: 'white', border: `1px solid #F1E5D1`, boxShadow: '0 2px 10px rgba(77,49,32,0.05)',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
        <div style={{ width: 16, height: 16, borderRadius: 50, backgroundImage: `url('assets/flags/${from.toLowerCase() === 'gbp' ? 'gb' : from.toLowerCase()}.svg')`, backgroundSize: 'cover' }}/>
        <span style={{ font: `600 10px/14px ${PAY.font}`, color: PAY.warm800 }}>{from} →</span>
        <div style={{ width: 16, height: 16, borderRadius: 50, backgroundImage: `url('assets/flags/${to === 'GHS' ? 'gh' : to === 'NGN' ? 'ng' : to === 'ZMW' ? 'zm' : 'gh'}.svg')`, backgroundSize: 'cover' }}/>
        <span style={{ font: `600 10px/14px ${PAY.font}`, color: PAY.warm800 }}>{to}</span>
      </div>
      <div style={{ font: `700 18px/22px ${PAY.font}`, color: PAY.ink, marginTop: 4 }}>{rate}</div>
      <div style={{ font: `600 10px/14px ${PAY.font}`, color: up ? '#1B7030' : '#8A0022' }}>{trend} today</div>
    </div>
  );
}

function ActionCard({ icon, iconBg, iconFg, title, subtitle, action, onClick }) {
  return (
    <div onClick={onClick} style={{
      background: 'white', borderRadius: 16, padding: 14,
      border: `1px solid #F1E5D1`, boxShadow: '0 2px 10px rgba(77,49,32,0.05)',
      marginBottom: 8, display: 'flex', alignItems: 'center', gap: 14, cursor: 'pointer',
    }}>
      <div style={{
        width: 44, height: 44, borderRadius: 12, background: iconBg, color: iconFg,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
      }}><Icon name={icon} size={20}/></div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ font: `700 14px/18px ${PAY.font}`, color: PAY.ink }}>{title}</div>
        <div style={{ font: `400 11px/16px ${PAY.font}`, color: PAY.warm800, marginTop: 2 }}>{subtitle}</div>
      </div>
      <div style={{ font: `700 11px/14px ${PAY.font}`, color: PAY.orange, textTransform: 'uppercase', letterSpacing: 0.6 }}>{action} →</div>
    </div>
  );
}

function QuickSend({ in_, nm, isNew, cc }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6, flex: 'none', cursor: 'pointer' }}>
      <div style={{ position: 'relative' }}>
        <div style={{
          width: 52, height: 52, borderRadius: 50,
          background: isNew ? 'white' : '#FFEFE3', color: isNew ? PAY.warm800 : '#7A3211',
          border: isNew ? `1.5px dashed ${PAY.warm500}` : 'none',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          font: `700 14px/18px ${PAY.font}`,
        }}>{isNew ? <Icon name="add" size={20}/> : in_}</div>
        {cc && <div style={{
          position: 'absolute', bottom: -2, right: -2, width: 18, height: 18, borderRadius: 50,
          backgroundImage: `url('assets/flags/${cc}.svg')`, backgroundSize: 'cover',
          border: '2px solid white',
        }}/>}
      </div>
      <div style={{ font: `500 11px/14px ${PAY.font}`, color: PAY.warm900 }}>{nm}</div>
    </div>
  );
}

function TxnRow({ avatar, avatarBg = '#FFEFE3', avatarFg = '#7A3211', cc, title, sub, amount, onClick, chip, account = 'Monzo', badge = 'M' }) {
  // Strip date prefix ("Today · " / "Yesterday · " / "May 01 · ") and use the
  // first remaining segment as the time-style label, mirroring Flutter's
  // transaction.time field.
  const time = (() => {
    if (!sub) return '';
    const parts = sub.split('·').map(s => s.trim());
    return parts[0];
  })();
  const isCredit = amount && amount.startsWith('+');
  return <div onClick={onClick} style={{
    display: 'flex', alignItems: 'flex-start', gap: 14, padding: '16px 0',
    borderBottom: `1px solid #F1E5D1`, cursor: onClick ? 'pointer' : 'default',
  }}>
    <div style={{ position: 'relative', flexShrink: 0 }}>
      <div style={{
        width: 52, height: 52, borderRadius: '50%', background: avatarBg, color: avatarFg,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        font: `700 15px/20px ${PAY.font}`,
      }}>{avatar}</div>
      {cc && <div style={{
        position: 'absolute', bottom: -2, right: -2, width: 18, height: 18, borderRadius: '50%',
        backgroundImage: `url('assets/flags/${cc}.svg')`, backgroundSize: 'cover',
        border: '2px solid white',
      }}/>}
    </div>
    <div style={{ flex: 1, minWidth: 0, paddingTop: 2 }}>
      <div style={{ font: `600 16px/20px ${PAY.font}`, color: PAY.ink, letterSpacing: -0.1 }}>{title}</div>
      <div style={{ font: `400 13px/18px ${PAY.font}`, color: PAY.warm800, marginTop: 4 }}>{time}</div>
    </div>
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 8 }}>
      <div style={{ font: `700 20px/24px ${PAY.font}`, color: isCredit ? '#1B7030' : PAY.ink, letterSpacing: -0.3 }}>{amount}</div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <span style={{ font: `400 12px/16px ${PAY.font}`, color: PAY.warm800 }}>{account}</span>
        <div style={{
          width: 28, height: 28, borderRadius: '50%', background: PAY.ink, color: '#7CE0A0',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          font: `700 11px/14px ${PAY.font}`,
        }}>{badge}</div>
      </div>
      {chip}
    </div>
  </div>;
}

// Date row that groups transactions by day (mirrors _TransactionDateRow).
function TxnDateRow({ date, total }) {
  return <div style={{
    display: 'flex', alignItems: 'center', padding: '10px 0 6px',
    borderBottom: `1px solid #F1E5D1`,
  }}>
    <div style={{ flex: 1, font: `500 13px/18px ${PAY.font}`, color: PAY.warm800 }}>{date}</div>
    <div style={{ font: `500 15px/20px ${PAY.font}`, color: PAY.warm800 }}>{total}</div>
  </div>;
}

// ─── SPENDING ───────────────────────────────────────────────────────────────
function SpendingScreen({ tweaks, onTxn }) {
  const [tab, setTab] = React.useState('Overview');
  return (
    <div style={{ background: payWarmScreen, height: '100%', overflow: 'auto' }}>
      <div style={{ padding: '4px 20px 4px', display: 'flex', alignItems: 'center', gap: 10 }}>
        <div style={{ flex: 1 }}>
          <div style={{ font: `400 11px/14px ${PAY.font}`, color: PAY.warm800, letterSpacing: 1.2, textTransform: 'uppercase' }}>This month · May</div>
          <div style={{ font: `800 30px/34px ${PAY.font}`, color: PAY.warm900, letterSpacing: -0.6, marginTop: 2 }}>Spend</div>
        </div>
        <div style={{
          width: 40, height: 40, borderRadius: 50, background: 'white',
          border: `1px solid #F1E5D1`, display: 'flex', alignItems: 'center', justifyContent: 'center',
          color: PAY.warm800,
        }}><Icon name="search" size={18}/></div>
        <div style={{
          width: 40, height: 40, borderRadius: 50, background: 'white',
          border: `1px solid #F1E5D1`, display: 'flex', alignItems: 'center', justifyContent: 'center',
          color: PAY.warm800,
        }}><Icon name="filter" size={18}/></div>
      </div>

      {/* pills */}
      <div style={{ padding: '12px 16px 14px', display: 'flex', gap: 8, overflowX: 'auto' }}>
        {['Overview', 'Budget', 'Bills', 'Accounts'].map(p => (
          <div key={p} onClick={() => setTab(p)} style={{
            padding: '8px 14px', borderRadius: 50, flex: 'none',
            background: tab === p ? PAY.warm900 : 'white',
            color: tab === p ? 'white' : PAY.warm900,
            border: `1px solid ${tab === p ? PAY.warm900 : '#F1E5D1'}`,
            font: `700 11px/14px ${PAY.font}`, cursor: 'pointer',
            textTransform: 'uppercase', letterSpacing: 0.6,
            transition: 'all 160ms',
          }}>{p}</div>
        ))}
      </div>

      {tab === 'Overview' && <SpendingOverview tweaks={tweaks} onTxn={onTxn}/>}
      {tab === 'Budget' && <SpendingBudget/>}
      {tab === 'Bills' && <SpendingBills/>}
      {tab === 'Accounts' && <SpendingAccounts/>}
    </div>
  );
}

function SpendingOverview({ tweaks, onTxn }) {
  return (
    <div style={{ padding: '0 16px 24px' }}>
      {/* Safe to spend — dark forest gradient */}
      <div style={{
        background: paySafe, color: 'white', borderRadius: 24, padding: 20,
        boxShadow: '0 8px 24px rgba(18, 44, 28, 0.25)', marginBottom: 12,
        position: 'relative', overflow: 'hidden',
      }}>
        <GlowOrb size={180} top={-60} right={-60} color="#4ACB64" opacity={0.18}/>
        <div style={{ font: `600 10px/14px ${PAY.font}`, color: 'rgba(255,255,255,0.7)', letterSpacing: 1.2, textTransform: 'uppercase' }}>Safe to spend</div>
        <div style={{ font: `800 40px/44px ${PAY.font}`, color: 'white', marginTop: 6, letterSpacing: -0.8 }}>£1,840<span style={{ opacity: 0.5, fontSize: 26 }}>.00</span></div>
        <div style={{ font: `400 12px/17px ${PAY.font}`, color: 'rgba(255,255,255,0.75)', marginTop: 4 }}>
          After bills & usual spending this month
        </div>
        <div style={{ height: 6, borderRadius: 3, background: 'rgba(255,255,255,0.1)', marginTop: 14, overflow: 'hidden' }}>
          <div style={{ width: '62%', height: '100%', background: '#7CE0A0', borderRadius: 3 }}/>
        </div>
        <div style={{ display: 'flex', gap: 8, marginTop: 14 }}>
          <StatTile tone="dark" label="Spent" value="£2,980"/>
          <StatTile tone="dark" label="Income" value="£4,820"/>
          <StatTile tone="dark" label="Saved" value="£640"/>
        </div>
      </div>

      {/* Monthly breakdown — donut */}
      <SectionHeader kicker="Where the money's going" title="By category" action="Open"/>
      <div style={{
        background: 'white', borderRadius: 20, padding: 18,
        border: `1px solid #F1E5D1`, marginBottom: 14,
        display: 'flex', alignItems: 'center', gap: 18,
      }}>
        <div style={{
          width: 116, height: 116, borderRadius: 50, flex: 'none',
          background: `conic-gradient(${PAY.orange} 0 38%, #F3A85C 38% 62%, #E5C18A 62% 82%, #DCCDB7 82% 100%)`,
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          boxShadow: '0 6px 20px rgba(243,121,32,0.18)',
        }}>
          <div style={{
            width: 84, height: 84, borderRadius: 50, background: 'white',
            display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
          }}>
            <div style={{ font: `700 18px/20px ${PAY.font}`, color: PAY.ink, letterSpacing: -0.3 }}>£2,980</div>
            <div style={{ font: `400 9px/12px ${PAY.font}`, color: PAY.warm800, letterSpacing: 0.4, textTransform: 'uppercase' }}>spent</div>
          </div>
        </div>
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 8 }}>
          {[[PAY.orange, 'Bills', '38%', '£1,132'], ['#F3A85C', 'Groceries', '24%', '£715'], ['#E5C18A', 'Transport', '20%', '£596'], ['#DCCDB7', 'Family', '18%', '£537']].map(([c, l, v, amt]) => (
            <div key={l} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <div style={{ width: 8, height: 8, borderRadius: 2, background: c }}/>
              <div style={{ flex: 1, font: `500 12px/16px ${PAY.font}`, color: PAY.ink }}>{l}</div>
              <div style={{ font: `400 10px/14px ${PAY.font}`, color: PAY.warm800 }}>{amt}</div>
              <div style={{ font: `700 12px/16px ${PAY.font}`, color: PAY.ink, width: 36, textAlign: 'right' }}>{v}</div>
            </div>
          ))}
        </div>
      </div>

      {/* AI insight */}
      <div style={{
        borderRadius: 20, padding: 18, marginBottom: 14,
        background: 'linear-gradient(135deg, #FFE2C5 0%, #FFF2E3 100%)',
        border: `1px solid #F1DEC9`, position: 'relative', overflow: 'hidden',
      }}>
        <div style={{ position: 'absolute', right: -10, bottom: -10, width: 90, height: 90, borderRadius: '50%',
          background: 'radial-gradient(circle, rgba(243,121,32,0.3) 0%, transparent 70%)' }}/>
        <div style={{ display: 'inline-flex', alignItems: 'center', gap: 6, padding: '3px 10px', borderRadius: 50, background: 'white', color: '#7A3211', font: `700 10px/14px ${PAY.font}`, letterSpacing: 0.8, textTransform: 'uppercase' }}>
          <Icon name="sparkle" size={12} color="#7A3211" strokeWidth={2.4}/> Simi insight
        </div>
        <div style={{ font: `600 14px/20px ${PAY.font}`, color: PAY.warm900, marginTop: 10 }}>
          You're spending <Emph>22% more on transport</Emph> than usual — mostly weekend rides.
        </div>
        <div style={{ font: `400 12px/17px ${PAY.font}`, color: PAY.warm800, marginTop: 4 }}>
          A weekend travel cap of £40 would put you back on track.
        </div>
        <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
          <PayButton variant="primary" size="sm">Set cap</PayButton>
          <PayButton variant="link" size="sm">Ask Simi</PayButton>
        </div>
      </div>

      {/* Recent transactions — mirrors _RecentTransactionsCard +
          _RecentTransactionRow + _SectionHeading in spending_overview_screen.dart.
          Flat list (no date grouping), thin warm divider between rows, and an
          outlined "View all transactions" CTA at the bottom. */}
      <RecentTransactionsSection onTxn={onTxn}/>
    </div>
  );
}

// Source: _SectionHeading — title (titleLarge / accentBrown) + subtitle
// (bodyMedium / muted) above the recent transactions card.
function OverviewSectionHeading({ title, subtitle }) {
  return (
    <div style={{ padding: '0 4px', marginBottom: 12 }}>
      <div style={{ font: `700 22px/28px ${PAY.font}`, color: PAY.warm900, letterSpacing: -0.3 }}>{title}</div>
      <div style={{ font: `400 14px/20px ${PAY.font}`, color: PAY.warm800, marginTop: 4 }}>{subtitle}</div>
    </div>
  );
}

// Source: _RecentTransactionsCard — PayaboCard with bg=spendingCardWarmElevated,
// padding=lg, rows separated by Divider(height: xl=24, color: borderStrong*0.6),
// then SizedBox(lg) and an outlined "View all transactions" button.
function RecentTransactionsSection({ onTxn }) {
  // Mirrors the mock data exactly (mock_spending_repository.dart, March overview).
  const txns = [
    { merchant: 'Uber',            category: 'Transport',  sub: 'Ride hailing', amt: '£14.20',  icon: 'U',  bgKey: 'dark',        fgKey: 'surfaceBase' },
    { merchant: 'Amazon',          category: 'Shopping',   sub: 'Online',       amt: '£27.99',  icon: 'a',  bgKey: 'warmSurface', fgKey: 'dark'        },
    { merchant: "Nando's",         category: 'Eating out', sub: 'Restaurant',   amt: '£28.45',  icon: 'N',  bgKey: 'warmAccent',  fgKey: 'warmText'    },
    { merchant: 'Shoprite Lekki',  category: 'Groceries',  sub: 'Supermarket',  amt: '₦18,500', icon: 'SR', bgKey: 'dark',        fgKey: 'surfaceBase' },
    { merchant: 'Eko Electricity', category: 'Bills',      sub: 'Electricity',  amt: '₦12,000', icon: 'EE', bgKey: 'warmSurface', fgKey: 'dark'        },
  ];
  // Background / foreground key → token resolver (mirrors _resolveIconBackground
  // / _resolveIconForeground in the Dart screen).
  const bgFor = k => k === 'dark' ? PAY.ink : k === 'warmAccent' ? PAY.warm500 : PAY.warm200;
  const fgFor = k => k === 'surfaceBase' ? 'white' : k === 'warmText' ? PAY.warm900 : PAY.ink;

  return (
    <>
      <OverviewSectionHeading
        title="Recent transactions"
        subtitle="A quick preview before you dive into everything"/>
      <div style={{
        background: PAY.warm100,
        borderRadius: 20,
        border: `1px solid ${PAY.navBorder}`,
        padding: 16,
      }}>
        {txns.map((t, i) => (
          <React.Fragment key={i}>
            <div onClick={i === 0 ? onTxn : undefined} style={{
              display: 'flex', alignItems: 'center',
              cursor: i === 0 ? 'pointer' : 'default',
            }}>
              <div style={{
                width: 46, height: 46, borderRadius: '50%', flexShrink: 0,
                background: bgFor(t.bgKey),
                color: fgFor(t.fgKey),
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                font: `700 16px/20px ${PAY.font}`,
              }}>{t.icon}</div>
              <div style={{ width: 12 }}/>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ font: `600 14px/18px ${PAY.font}`, color: PAY.ink, letterSpacing: -0.1 }}>{t.merchant}</div>
                <div style={{ font: `400 12px/16px ${PAY.font}`, color: PAY.warm800, marginTop: 4 }}>{t.category} · {t.sub}</div>
              </div>
              <div style={{ width: 12 }}/>
              <div style={{ font: `700 16px/20px ${PAY.font}`, color: PAY.warm900, letterSpacing: -0.2 }}>{t.amt}</div>
            </div>
            {i < txns.length - 1 && (
              <div style={{
                height: 1, margin: '24px 0',
                background: PAY.warm300, opacity: 0.6,
              }}/>
            )}
          </React.Fragment>
        ))}
        <div style={{ height: 16 }}/>
        <button onClick={onTxn} style={{
          width: '100%', height: 50,
          background: 'white',
          border: `1px solid ${PAY.warm300}`,
          borderRadius: 18,
          color: PAY.warm900,
          font: `700 14px/18px ${PAY.font}`,
          display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8,
          cursor: 'pointer', padding: 0,
        }}>
          View all transactions
          <Icon name="chev" size={18} color={PAY.warm900}/>
        </button>
      </div>
    </>
  );
}

function SpendingBudget() {
  const cats = [
    ['Bills', 1132, 1300, PAY.orange],
    ['Groceries', 715, 600, '#F3A85C'],
    ['Transport', 596, 400, '#E5C18A'],
    ['Family', 537, 700, '#DCCDB7'],
    ['Entertainment', 142, 250, '#9B7A43'],
  ];
  return (
    <div style={{ padding: '0 16px 24px' }}>
      <div style={{
        background: 'white', borderRadius: 20, padding: 18,
        border: `1px solid #F1E5D1`, marginBottom: 14,
      }}>
        <div style={{ font: `600 11px/14px ${PAY.font}`, color: PAY.warm800, letterSpacing: 0.6, textTransform: 'uppercase' }}>Monthly budget</div>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginTop: 6 }}>
          <div style={{ font: `800 32px/36px ${PAY.font}`, color: PAY.ink, letterSpacing: -0.6 }}>£3,250</div>
          <div style={{ font: `400 12px/17px ${PAY.font}`, color: PAY.warm800 }}>/ £3,250</div>
          <div style={{ flex: 1 }}/>
          <PayChip tone="warning">Over by 8%</PayChip>
        </div>
        {cats.map(([n, sp, bd, c]) => {
          const pct = Math.min(100, (sp / bd) * 100);
          const over = sp > bd;
          return (
            <div key={n} style={{ marginTop: 14 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <div style={{ width: 8, height: 8, borderRadius: 2, background: c }}/>
                <div style={{ font: `600 12px/16px ${PAY.font}`, color: PAY.ink, flex: 1 }}>{n}</div>
                <div style={{ font: `400 11px/16px ${PAY.font}`, color: over ? PAY.danger : PAY.warm800 }}>£{sp} / £{bd}</div>
              </div>
              <div style={{ height: 6, borderRadius: 3, background: '#F5EADB', marginTop: 6, overflow: 'hidden' }}>
                <div style={{ width: pct + '%', height: '100%', background: over ? PAY.danger : c }}/>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function SpendingBills() {
  const bills = [
    { logo: 'SKY', bg: '#0B1A4A', n: 'Sky Broadband', d: 'Due Thu, May 14', amt: '£42.99', tone: 'warning' },
    { logo: 'V', bg: '#E60028', n: 'Vodafone', d: 'Due Sat, May 16', amt: '£28.00', tone: 'warning' },
    { logo: 'EE', bg: '#FFD400', fg: '#111', n: 'EE Mobile', d: 'Dec 02', amt: '£21.00', tone: 'info' },
    { logo: 'BG', bg: '#111', n: 'British Gas', d: 'Paid May 03', amt: '£94.20', tone: 'success' },
    { logo: 'ECG', bg: '#0E5A2C', n: 'ECG Prepaid · Accra', d: 'Auto-top up May 20', amt: 'GHS 200', tone: 'info' },
    { logo: 'DST', bg: '#003D7A', n: 'DStv · Lagos', d: 'Paid May 01', amt: 'NGN 18,500', tone: 'success' },
  ];
  return (
    <div style={{ padding: '0 16px 24px' }}>
      <div style={{ display: 'flex', gap: 8, marginBottom: 12 }}>
        <StatTile tone="warm" label="Due this month" value="£239"/>
        <StatTile tone="warm" label="Paid so far" value="£128"/>
      </div>
      <SectionHeader title="All bills" kicker="UK · Ghana · Nigeria"/>
      <div style={{ background: 'white', borderRadius: 16, border: `1px solid #F1E5D1`, padding: '4px 0' }}>
        {bills.map((b, i) => (
          <div key={i} style={{
            display: 'flex', alignItems: 'center', gap: 12, padding: '12px 16px',
            borderBottom: i === bills.length - 1 ? 'none' : '1px solid #F5EADB',
          }}>
            <div style={{ width: 36, height: 36, borderRadius: 8, background: b.bg, color: b.fg || 'white',
              display: 'flex', alignItems: 'center', justifyContent: 'center', font: `700 10px/14px ${PAY.font}` }}>{b.logo}</div>
            <div style={{ flex: 1 }}>
              <div style={{ font: `600 13px/18px ${PAY.font}`, color: PAY.ink }}>{b.n}</div>
              <div style={{ font: `400 11px/16px ${PAY.font}`, color: PAY.n500 }}>{b.d}</div>
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 4 }}>
              <div style={{ font: `700 13px/16px ${PAY.font}`, color: PAY.ink }}>{b.amt}</div>
              <PayChip tone={b.tone} style={{ fontSize: 9, padding: '2px 8px' }}>
                {b.tone === 'success' ? 'Paid' : b.tone === 'warning' ? 'Due' : 'Auto'}
              </PayChip>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function SpendingAccounts() {
  const accounts = [
    { n: 'Main · Monzo', sub: '•• 4521', bal: '£3,624.18', grad: 'linear-gradient(135deg,#FFF8F0 0%, #FFE9D4 100%)', cc: 'gb' },
    { n: 'Savings · Marcus', sub: '•• 8812', bal: '£14,210.50', grad: 'linear-gradient(135deg,#FFFBF6 0%, #F7EBDD 100%)', cc: 'gb' },
    { n: 'MTN MoMo · Ghana', sub: '•• 0277', bal: 'GHS 1,840.00', grad: 'linear-gradient(135deg,#FFFCF8 0%, #F6EDE3 100%)', cc: 'gh' },
    { n: 'Kuda · Nigeria', sub: '•• 4019', bal: 'NGN 85,200.00', grad: 'linear-gradient(135deg,#FFFCF8 0%, #F6EDE3 100%)', cc: 'ng' },
  ];
  return (
    <div style={{ padding: '0 16px 24px' }}>
      {accounts.map((a, i) => (
        <div key={i} style={{
          background: a.grad, borderRadius: 16, padding: 16, marginBottom: 10,
          border: '1px solid #F1E5D1', position: 'relative', overflow: 'hidden',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <div style={{ width: 24, height: 24, borderRadius: 50, backgroundImage: `url('assets/flags/${a.cc}.svg')`, backgroundSize: 'cover', boxShadow: '0 0 0 1.5px white' }}/>
            <div style={{ flex: 1 }}>
              <div style={{ font: `700 13px/18px ${PAY.font}`, color: PAY.ink }}>{a.n}</div>
              <div style={{ font: `400 11px/14px ${PAY.font}`, color: PAY.warm800 }}>{a.sub}</div>
            </div>
            <Icon name="chev" size={16} color={PAY.warm800}/>
          </div>
          <div style={{ font: `800 22px/26px ${PAY.font}`, color: PAY.ink, marginTop: 10, letterSpacing: -0.4 }}>{a.bal}</div>
        </div>
      ))}
      <div onClick={() => {}} style={{
        marginTop: 4, padding: 14, borderRadius: 16, background: 'white',
        border: `1.5px dashed ${PAY.warm500}`, display: 'flex', alignItems: 'center', gap: 10,
        cursor: 'pointer',
      }}>
        <Icon name="plus" size={20} color={PAY.orange}/>
        <div style={{ font: `700 12px/16px ${PAY.font}`, color: PAY.orange, textTransform: 'uppercase', letterSpacing: 0.6 }}>Link another account</div>
      </div>
    </div>
  );
}

// ─── Notifications screen ───────────────────────────────────────────────────
function NotificationsScreen({ onBack }) {
  return (
    <div style={{ background: payWarmScreen, height: '100%', overflow: 'auto' }}>
      <div style={{ padding: '12px 16px 8px', display: 'flex', alignItems: 'center', gap: 14 }}>
        <div onClick={onBack} style={{ cursor: 'pointer', width: 32, height: 32, display: 'flex', alignItems: 'center', justifyContent: 'center', color: PAY.warm900 }}><Icon name="back" size={22}/></div>
        <div style={{ flex: 1 }}>
          <div style={{ font: `400 11px/14px ${PAY.font}`, color: PAY.warm800, letterSpacing: 0.8, textTransform: 'uppercase' }}>Updates</div>
          <div style={{ font: `700 22px/26px ${PAY.font}`, color: PAY.warm900, letterSpacing: -0.3 }}>Notifications</div>
        </div>
        <div style={{ font: `700 11px/14px ${PAY.font}`, color: PAY.orange, textTransform: 'uppercase', letterSpacing: 0.6 }}>Mark all</div>
      </div>

      <div style={{ padding: '12px 16px' }}>
        <div style={{ font: `700 11px/14px ${PAY.font}`, color: PAY.warm800, letterSpacing: 1, textTransform: 'uppercase', marginBottom: 8 }}>Today</div>
        <NoteCard from="Simi" type="ai" title="Sky Broadband is due Thursday."
          body="I'll keep £50 aside from your main account on Wed evening." time="09:42 AM" cta="Confirm"/>
        <NoteCard from="MTN MoMo" type="alert" title="Ama Serwaa received GHS 2,330."
          body="Receipt #MM-78213 · GHS 19.42 per £" time="09:42 AM" flag="gh"/>
        <NoteCard from="Simi" type="ai" title="Transport up 22% this week."
          body="A weekend cap of £40 would put you back on track." time="08:14 AM" cta="Set cap"/>

        <div style={{ font: `700 11px/14px ${PAY.font}`, color: PAY.warm800, letterSpacing: 1, textTransform: 'uppercase', margin: '16px 0 8px' }}>Yesterday</div>
        <NoteCard from="Bloom Ltd" type="alert" title="Payday — £3,420.00 in."
          body="Net pay deposited to Monzo •• 4521." time="07:02 AM" tone="success"/>
        <NoteCard from="DStv" type="alert" title="Subscription renewed."
          body="NGN 18,500 charged · Kuda •• 4019" time="May 01" flag="ng"/>
        <NoteCard from="Simi" type="ai" title="You crossed £18k net worth."
          body="That's £412 up this month — nicely steady." time="May 01" tone="success"/>
      </div>
    </div>
  );
}

function NoteCard({ from, type, title, body, time, cta, flag, tone }) {
  const isAi = type === 'ai';
  return (
    <div style={{
      background: isAi ? 'linear-gradient(135deg, #FFE2C5 0%, #FFF7EC 100%)' : 'white',
      border: `1px solid ${isAi ? '#F1DEC9' : '#F1E5D1'}`,
      borderRadius: 16, padding: 14, marginBottom: 8,
      boxShadow: '0 2px 10px rgba(77,49,32,0.05)',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6 }}>
        {isAi ? (
          <div style={{
            width: 26, height: 26, borderRadius: 50,
            backgroundImage: "url('assets/simi.png')",
            backgroundSize: 'cover', backgroundPosition: '50% 25%',
            border: '1.5px solid rgba(243,121,32,0.6)',
          }}/>
        ) : flag ? (
          <div style={{ width: 26, height: 26, borderRadius: 50, backgroundImage: `url('assets/flags/${flag}.svg')`, backgroundSize: 'cover', boxShadow: '0 0 0 1.5px #F1E5D1' }}/>
        ) : (
          <div style={{
            width: 26, height: 26, borderRadius: 50, background: tone === 'success' ? PAY.success050 : '#F2F4F4',
            display: 'flex', alignItems: 'center', justifyContent: 'center', color: tone === 'success' ? '#1B7030' : PAY.warm800,
          }}><Icon name={tone === 'success' ? 'check' : 'bell'} size={14}/></div>
        )}
        <div style={{ font: `700 11px/14px ${PAY.font}`, color: isAi ? '#7A3211' : PAY.warm900, letterSpacing: 0.6, textTransform: 'uppercase' }}>{from}</div>
        <div style={{ flex: 1 }}/>
        <div style={{ font: `400 10px/14px ${PAY.font}`, color: PAY.warm800 }}>{time}</div>
      </div>
      <div style={{ font: `600 14px/20px ${PAY.font}`, color: PAY.warm900 }}>{title}</div>
      <div style={{ font: `400 12px/17px ${PAY.font}`, color: PAY.warm800, marginTop: 2 }}>{body}</div>
      {cta && <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
        <PayButton variant="primary" size="sm">{cta}</PayButton>
        <PayButton variant="link" size="sm">Snooze</PayButton>
      </div>}
    </div>
  );
}

Object.assign(window, { PayScreen, SpendingScreen, NotificationsScreen, ActionCard, QuickSend, TxnRow, TxnDateRow, RateCard });
