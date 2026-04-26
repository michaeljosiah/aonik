// Compliance · Documents + Users · Access + Tenants + Agent Center

function ScreenCompliance() {
  const docs = [
    { id: 'DOC-4821', party: 'Primrose Logistics', type: 'Certificate of incorporation', status: 'verified',  tone: 'success', uploaded: '14 Apr', expires: '2028-03-14', verifier: 'Jumio' },
    { id: 'DOC-4820', party: 'Maria Obi',           type: 'National ID (NIN)',           status: 'pending',   tone: 'warning', uploaded: '22 Apr', expires: '2031-09-22', verifier: 'Onfido', agent: true, conf: 0.91 },
    { id: 'DOC-4819', party: 'Northstar Freight',   type: 'Beneficial ownership',        status: 'review',    tone: 'pending', uploaded: '20 Apr', expires: '—',         verifier: 'Manual', agent: true, conf: 0.72 },
    { id: 'DOC-4818', party: 'Apex Fabrication',    type: 'Proof of address',            status: 'verified',  tone: 'success', uploaded: '11 Apr', expires: '2026-10-11', verifier: 'Onfido' },
    { id: 'DOC-4817', party: 'Blue Harbor Co',      type: 'Tax certificate',             status: 'rejected',  tone: 'danger',  uploaded: '19 Apr', expires: '—',         verifier: 'Manual' },
    { id: 'DOC-4816', party: 'Samuel Okoro',        type: 'Passport',                    status: 'verified',  tone: 'success', uploaded: '15 Apr', expires: '2030-02-15', verifier: 'Jumio' },
  ];
  const cols = [
    { key: 'id', label: 'Doc ID', w: '110px', mono: true, weight: 500 },
    { key: 'party', label: 'Party', w: '1fr',
      render: r => <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
        <Avatar name={r.party} size={24} color={agentColor(r.party) + '22'} textColor={agentColor(r.party)}/>
        <span style={{ fontSize: 13, fontWeight: 500, color: 'var(--text-primary)' }}>{r.party}</span>
      </div> },
    { key: 'type', label: 'Type', w: '1fr',
      render: r => <span style={{ fontSize: 12, color: 'var(--text-secondary)', display: 'inline-flex', alignItems: 'center', gap: 6 }}>
        {r.agent && <Icon name="sparkles" size={11} color="var(--brand-primary)"/>}
        {r.type}
      </span> },
    { key: 'verifier', label: 'Verifier', w: '100px',
      render: r => <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)' }}>{r.verifier}</span> },
    { key: 'uploaded', label: 'Uploaded', w: '90px', mono: true, fontSize: 12,
      render: r => <span style={{ color: 'var(--text-secondary)' }}>{r.uploaded}</span> },
    { key: 'expires', label: 'Expires', w: '110px', mono: true, fontSize: 12,
      render: r => <span style={{ color: 'var(--text-secondary)' }}>{r.expires}</span> },
    { key: 'status', label: 'Status', w: '110px',
      render: r => <Pill tone={r.tone} dot>{r.status}</Pill> },
  ];

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader eyebrow="Operations · Compliance" title="Documents" subtitle="KYC · KYB · sanctions screening · 2 awaiting agent review"
        actions={<>
          <button className="btn btn-outline btn-sm"><Icon name="refresh" size={12}/> Re-screen</button>
          <button className="btn btn-primary btn-sm"><Icon name="upload" size={12}/> Upload</button>
        </>}/>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 14 }}>
        {[
          { l: 'Verified',  v: '1,842', tone: 'var(--success)' },
          { l: 'Pending',   v: '42',    tone: 'var(--warning)' },
          { l: 'In review', v: '8',     tone: 'var(--brand-secondary)' },
          { l: 'Rejected',  v: '14',    tone: 'var(--danger)' },
        ].map((s, i) => (
          <div key={i} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 7, fontSize: 11, color: 'var(--text-secondary)' }}>
              <span style={{ width: 6, height: 6, borderRadius: 999, background: s.tone }}/>{s.l}
            </div>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 22, fontWeight: 600, color: 'var(--text-primary)', marginTop: 4 }}>{s.v}</div>
          </div>
        ))}
      </div>
      <FilterBar tabs={['All', 'Verified', 'Pending', 'In review', 'Rejected']} active="All" search="Filter by party, type…"/>
      <DataTable cols={cols} rows={docs} footer={<TableFooter showing="1–6" total="1,906 documents" page={1} pages={318}/>}/>
    </div>
  );
}

