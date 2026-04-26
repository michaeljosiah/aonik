// Notifications screen — header tabs, filter rail, grouped feed.
// Sources: System, Agents, Mentions, Approvals, Billing.

function NotifIcon({ kind, color }) {
  const iconByKind = {
    system:   'cog',
    agent:    'sparkles',
    mention:  'at',
    approval: 'clipcheck',
    payment:  'wallet',
    security: 'shield',
    incident: 'warn',
    success:  'check',
  };
  return (
    <div style={{
      width: 36, height: 36, borderRadius: 10, flex: 'none',
      background: color + '18', color: color,
      display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
    }}>
      <Icon name={iconByKind[kind] || 'bell'} size={16}/>
    </div>
  );
}

function NotifCard({ n }) {
  return (
    <div style={{
      display: 'grid', gridTemplateColumns: 'auto 1fr auto', gap: 14,
      padding: '14px 18px',
      background: n.unread ? 'var(--surface)' : 'transparent',
      borderBottom: '1px solid var(--border-light)',
      position: 'relative',
    }}>
      {n.unread && <span style={{
        position: 'absolute', left: 6, top: '50%', transform: 'translateY(-50%)',
        width: 6, height: 6, borderRadius: 999, background: 'var(--brand-secondary)',
      }}/>}

      <NotifIcon kind={n.kind} color={n.color}/>

      <div style={{ minWidth: 0 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
          <span style={{ fontSize: 13, fontWeight: n.unread ? 600 : 500, color: 'var(--text-primary)' }}>{n.title}</span>
          {n.tag && <Pill tone={n.tag.tone} size="sm" dot={n.tag.dot}>{n.tag.label}</Pill>}
          {n.source && (
            <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)', display: 'inline-flex', alignItems: 'center', gap: 4 }}>
              <span style={{ width: 3, height: 3, borderRadius: 999, background: 'var(--text-tertiary)' }}/>
              {n.source}
            </span>
          )}
        </div>
        <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 4, lineHeight: 1.55 }}>{n.body}</div>

        {n.diff && (
          <div style={{
            marginTop: 8, background: 'var(--surface-inset)', borderRadius: 6, padding: '8px 10px',
            fontFamily: 'var(--font-mono)', fontSize: 11, lineHeight: 1.6,
          }}>
            {n.diff.map((l, i) => (
              <div key={i} style={{ color: l.t === 'add' ? 'var(--success)' : l.t === 'rm' ? 'var(--danger)' : 'var(--text-secondary)' }}>
                {l.t === 'add' ? '+ ' : l.t === 'rm' ? '- ' : '  '}{l.text}
              </div>
            ))}
          </div>
        )}

        {n.actions && (
          <div style={{ display: 'flex', gap: 6, marginTop: 10, flexWrap: 'wrap' }}>
            {n.actions.map((a, i) => (
              <button key={i} className={`btn btn-${a.kind || 'outline'} btn-sm`} style={{ height: 26, padding: '0 10px', fontSize: 11 }}>
                {a.icon && <Icon name={a.icon} size={11}/>}{a.label}
              </button>
            ))}
          </div>
        )}
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 6 }}>
        <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>{n.when}</span>
        <span className="hover-halo" style={{ width: 22, height: 22 }}><Icon name="more" size={12}/></span>
      </div>
    </div>
  );
}

