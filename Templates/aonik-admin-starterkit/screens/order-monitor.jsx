// ─── Order item monitor + enhanced order list with expandable rows ─────────

// ── Shared data ────────────────────────────────────────────────────────────
const MON_ORDERS = [
  {
    id: 'ORD-20250422-0047', invoice: 'INV-0047', status: 'Awaiting payment', tone: 'pending',
    payer: 'Primrose Logistics', payerColor: '#055a60', created: '22 Apr 14:22', gbpTotal: 5017.55,
    items: [
      { id: 'ITM-0047-01', label: 'Ikeja Electric',  symbol: 'IE', color: '#1e4d8c', type: 'Bill pay',  amount: 15000, cur: 'NGN', stage: 'Payment pending', tone: 'pending',  problem: false },
      { id: 'ITM-0047-02', label: 'GTBank',           symbol: 'GT', color: '#f26522', type: 'Transfer',  amount: 5000,  cur: 'GBP', stage: 'Compliance',      tone: 'warning',  problem: true,  issue: 'AML check — awaiting result' },
      { id: 'ITM-0047-03', label: 'DSTV',             symbol: 'DS', color: '#003087', type: 'Bill pay',  amount: 24500, cur: 'NGN', stage: 'Payment pending', tone: 'pending',  problem: false },
    ],
  },
  {
    id: 'ORD-20250421-0046', invoice: 'INV-0046', status: 'Processing', tone: 'tint',
    payer: 'Northstar Freight', payerColor: '#0097a9', created: '21 Apr 09:14', gbpTotal: 12840.00,
    items: [
      { id: 'ITM-0046-01', label: 'GTBank',   symbol: 'GT', color: '#f26522', type: 'Transfer', amount: 8200,  cur: 'GBP', stage: 'Processing',  tone: 'tint',    problem: false },
      { id: 'ITM-0046-02', label: 'Zenith Bank', symbol: 'ZB', color: '#cc0000', type: 'Transfer', amount: 4640, cur: 'GBP', stage: 'Processing', tone: 'tint',   problem: false },
    ],
  },
  {
    id: 'ORD-20250419-0045', invoice: 'INV-0045', status: 'Settled', tone: 'success',
    payer: 'Primrose Logistics', payerColor: '#055a60', created: '19 Apr 11:30', gbpTotal: 3200.00,
    items: [
      { id: 'ITM-0045-01', label: 'MTN', symbol: 'MT', color: '#e6b800', type: 'Bill pay', amount: 6350000, cur: 'NGN', stage: 'Settled', tone: 'success', problem: false },
    ],
  },
  {
    id: 'ORD-20250415-0043', invoice: 'INV-0043', status: 'Overdue', tone: 'danger',
    payer: 'Primrose Logistics', payerColor: '#055a60', created: '15 Apr 08:12', gbpTotal: 28400.00,
    items: [
      { id: 'ITM-0043-01', label: 'GTBank',      symbol: 'GT', color: '#f26522', type: 'Transfer', amount: 10000, cur: 'GBP', stage: 'Payment overdue', tone: 'danger', problem: true, issue: 'Payment not received — 7 days overdue' },
      { id: 'ITM-0043-02', label: 'First Bank',  symbol: 'FB', color: '#003366', type: 'Transfer', amount: 8200,  cur: 'GBP', stage: 'Payment overdue', tone: 'danger', problem: true, issue: 'Waiting on payer — chased x2' },
      { id: 'ITM-0043-03', label: 'UBA',         symbol: 'UB', color: '#c00000', type: 'Transfer', amount: 6100,  cur: 'GBP', stage: 'On hold',         tone: 'warning', problem: true, issue: 'Blocked pending INV-0043 payment' },
      { id: 'ITM-0043-04', label: 'Zenith Bank', symbol: 'ZB', color: '#cc0000', type: 'Transfer', amount: 4100,  cur: 'GBP', stage: 'On hold',         tone: 'warning', problem: true, issue: 'Blocked pending INV-0043 payment' },
    ],
  },
];