function ScreenUsers() {
  const users = [
    { id: '1', name: 'Oliver Chen',      email: 'oliver@primrose.co',  role: 'Platform Admin',      status: 'active',   tone: 'success', last: '2m ago',  mfa: true,  color: '#7b76b6' },
    { id: '2', name: 'Maria Gomez',      email: 'maria@primrose.co',   role: 'Finance Manager',     status: 'active',   tone: 'success', last: '14m ago', mfa: true,  color: '#eb5c37' },
    { id: '3', name: 'David Lynn',       email: 'david@primrose.co',   role: 'Analyst',             status: 'active',   tone: 'success', last: '1h ago',  mfa: true,  color: '#055a60' },
    { id: '4', name: 'Kiran Desai',      email: 'kiran@primrose.co',   role: 'Operations',          status: 'active',   tone: 'success', last: '3h ago',  mfa: true,  color: '#3ab795' },
    { id: '5', name: 'Raj Patel',        email: 'raj@primrose.co',     role: 'Compliance Officer',  status: 'active',   tone: 'success', last: '5h ago',  mfa: true,  color: '#0097a9' },
    { id: '6', name: 'Amina Nkrumah',    email: 'amina@primrose.co',   role: 'Analyst',             status: 'pending',  tone: 'warning', last: 'never',    mfa: false, color: '#e8a838' },
    { id: '7', name: 'Jaya Lim',         email: 'jaya@primrose.co',    role: 'Read-only',           status: 'active',   tone: 'success', last: '2d ago',  mfa: false, color: '#5facbd' },
    { id: '8', name: 'Thandiwe Moyo',    email: 'thandiwe@primrose.co',role: 'Read-only',           status: 'suspended',tone: 'danger',  last: '1w ago',  mfa: true,  color: '#888' },
  ];
  const cols = [
    { key: 'name', label: 'User', w: '1.5fr',
      render: r => <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
        <Avatar name={r.name} size={30} color={r.color} textColor="#fff"/>
        <div>
          <div style={{ fontSize: 13, fontWeight: 500, color: 'var(--text-primary)' }}>{r.name}</div>
          <div style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{r.email}</div>
        </div>
      </div> },
    { key: 'role', label: 'Role', w: '1fr',
      render: r => <span style={{
        fontSize: 11, fontWeight: 500, padding: '3px 8px', borderRadius: 4,
        background: 'var(--surface-inset)', color: 'var(--text-primary)',
        border: '1px solid var(--border-light)',
      }}>{r.role}</span> },
    { key: 'mfa', label: 'MFA', w: '70px',
      render: r => r.mfa
        ? <Icon name="shield" size={14} color="var(--success)"/>
        : <Icon name="warn" size={14} color="var(--warning)"/> },
    { key: 'last', label: 'Last seen', w: '110px', mono: true, fontSize: 11,
      render: r => <span style={{ color: 'var(--text-secondary)' }}>{r.last}</span> },
    { key: 'status', label: 'Status', w: '120px',
      render: r => <Pill tone={r.tone} dot>{r.status}</Pill> },
  ];

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader eyebrow="Finance · Access" title="Users" subtitle="8 team members · 7 active · 1 pending invite · 1 suspended"
        actions={<>
          <button className="btn btn-outline btn-sm"><Icon name="shield" size={12}/> Roles</button>
          <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> Invite user</button>
        </>}/>
      <FilterBar tabs={['All', 'Active', 'Pending', 'Suspended']} active="All" search="Filter users…"/>
      <DataTable cols={cols} rows={users} footer={<TableFooter showing="1–8" total="8 users" page={1} pages={1}/>}/>
    </div>
  );
}