function ScreenNotifications() {
  const [tab, setTab] = React.useState('All');
  const tabs = [
    { l: 'All',       count: 24 },
    { l: 'Unread',    count: 9  },
    { l: 'Mentions',  count: 3  },
    { l: 'Approvals', count: 2  },
    { l: 'System',    count: 6  },
    { l: 'Archived',  count: null },
  ];

  const today = [
    {
      kind: 'approval', color: 'var(--brand-secondary)', unread: true,
      title: 'Billing Agent · approval needed',
      tag: { label: 'Above policy', tone: 'pending', dot: true },
      source: 'Agent · billing-001',
      body: 'Wants to apply £74,200 from Wise inbound to INV-2058 (Northstar). Amount exceeds the £50K auto-apply ceiling.',
      diff: [
        { t: 'add', text: 'Cr  4000 Revenue · Freight · NGN  £74,200.00' },
        { t: 'add', text: 'Dr  1010 Cash at Bank · GBP        £74,200.00' },
        { t: 'ctx', text: 'memo: INV-2058 · Northstar Freight · 22 Apr' },
      ],
      actions: [
        { label: 'Approve & post', kind: 'primary', icon: 'check' },
        { label: 'Open in Journal' },
        { label: 'Decline' },
      ],
      when: '2m ago',
    },
    {
      kind: 'mention', color: '#7b76b6', unread: true,
      title: 'Maria Gomez mentioned you',
      source: 'Customers · Primrose',
      body: '"@oliver — looking at the April fuel overrun, do you want to bump the budget by £4K or send it to Ops review?"',
      actions: [
        { label: 'Reply', kind: 'primary', icon: 'send' },
        { label: 'View thread' },
      ],
      when: '14m ago',
    },
    {
      kind: 'agent', color: 'var(--brand-primary)', unread: true,
      title: 'Insights Agent · weekly summary ready',
      tag: { label: 'fresh', tone: 'tint' },
      source: 'agent · insights-002',
      body: 'Fuel category trending 14% over budget; NGN exposure doubled QoQ; receivables aging improving. 3 recommendations queued.',
      actions: [
        { label: 'Open summary', kind: 'primary' },
        { label: 'Snooze 7 days' },
      ],
      when: '38m ago',
    },
    {
      kind: 'incident', color: 'var(--warning)', unread: true,
      title: 'Bank feed degraded · Zenith Bank',
      tag: { label: 'investigating', tone: 'warning', dot: true },
      source: 'Observability · partners',
      body: 'NGN Settlement transactions delayed 6–12 minutes vs baseline. Reconciliation paused for that account; agent will retry hourly.',
      actions: [
        { label: 'View incident' },
        { label: 'Acknowledge' },
      ],
      when: '1h ago',
    },
    {
      kind: 'payment', color: 'var(--success)', unread: false,
      title: 'Payout settled · PO-0871',
      source: 'Wise · GBP→USD',
      body: '£8,400 → $10,512 settled to FX Buffer. FX rate locked at 1.252.',
      when: '2h ago',
    },
  ];

  const earlier = [
    {
      kind: 'security', color: 'var(--danger)', unread: false,
      title: 'New device signed in · London',
      source: 'Security · sso',
      body: 'Chrome on macOS · 89.142.18.4 · Tower Hamlets. If this wasn\'t you, revoke the session and rotate your password.',
      actions: [{ label: 'Revoke session', kind: 'outline' }, { label: 'It was me' }],
      when: '4h ago',
    },
    {
      kind: 'agent', color: 'var(--brand-primary)', unread: false,
      title: 'Reconciliation Agent · 38 of 40 matched',
      source: 'agent · ledger-007',
      body: '2 transactions could not be matched automatically. Review queue created.',
      actions: [{ label: 'Open review queue' }],
      when: '6h ago',
    },
    {
      kind: 'system', color: 'var(--text-secondary)', unread: false,
      title: 'Tenant export complete',
      source: 'System',
      body: 'April financial report · 14 files · 218MB ready in Downloads.',
      actions: [{ label: 'Download', icon: 'download' }],
      when: 'yesterday',
    },
    {
      kind: 'success', color: 'var(--success)', unread: false,
      title: 'KYB renewed · Primrose Logistics',
      source: 'Compliance',
      body: 'Annual re-screen passed. Sanctions and PEP checks clear. Next review: April 26, 2027.',
      when: 'yesterday',
    },
  ];

  return (
    <div style={{ display: 'flex', height: '100%' }}>
      {/* Filter rail */}
      <aside style={{
        width: 240, flex: 'none', background: 'var(--surface-inset)',
        borderRight: '1px solid var(--border-light)', padding: '20px 14px',
        overflow: 'auto',
      }}>
        <div style={{ fontSize: 10, letterSpacing: '0.08em', color: 'var(--text-tertiary)', textTransform: 'uppercase', fontWeight: 600, padding: '0 8px 8px' }}>
          Sources
        </div>
        {[
          { ic: 'bell',     l: 'All notifications', n: 24, c: 'var(--text-secondary)', active: true },
          { ic: 'sparkles', l: 'From agents',       n: 14, c: 'var(--brand-primary)' },
          { ic: 'cog',      l: 'System',            n:  6, c: 'var(--text-secondary)' },
          { ic: 'at',       l: 'Mentions',          n:  3, c: '#7b76b6' },
          { ic: 'clipcheck',l: 'Approvals needed',  n:  2, c: 'var(--brand-secondary)' },
          { ic: 'shield',   l: 'Security',          n:  1, c: 'var(--danger)' },
          { ic: 'wallet',   l: 'Payments',          n:  4, c: 'var(--success)' },
        ].map((s, i) => (
          <div key={i} style={{
            display: 'flex', alignItems: 'center', gap: 10, padding: '8px 10px',
            borderRadius: 7, cursor: 'pointer',
            background: s.active ? 'var(--surface)' : 'transparent',
            border: s.active ? '1px solid var(--border-light)' : '1px solid transparent',
            color: s.active ? 'var(--text-primary)' : 'var(--text-secondary)',
            fontWeight: s.active ? 500 : 400, fontSize: 12.5,
          }}>
            <Icon name={s.ic} size={14} color={s.c}/>
            <span style={{ flex: 1 }}>{s.l}</span>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>{s.n}</span>
          </div>
        ))}

        <div style={{ fontSize: 10, letterSpacing: '0.08em', color: 'var(--text-tertiary)', textTransform: 'uppercase', fontWeight: 600, padding: '20px 8px 8px' }}>
          Agents
        </div>
        {[
          { l: 'Billing Agent',         n: 4, c: 'var(--brand-primary)' },
          { l: 'Reconciliation Agent',  n: 3, c: 'var(--accent-jade)' },
          { l: 'Insights Agent',        n: 2, c: 'var(--accent-violet)' },
          { l: 'KYB Agent',             n: 1, c: 'var(--accent-team)' },
          { l: 'Treasury Agent',        n: 4, c: 'var(--brand-secondary)' },
        ].map((a, i) => (
          <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '7px 10px', fontSize: 12, color: 'var(--text-secondary)' }}>
            <span style={{ width: 6, height: 6, borderRadius: 999, background: a.c }}/>
            <span style={{ flex: 1 }}>{a.l}</span>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)' }}>{a.n}</span>
          </div>
        ))}

        <div style={{ marginTop: 22, padding: 12, background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8 }}>
          <div style={{ fontSize: 11, fontWeight: 600, marginBottom: 4 }}>Quiet hours</div>
          <div style={{ fontSize: 10.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>22:00 → 07:00 · only Approvals & Security break through.</div>
          <button className="btn btn-ghost btn-sm" style={{ marginTop: 8, height: 24, padding: '0 8px', fontSize: 11 }}>
            <Icon name="settings" size={11}/> Configure
          </button>
        </div>
      </aside>

      {/* Feed */}
      <main style={{ flex: 1, overflow: 'auto', display: 'flex', flexDirection: 'column' }}>
        <div style={{ padding: '24px 32px 0' }}>
          <div style={{ display: 'flex', alignItems: 'flex-end', gap: 14 }}>
            <div>
              <h1 style={{ fontFamily: 'var(--font-brand)', fontSize: 24, fontWeight: 700, letterSpacing: '-0.015em' }}>Notifications</h1>
              <p style={{ fontSize: 12.5, color: 'var(--text-secondary)', marginTop: 4 }}>9 unread · 2 awaiting your approval</p>
            </div>
            <div style={{ flex: 1 }}/>
            <button className="btn btn-outline btn-sm"><Icon name="check" size={12}/> Mark all read</button>
            <button className="btn btn-outline btn-sm"><Icon name="settings" size={12}/></button>
          </div>

          {/* Tabs */}
          <div style={{ display: 'flex', gap: 2, borderBottom: '1px solid var(--border-light)', marginTop: 20 }}>
            {tabs.map(t => {
              const a = t.l === tab;
              return (
                <button key={t.l} onClick={() => setTab(t.l)} className="btn btn-ghost"
                  style={{
                    height: 36, padding: '0 14px', fontSize: 12.5, borderRadius: 0,
                    borderBottom: a ? '2px solid var(--brand-primary)' : '2px solid transparent',
                    color: a ? 'var(--text-primary)' : 'var(--text-secondary)',
                    fontWeight: a ? 600 : 400, marginBottom: -1, gap: 6,
                  }}>
                  {t.l}
                  {t.count != null && (
                    <span style={{ fontSize: 10, padding: '1px 6px', borderRadius: 999, fontFamily: 'var(--font-mono)',
                      background: a ? 'var(--brand-primary-10)' : 'var(--surface-inset)',
                      color: a ? 'var(--brand-primary)' : 'var(--text-tertiary)' }}>{t.count}</span>
                  )}
                </button>
              );
            })}
          </div>
        </div>

        <div style={{ flex: 1, padding: '0 0 24px' }}>
          {/* Today */}
          <div style={{ padding: '14px 32px 6px', display: 'flex', alignItems: 'center', gap: 10 }}>
            <span style={{ fontSize: 11, fontWeight: 600, letterSpacing: '0.06em', textTransform: 'uppercase', color: 'var(--text-tertiary)' }}>Today</span>
            <span style={{ flex: 1, height: 1, background: 'var(--border-light)' }}/>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)' }}>{today.length}</span>
          </div>
          <div style={{ background: 'var(--surface)', margin: '0 24px', borderRadius: 12, border: '1px solid var(--border-light)', overflow: 'hidden' }}>
            {today.map((n, i) => <NotifCard key={i} n={n}/>)}
          </div>

          {/* Earlier */}
          <div style={{ padding: '24px 32px 6px', display: 'flex', alignItems: 'center', gap: 10 }}>
            <span style={{ fontSize: 11, fontWeight: 600, letterSpacing: '0.06em', textTransform: 'uppercase', color: 'var(--text-tertiary)' }}>Earlier this week</span>
            <span style={{ flex: 1, height: 1, background: 'var(--border-light)' }}/>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)' }}>{earlier.length}</span>
          </div>
          <div style={{ background: 'var(--surface)', margin: '0 24px', borderRadius: 12, border: '1px solid var(--border-light)', overflow: 'hidden' }}>
            {earlier.map((n, i) => <NotifCard key={i} n={n}/>)}
          </div>
        </div>
      </main>

      {/* Right: notification toast preview */}
      <aside style={{
        width: 320, flex: 'none', borderLeft: '1px solid var(--border-light)',
        background: 'var(--surface)', padding: '20px 18px', overflow: 'auto',
      }}>
        <div style={{ fontSize: 10, letterSpacing: '0.08em', color: 'var(--text-tertiary)', textTransform: 'uppercase', fontWeight: 600, marginBottom: 12 }}>
          Live · toast preview
        </div>

        {/* Toast: agent proposal */}
        <div style={{
          background: 'var(--surface)', border: '1px solid var(--border-light)',
          borderLeft: '3px solid var(--brand-secondary)', borderRadius: 10,
          padding: 14, boxShadow: '0 12px 28px -8px rgb(0 0 0 / 0.18)',
          marginBottom: 14,
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6 }}>
            <Avatar name="B" size={20} color="var(--brand-primary-10)" textColor="var(--brand-primary)"/>
            <span style={{ fontSize: 11, fontWeight: 600 }}>Billing Agent</span>
            <span style={{ marginLeft: 'auto', fontFamily: 'var(--font-mono)', fontSize: 9.5, color: 'var(--text-tertiary)' }}>just now</span>
          </div>
          <div style={{ fontSize: 12, color: 'var(--text-primary)', lineHeight: 1.5 }}>
            Approval needed: apply £74,200 from Wise inbound to <b>INV-2058</b>.
          </div>
          <div style={{ display: 'flex', gap: 6, marginTop: 8 }}>
            <button className="btn btn-secondary btn-sm" style={{ height: 24, padding: '0 8px', fontSize: 11 }}>Approve</button>
            <button className="btn btn-outline btn-sm" style={{ height: 24, padding: '0 8px', fontSize: 11 }}>Review</button>
          </div>
        </div>

        {/* Toast: success */}
        <div style={{
          background: 'var(--surface)', border: '1px solid var(--border-light)',
          borderLeft: '3px solid var(--success)', borderRadius: 10,
          padding: 12, boxShadow: '0 8px 20px -6px rgb(0 0 0 / 0.12)',
          marginBottom: 14, display: 'flex', alignItems: 'flex-start', gap: 10,
        }}>
          <Icon name="check" size={14} color="var(--success)"/>
          <div style={{ flex: 1 }}>
            <div style={{ fontSize: 12, fontWeight: 600 }}>Journal entry posted</div>
            <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 2 }}>JE-4082 · £12,480 · April 2026</div>
          </div>
          <span className="hover-halo" style={{ width: 20, height: 20 }}><Icon name="close" size={11}/></span>
        </div>

        {/* Toast: warning */}
        <div style={{
          background: 'var(--surface)', border: '1px solid var(--border-light)',
          borderLeft: '3px solid var(--warning)', borderRadius: 10,
          padding: 12, marginBottom: 14, display: 'flex', alignItems: 'flex-start', gap: 10,
        }}>
          <Icon name="warn" size={14} color="var(--warning)"/>
          <div style={{ flex: 1 }}>
            <div style={{ fontSize: 12, fontWeight: 600 }}>Bank feed degraded</div>
            <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 2 }}>Zenith Bank · NGN Settlement</div>
          </div>
        </div>

        <div style={{ marginTop: 22, fontSize: 10, letterSpacing: '0.08em', color: 'var(--text-tertiary)', textTransform: 'uppercase', fontWeight: 600, marginBottom: 10 }}>
          Delivery channels
        </div>
        {[
          { ic: 'bell',  l: 'In-app',       on: true },
          { ic: 'mail',  l: 'Email digest', on: true },
          { ic: 'phone', l: 'SMS · urgent', on: false },
          { ic: 'link',  l: 'Slack',        on: true, sub: '#fin-ops' },
        ].map((c, i) => (
          <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '8px 0', borderBottom: '1px solid var(--border-light)' }}>
            <Icon name={c.ic} size={13} color="var(--text-secondary)"/>
            <div style={{ flex: 1 }}>
              <div style={{ fontSize: 12, color: 'var(--text-primary)' }}>{c.l}</div>
              {c.sub && <div style={{ fontSize: 10, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{c.sub}</div>}
            </div>
            <span style={{
              width: 28, height: 16, borderRadius: 999, padding: 2, flex: 'none',
              background: c.on ? 'var(--brand-primary)' : 'var(--gray-300)',
              display: 'inline-flex', alignItems: 'center',
            }}>
              <span style={{
                width: 12, height: 12, borderRadius: 999, background: '#fff',
                transform: c.on ? 'translateX(12px)' : 'translateX(0)', transition: 'transform 150ms',
              }}/>
            </span>
          </div>
        ))}
      </aside>
    </div>
  );
}

Object.assign(window, { ScreenNotifications });