function monFmt(n, cur) {
  const s = { GBP: '£', NGN: '₦', USD: '$' }[cur] || cur + ' ';
  return s + Number(n).toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}
function monInitials(n) { return (n||'?').split(' ').map(w=>w[0]).slice(0,2).join('').toUpperCase(); }

function MonAvatar({ name, color, size = 26 }) {
  return (
    <div style={{ width: size, height: size, borderRadius: size*0.28, background: color, color: '#fff', flex: 'none', display: 'flex', alignItems: 'center', justifyContent: 'center', fontFamily: 'var(--font-brand)', fontWeight: 700, fontSize: size*0.38 }}>{monInitials(name)}</div>
  );
}
function MonLogo({ symbol, color, size = 26 }) {
  return (
    <div style={{ width: size, height: size, borderRadius: 5, background: color, color: '#fff', flex: 'none', display: 'flex', alignItems: 'center', justifyContent: 'center', fontFamily: 'var(--font-brand)', fontWeight: 800, fontSize: size*0.32 }}>{symbol}</div>
  );
}
function MonPulse({ color = 'var(--warning)', size = 9 }) {
  return (
    <span style={{ position: 'relative', display: 'inline-flex', width: size, height: size, flex: 'none' }}>
      <span style={{ position: 'absolute', inset: 0, borderRadius: '50%', background: color, opacity: 0.4, animation: 'pulse-ring 1.4s ease-out infinite' }}/>
      <span style={{ width: '100%', height: '100%', borderRadius: '50%', background: color, display: 'block' }}/>
      <style>{`@keyframes pulse-ring { 0%{transform:scale(1);opacity:0.4;} 70%{transform:scale(2.4);opacity:0;} 100%{transform:scale(2.4);opacity:0;} }`}</style>
    </span>
  );
}

// ── Stage pipeline mini (horizontal dots) ─────────────────────────────────
const STAGE_ORDER = ['Order created','Invoice generated','Compliance','Payment pending','Processing','Settled'];
function MiniPipeline({ stage, tone }) {
  const idx = STAGE_ORDER.findIndex(s => stage.toLowerCase().includes(s.toLowerCase().split(' ')[0]));
  const cur = idx >= 0 ? idx : 0;
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 3 }}>
      {STAGE_ORDER.map((s, i) => {
        const done = i < cur;
        const active = i === cur;
        const bad = active && (tone === 'danger' || tone === 'warning');
        return (
          <React.Fragment key={s}>
            {i > 0 && <div style={{ width: 10, height: 2, borderRadius: 1, background: done ? 'var(--brand-primary)' : 'var(--border-light)', flex: 'none' }}/>}
            <div style={{
              width: active ? 10 : 7, height: active ? 10 : 7,
              borderRadius: '50%', flex: 'none',
              background: done ? 'var(--brand-primary)' : active ? (bad ? 'var(--warning)' : 'var(--brand-primary)') : 'var(--border-light)',
              border: active ? (bad ? '1.5px solid var(--warning)' : '1.5px solid var(--brand-primary)') : 'none',
              boxShadow: active ? '0 0 0 3px ' + (bad ? 'rgba(235,195,52,0.25)' : 'rgba(5,90,96,0.2)') : 'none',
            }}/>
          </React.Fragment>
        );
      })}
    </div>
  );
}