function ScreenTenants() {
  const tenants = [
    { name: 'Primrose Logistics', env: 'Prod',    status: 'Active',       tone: 'success', users: 42, currency: 'NGN·USD·GBP', region: 'EMEA', created: 'Jan 2025', mrr: '$4,200' },
    { name: 'Apex Fabrication',   env: 'Prod',    status: 'Active',       tone: 'success', users: 18, currency: 'GBP·EUR',     region: 'EMEA', created: 'Mar 2025', mrr: '$2,100' },
    { name: 'Meridian Studio',    env: 'Prod',    status: 'Active',       tone: 'success', users: 8,  currency: 'USD',         region: 'AMER', created: 'Apr 2025', mrr: '$980' },
    { name: 'Cedar Analytics',    env: 'Prod',    status: 'Active',       tone: 'success', users: 24, currency: 'USD·CAD',     region: 'AMER', created: 'May 2025', mrr: '$3,400' },
    { name: 'Northstar Freight',  env: 'Prod',    status: 'Suspended',    tone: 'danger',  users: 11, currency: 'USD·KES',     region: 'AFR',  created: 'Jun 2025', mrr: '—' },
    { name: 'Blue Harbor Co',     env: 'Staging', status: 'Provisioning', tone: 'pending', users: 2,  currency: 'USD·ZAR',     region: 'AFR',  created: '3d ago',  mrr: '—' },
    { name: 'Quill & Co · Dev',   env: 'Dev',     status: 'Active',       tone: 'muted',   users: 4,  currency: 'USD',         region: 'AMER', created: 'Aug 2025', mrr: '—' },
    { name: 'Orinoco Textiles',   env: 'Prod',    status: 'Active',       tone: 'success', users: 14, currency: 'USD·BRL',     region: 'AMER', created: 'Sep 2025', mrr: '$1,840' },
  ];

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader eyebrow="Admin · Host" title="Tenants" subtitle="8 tenants · 6 in prod · $12,520 MRR · 1 provisioning"
        actions={<>
          <button className="btn btn-outline btn-sm"><Icon name="download" size={12}/> Export</button>
          <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> New tenant</button>
        </>}/>
      <FilterBar tabs={['All', 'Prod', 'Staging', 'Dev']} active="All" search="Filter tenants…"
        extra={<button className="btn btn-ghost btn-sm"><Icon name="globe" size={12}/> All regions</button>}/>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14 }}>
        {tenants.map(t => (
          <div key={t.name} style={{
            background: 'var(--surface)', border: '1px solid var(--border-light)',
            borderRadius: 12, padding: 18, display: 'flex', flexDirection: 'column', gap: 12,
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
              <Avatar name={t.name} size={36} color={agentColor(t.name) + '22'} textColor={agentColor(t.name)}/>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{t.name}</div>
                <div style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{t.region} · {t.currency}</div>
              </div>
              <span style={{
                fontFamily: 'var(--font-mono)', fontSize: 10, fontWeight: 600,
                padding: '2px 7px', borderRadius: 4,
                background: t.env === 'Prod' ? 'var(--brand-primary-10)' : t.env === 'Staging' ? '#eb5c3720' : 'var(--surface-inset)',
                color: t.env === 'Prod' ? 'var(--brand-primary)' : t.env === 'Staging' ? 'var(--brand-secondary)' : 'var(--text-tertiary)',
              }}>{t.env}</span>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', paddingTop: 10, borderTop: '1px solid var(--border-light)' }}>
              <div>
                <div style={{ fontSize: 10, color: 'var(--text-tertiary)', letterSpacing: '0.04em', textTransform: 'uppercase', marginBottom: 2 }}>Users · MRR</div>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 13, color: 'var(--text-primary)', fontWeight: 500 }}>{t.users} · {t.mrr}</div>
              </div>
              <Pill tone={t.tone} dot>{t.status}</Pill>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function ScreenAgents() {
  const agents = [
    { id: 'orch',   name: 'Orchestrator',      color: '#055a60', role: 'Routes all agent traffic · 4 policies', runs: 1842, conf: 0.99, state: 'running', last: 'now' },
    { id: 'ledger', name: 'Ledger Agent',      color: '#055a60', role: 'Books + journal entries',               runs: 142,  conf: 0.96, state: 'idle',    last: '2m ago' },
    { id: 'billing',name: 'Billing Agent',     color: '#eb5c37', role: 'Invoices + matching',                    runs: 318,  conf: 0.94, state: 'running', last: 'now' },
    { id: 'payout', name: 'Payout Router',     color: '#3ab795', role: 'Rails + FX + partners',                  runs: 84,   conf: 0.91, state: 'idle',    last: '12m ago' },
    { id: 'compl',  name: 'Compliance Agent',  color: '#7b76b6', role: 'KYC + sanctions + audit',                runs: 42,   conf: 0.98, state: 'idle',    last: '1h ago' },
    { id: 'close',  name: 'Close Agent',       color: '#0097a9', role: 'Month-end close orchestrator',           runs: 6,    conf: 0.89, state: 'running', last: 'now' },
    { id: 'dunn',   name: 'Dunning Agent',     color: '#5facbd', role: 'Overdue outreach',                       runs: 28,   conf: 0.87, state: 'paused',  last: '3h ago' },
  ];
  const stateDot = s => ({ running: { c:'var(--success)', t:'Running' }, idle:{ c:'var(--gray-400)', t:'Idle' }, paused:{ c:'var(--warning)', t:'Paused' }}[s]);

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader eyebrow="Admin · Agents" title="Agent Command Center" subtitle="7 agents · 620 ops today · 0.93 avg confidence · 3 awaiting review"
        actions={<>
          <button className="btn btn-outline btn-sm"><Icon name="refresh" size={14}/> Re-sync tools</button>
          <button className="btn btn-primary btn-sm"><Icon name="plus" size={14}/> New agent</button>
        </>}/>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 16 }}>
        <KPI label="Ops today"        value="620"  delta="+18%"  deltaTone="up"
             spark="0,22 10,20 20,18 30,16 40,17 50,14 60,12 70,10 80,9 90,6 100,5" sparkColor="#055a60"/>
        <KPI label="Avg confidence"   value="0.93" delta="+0.02" deltaTone="up"
             spark="0,18 15,16 30,15 45,14 60,13 75,12 90,11 100,10" sparkColor="#3ab795"/>
        <KPI label="Auto-applied"     value="74%"  delta="+3%"   deltaTone="up"
             spark="0,20 15,18 30,15 45,16 60,12 75,10 90,8 100,7" sparkColor="#7b76b6"/>
        <KPI label="Interventions"    value="12"   delta="-4"    deltaTone="up"
             spark="0,10 15,12 30,10 45,14 60,12 75,15 90,18 100,20" sparkColor="#eb5c37"/>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 20 }}>
        <Card title="Agent roster" subtitle="Orchestrator + domain agents">
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginTop: 4 }}>
            {agents.map(a => {
              const st = stateDot(a.state);
              return (
                <div key={a.id} style={{
                  display: 'grid', gridTemplateColumns: 'auto 1fr auto auto', gap: 14,
                  alignItems: 'center', padding: '12px 14px',
                  background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10,
                }}>
                  <Avatar name={a.name} size={34} color={a.color + '22'} textColor={a.color}/>
                  <div>
                    <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{a.name}</div>
                    <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 1 }}>{a.role}</div>
                  </div>
                  <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)' }}>
                    <div style={{ color: 'var(--text-primary)', fontWeight: 600 }}>{a.runs} runs</div>
                    <div>conf {a.conf.toFixed(2)}</div>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                    <span style={{ width: 8, height: 8, borderRadius: 999, background: st.c }}/>
                    <span style={{ fontSize: 11, color: 'var(--text-secondary)', minWidth: 52 }}>{st.t}</span>
                  </div>
                </div>
              );
            })}
          </div>
        </Card>

        <Card title="Live trace · Billing Agent" subtitle="Run ra_88421 · matching INV-2041 ↔ bank_txn_9f2c1a"
          action={<Pill tone="tint" dot>running</Pill>}>
          <div style={{ display: 'flex', flexDirection: 'column', marginTop: 4 }}>
            {[
              { n:1, t:'search_invoices',        d:'filter: status=open, ref~2041', ms:'142ms', state:'done' },
              { n:2, t:'list_bank_transactions', d:'window: 14–21 Apr, amt ≈ 12480',ms:'318ms', state:'done' },
              { n:3, t:'match_invoice_to_txn',   d:'score: 0.94 · ref+amount+party',ms:'211ms', state:'done' },
              { n:4, t:'draft_journal_entry',    d:'composing balanced debit/credit…', ms:'—', state:'active' },
              { n:5, t:'propose_apply',          d:'awaiting human confirmation',      ms:'—', state:'pending' },
            ].map((s, i, arr) => (
              <div key={i} style={{
                display: 'grid', gridTemplateColumns: '24px 1fr auto', gap: 12, alignItems: 'center',
                padding: s.state === 'active' ? '12px 12px' : '12px 4px',
                borderBottom: i < arr.length - 1 ? '1px solid var(--border-light)' : 'none',
                background: s.state === 'active' ? 'var(--brand-primary-10)' : 'transparent',
                margin: s.state === 'active' ? '0 -12px' : '0',
                borderRadius: s.state === 'active' ? 8 : 0,
              }}>
                <span style={{
                  width: 22, height: 22, borderRadius: 999,
                  background: s.state === 'done' ? 'var(--success)' : s.state === 'active' ? 'var(--brand-primary)' : 'var(--gray-200)',
                  color: s.state === 'pending' ? 'var(--gray-500)' : '#fff',
                  display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                  fontSize: 10, fontWeight: 600, fontFamily: 'var(--font-mono)',
                }}>{s.state === 'done' ? '✓' : s.n}</span>
                <div>
                  <div style={{ fontSize: 13, color: 'var(--text-primary)', fontWeight: 500, fontFamily: 'var(--font-mono)' }}>{s.t}</div>
                  <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 2, fontFamily: 'var(--font-mono)' }}
                       className={s.state === 'active' ? 'shimmer' : ''}>{s.d}</div>
                </div>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)' }}>{s.ms}</div>
              </div>
            ))}
          </div>
        </Card>
      </div>

      <Card title="Policies in force" subtitle="Guardrails applied to every run">
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14, marginTop: 4 }}>
          {[
            { t: 'Confidence threshold', d: 'Auto-apply only when agent confidence ≥ 0.95', v: '0.95' },
            { t: 'Amount ceiling',       d: 'Human review required above $50,000',          v: '$50K' },
            { t: 'Dual-control payouts', d: 'Two approvers required for outbound payouts',  v: 'On' },
            { t: 'FX policy band',       d: 'Flag if rate deviates > 2% from reference',    v: '2%' },
            { t: 'PII redaction',        d: 'Customer PII stripped from all prompts',        v: 'On' },
            { t: 'Audit log retention',  d: 'Immutable log of every tool call · 7 years',   v: '7y' },
          ].map((p, i) => (
            <div key={i} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: 14, display: 'flex', flexDirection: 'column', gap: 6 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{p.t}</div>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, fontWeight: 600, color: 'var(--brand-primary)', background: 'var(--brand-primary-10)', padding: '1px 8px', borderRadius: 999 }}>{p.v}</span>
              </div>
              <div style={{ fontSize: 11, color: 'var(--text-secondary)', lineHeight: 1.5 }}>{p.d}</div>
            </div>
          ))}
        </div>
      </Card>
    </div>
  );
}

Object.assign(window, { ScreenCompliance, ScreenUsers, ScreenTenants, ScreenAgents });