// ── Enhanced order list with expandable rows ───────────────────────────────
function ScreenOrderListEnhanced() {
  const [expanded, setExpanded] = React.useState(new Set(['ORD-20250422-0047']));
  const [statusFilter, setStatusFilter] = React.useState('All');
  const problemCount = MON_ORDERS.flatMap(o => o.items).filter(i => i.problem).length;

  const toggle = id => setExpanded(prev => {
    const next = new Set(prev);
    next.has(id) ? next.delete(id) : next.add(id);
    return next;
  });

  const filtered = statusFilter === 'All' ? MON_ORDERS
    : statusFilter === 'Problems' ? MON_ORDERS.filter(o => o.items.some(i => i.problem))
    : MON_ORDERS.filter(o => o.status === statusFilter);

  return (
    <div style={{ padding: '22px 28px', height: '100%', overflow: 'auto', display: 'flex', flexDirection: 'column', gap: 16 }}>
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between' }}>
        <div>
          <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Orders</div>
          <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>Click any order to inspect its items inline.</div>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          {problemCount > 0 && (
            <button onClick={() => setStatusFilter('Problems')} className="btn btn-sm" style={{ background: 'rgba(204,46,46,0.1)', color: 'var(--danger)', border: '1px solid var(--danger)' }}>
              <MonPulse color="var(--danger)" size={7}/> {problemCount} item{problemCount !== 1 ? 's' : ''} need attention
            </button>
          )}
          <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> New order</button>
        </div>
      </div>

      {/* Filter pills */}
      <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
        {['All', 'Awaiting payment', 'Processing', 'Settled', 'Overdue', 'Problems'].map(f => (
          <button key={f} onClick={() => setStatusFilter(f)} style={{
            padding: '5px 12px', borderRadius: 999, fontSize: 12, fontWeight: 500, cursor: 'pointer',
            background: statusFilter === f ? (f === 'Problems' ? 'var(--danger)' : 'var(--brand-primary)') : 'var(--surface)',
            color: statusFilter === f ? '#fff' : 'var(--text-secondary)',
            border: statusFilter === f ? 'none' : '1px solid var(--border-light)',
          }}>{f}</button>
        ))}
      </div>

      {/* Order rows with expandable items */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {filtered.map(order => {
          const isOpen = expanded.has(order.id);
          const problems = order.items.filter(i => i.problem);
          return (
            <div key={order.id} style={{ background: 'var(--surface)', border: `1px solid ${problems.length > 0 ? 'rgba(204,46,46,0.3)' : 'var(--border-light)'}`, borderRadius: 12, overflow: 'hidden' }}>
              {/* Order header row */}
              <div onClick={() => toggle(order.id)} style={{
                display: 'grid', gridTemplateColumns: '28px 1fr 160px 120px 120px 100px 110px',
                alignItems: 'center', gap: 12, padding: '14px 16px', cursor: 'pointer',
                background: isOpen ? 'var(--surface-inset)' : 'transparent',
                transition: 'background 150ms',
              }}>
                {/* Expand chevron */}
                <span style={{ color: 'var(--text-tertiary)', transform: isOpen ? 'rotate(90deg)' : 'none', transition: 'transform 150ms', display: 'flex', justifyContent: 'center' }}>
                  <Icon name="chevron" size={14}/>
                </span>

                {/* Order ID + payer */}
                <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                  <MonAvatar name={order.payer} color={order.payerColor} size={28}/>
                  <div>
                    <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{order.id}</div>
                    <div style={{ fontSize: 11.5, color: 'var(--text-secondary)' }}>{order.payer}</div>
                  </div>
                </div>

                {/* Item logo strip */}
                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <div style={{ display: 'flex' }}>
                    {order.items.slice(0, 4).map((it, i) => (
                      <div key={it.id} style={{
                        width: 24, height: 24, borderRadius: 5, background: it.color, color: '#fff',
                        display: 'flex', alignItems: 'center', justifyContent: 'center',
                        fontFamily: 'var(--font-brand)', fontWeight: 800, fontSize: 8,
                        marginLeft: i > 0 ? -6 : 0, border: '1.5px solid var(--surface)',
                        position: 'relative', zIndex: 4 - i,
                      }}>{it.symbol}</div>
                    ))}
                    {order.items.length > 4 && (
                      <div style={{ width: 24, height: 24, borderRadius: 5, background: 'var(--surface-inset)', border: '1.5px solid var(--border-light)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 9, color: 'var(--text-tertiary)', marginLeft: -6 }}>+{order.items.length - 4}</div>
                    )}
                  </div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
                    <span style={{ fontSize: 12, color: 'var(--text-primary)', fontWeight: 500 }}>{order.items.length} item{order.items.length !== 1 ? 's' : ''}</span>
                    {problems.length > 0 && (
                      <span style={{ fontSize: 10.5, color: 'var(--danger)', display: 'flex', alignItems: 'center', gap: 3, fontWeight: 500 }}>
                        <MonPulse color="var(--danger)" size={6}/> {problems.length} problem{problems.length !== 1 ? 's' : ''}
                      </span>
                    )}
                  </div>
                </div>

                {/* Status */}
                <div><Pill tone={order.tone} dot size="sm">{order.status}</Pill></div>

                {/* Invoice */}
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-secondary)', display: 'flex', alignItems: 'center', gap: 5 }}>
                  <Icon name="invoice" size={11} color="var(--text-tertiary)"/>
                  {order.invoice}
                </div>

                {/* Date */}
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-tertiary)' }}>{order.created}</div>

                {/* Total */}
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 13, fontWeight: 700, color: 'var(--text-primary)', textAlign: 'right' }}>
                  {monFmt(order.gbpTotal, 'GBP')}
                </div>
              </div>

              {/* ── Expanded item rows ── */}
              {isOpen && (
                <div style={{ borderTop: '1px solid var(--border-light)' }}>
                  {/* Item column headers */}
                  <div style={{ display: 'grid', gridTemplateColumns: '28px 200px 90px 200px 140px 110px 80px', alignItems: 'center', gap: 10, padding: '8px 16px', background: 'rgba(0,0,0,0.02)', borderBottom: '1px solid var(--border-light)' }}>
                    {['', 'Item', 'Type', 'Pipeline stage', 'Status', 'Amount', ''].map((h, i) => (
                      <div key={i} style={{ fontSize: 10, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.07em', color: 'var(--text-tertiary)' }}>{h}</div>
                    ))}
                  </div>

                  {order.items.map((item, idx) => (
                    <div key={item.id} style={{
                      display: 'grid', gridTemplateColumns: '28px 200px 90px 200px 140px 110px 80px',
                      alignItems: 'center', gap: 10, padding: '11px 16px',
                      borderBottom: idx < order.items.length - 1 ? '1px solid var(--border-light)' : 'none',
                      background: item.problem ? 'rgba(204,46,46,0.03)' : 'transparent',
                    }}>
                      {/* Problem indicator */}
                      <div style={{ display: 'flex', justifyContent: 'center' }}>
                        {item.problem
                          ? <MonPulse color={item.tone === 'danger' ? 'var(--danger)' : 'var(--warning)'} size={8}/>
                          : <div style={{ width: 8, height: 8, borderRadius: '50%', background: item.tone === 'success' ? 'var(--success)' : 'var(--border-medium)' }}/>}
                      </div>

                      {/* Logo + label */}
                      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                        <MonLogo symbol={item.symbol} color={item.color} size={28}/>
                        <div>
                          <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{item.label}</div>
                          <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)' }}>{item.id}</div>
                        </div>
                      </div>

                      {/* Type */}
                      <div><Pill tone={item.type === 'Transfer' ? 'tint' : 'default'} size="sm">{item.type}</Pill></div>

                      {/* Pipeline mini */}
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                        <MiniPipeline stage={item.stage} tone={item.tone}/>
                        {item.problem && (
                          <div style={{ fontSize: 10.5, color: item.tone === 'danger' ? 'var(--danger)' : 'var(--warning)', fontWeight: 500, lineHeight: 1.3 }}>
                            {item.issue}
                          </div>
                        )}
                      </div>

                      {/* Status */}
                      <div><Pill tone={item.tone} dot size="sm">{item.stage}</Pill></div>

                      {/* Amount */}
                      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)', textAlign: 'right' }}>
                        {monFmt(item.amount, item.cur)}
                      </div>

                      {/* Actions */}
                      <div style={{ display: 'flex', gap: 4, justifyContent: 'flex-end' }}>
                        <span className="hover-halo" title="View item detail"><Icon name="arrowright" size={13} color="var(--text-secondary)"/></span>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}

// ── Item monitor — dedicated cross-order items view ────────────────────────
const ALL_ITEMS = MON_ORDERS.flatMap(o => o.items.map(i => ({ ...i, orderId: o.id, invoice: o.invoice, payer: o.payer, payerColor: o.payerColor })));

function ScreenOrderItemMonitor() {
  const [typeFilter, setTypeFilter] = React.useState('All');
  const [stageFilter, setStageFilter] = React.useState('All');
  const [problemOnly, setProblemOnly] = React.useState(false);

  const problems = ALL_ITEMS.filter(i => i.problem);
  const filtered = ALL_ITEMS.filter(i =>
    (typeFilter === 'All' || i.type === typeFilter) &&
    (stageFilter === 'All' || i.stage.includes(stageFilter)) &&
    (!problemOnly || i.problem)
  );

  const stageCounts = {};
  ALL_ITEMS.forEach(i => { stageCounts[i.stage] = (stageCounts[i.stage] || 0) + 1; });

  return (
    <div style={{ padding: '22px 28px', height: '100%', overflow: 'auto', display: 'flex', flexDirection: 'column', gap: 16 }}>
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between' }}>
        <div>
          <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Item monitor</div>
          <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>All order items across all orders — filter by type, stage, or flag issues.</div>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button onClick={() => setProblemOnly(p => !p)} className="btn btn-sm" style={{ background: problemOnly ? 'rgba(204,46,46,0.1)' : 'var(--surface)', color: problemOnly ? 'var(--danger)' : 'var(--text-secondary)', border: `1px solid ${problemOnly ? 'var(--danger)' : 'var(--border-light)'}` }}>
            <MonPulse color={problemOnly ? 'var(--danger)' : 'var(--text-tertiary)'} size={7}/>
            {problems.length} problems{problemOnly ? ' (active filter)' : ''}
          </button>
          <button className="btn btn-ghost btn-sm"><Icon name="download" size={12}/> Export</button>
        </div>
      </div>

      {/* Stage summary cards */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(6, 1fr)', gap: 10 }}>
        {[
          { stage: 'Compliance',      tone: 'warning', icon: 'shield' },
          { stage: 'Payment pending', tone: 'pending', icon: 'bank' },
          { stage: 'Processing',      tone: 'tint',    icon: 'refresh' },
          { stage: 'Payment overdue', tone: 'danger',  icon: 'warn' },
          { stage: 'On hold',         tone: 'warning', icon: 'warn' },
          { stage: 'Settled',         tone: 'success', icon: 'check' },
        ].map(s => {
          const count = ALL_ITEMS.filter(i => i.stage === s.stage).length;
          const active = stageFilter === s.stage;
          return (
            <div key={s.stage} onClick={() => setStageFilter(active ? 'All' : s.stage)} style={{
              background: 'var(--surface)', border: active ? '2px solid var(--brand-primary)' : '1px solid var(--border-light)',
              borderRadius: 10, padding: '12px 14px', cursor: 'pointer',
              background: active ? 'var(--brand-primary-10)' : 'var(--surface)',
            }}>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 6 }}>
                <Icon name={s.icon} size={14} color={
                  s.tone === 'danger' ? 'var(--danger)' : s.tone === 'warning' ? 'var(--warning)' :
                  s.tone === 'success' ? 'var(--success)' : 'var(--brand-primary)'
                }/>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 18, fontWeight: 700, color: 'var(--text-primary)' }}>{count}</div>
              </div>
              <div style={{ fontSize: 11, color: 'var(--text-secondary)', lineHeight: 1.3 }}>{s.stage}</div>
            </div>
          );
        })}
      </div>

      {/* Filters */}
      <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
        <div style={{ display: 'flex', gap: 5 }}>
          {['All', 'Bill pay', 'Transfer'].map(t => (
            <button key={t} onClick={() => setTypeFilter(t)} style={{
              padding: '5px 12px', borderRadius: 999, fontSize: 12, fontWeight: 500, cursor: 'pointer',
              background: typeFilter === t ? 'var(--brand-primary)' : 'var(--surface)',
              color: typeFilter === t ? '#fff' : 'var(--text-secondary)',
              border: typeFilter === t ? 'none' : '1px solid var(--border-light)',
            }}>{t}</button>
          ))}
        </div>
        <div style={{ height: 16, width: 1, background: 'var(--border-light)' }}/>
        <div style={{ fontSize: 12, color: 'var(--text-tertiary)' }}>{filtered.length} of {ALL_ITEMS.length} items</div>
        {(stageFilter !== 'All' || typeFilter !== 'All' || problemOnly) && (
          <button onClick={() => { setStageFilter('All'); setTypeFilter('All'); setProblemOnly(false); }} className="btn btn-ghost btn-sm"><Icon name="x" size={11}/> Clear filters</button>
        )}
      </div>

      {/* Items table */}
      <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, overflow: 'hidden' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr style={{ background: 'var(--surface-inset)' }}>
              {['', 'Item', 'Order', 'Payer', 'Type', 'Pipeline', 'Status', 'Amount', ''].map(h => (
                <th key={h} style={{ padding: '10px 14px', fontSize: 10.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.07em', color: 'var(--text-tertiary)', textAlign: h === 'Amount' ? 'right' : 'left', whiteSpace: 'nowrap' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {filtered.map((item, idx) => (
              <tr key={item.id} style={{ borderTop: '1px solid var(--border-light)', background: item.problem ? 'rgba(204,46,46,0.025)' : 'transparent' }}>
                {/* Problem dot */}
                <td style={{ padding: '12px 14px', width: 28 }}>
                  {item.problem
                    ? <MonPulse color={item.tone === 'danger' ? 'var(--danger)' : 'var(--warning)'} size={9}/>
                    : <div style={{ width: 9, height: 9, borderRadius: '50%', background: item.tone === 'success' ? 'var(--success)' : 'var(--border-light)' }}/>}
                </td>
                {/* Item */}
                <td style={{ padding: '12px 14px' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                    <MonLogo symbol={item.symbol} color={item.color} size={30}/>
                    <div>
                      <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{item.label}</div>
                      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>{item.id}</div>
                    </div>
                  </div>
                </td>
                {/* Order */}
                <td style={{ padding: '12px 14px' }}>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-secondary)' }}>{item.orderId.slice(-9)}</div>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>{item.invoice}</div>
                </td>
                {/* Payer */}
                <td style={{ padding: '12px 14px' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
                    <MonAvatar name={item.payer} color={item.payerColor} size={22}/>
                    <span style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}>{item.payer}</span>
                  </div>
                </td>
                {/* Type */}
                <td style={{ padding: '12px 14px' }}>
                  <Pill tone={item.type === 'Transfer' ? 'tint' : 'default'} size="sm">{item.type}</Pill>
                </td>
                {/* Pipeline */}
                <td style={{ padding: '12px 14px' }}>
                  <MiniPipeline stage={item.stage} tone={item.tone}/>
                  {item.problem && (
                    <div style={{ fontSize: 10.5, color: item.tone === 'danger' ? 'var(--danger)' : 'var(--warning)', marginTop: 3, fontWeight: 500, maxWidth: 180, lineHeight: 1.3 }}>{item.issue}</div>
                  )}
                </td>
                {/* Status */}
                <td style={{ padding: '12px 14px' }}>
                  <Pill tone={item.tone} dot size="sm">{item.stage}</Pill>
                </td>
                {/* Amount */}
                <td style={{ padding: '12px 14px', textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)', whiteSpace: 'nowrap' }}>
                  {monFmt(item.amount, item.cur)}
                </td>
                {/* Actions */}
                <td style={{ padding: '12px 14px' }}>
                  <div style={{ display: 'flex', gap: 4 }}>
                    {item.problem && <button className="btn btn-sm" style={{ padding: '3px 8px', fontSize: 11, background: 'var(--surface-inset)', border: '1px solid var(--border-light)', color: 'var(--text-primary)' }}>Resolve</button>}
                    <span className="hover-halo"><Icon name="arrowright" size={13} color="var(--text-secondary)"/></span>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

Object.assign(window, { ScreenOrderListEnhanced, ScreenOrderItemMonitor });
