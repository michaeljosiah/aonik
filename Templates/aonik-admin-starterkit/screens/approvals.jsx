// Approvals — cross-cutting decision queue.
// Any product (orders, refunds, KYB, payouts, policy edits) can drop a
// pending decision here. UI is type-agnostic + renders a typed body per kind.

const APPROVAL_TYPES = [
  { id:'all',         label:'All',                count: 12, icon:'list' },
  { id:'order',       label:'Orders',             count:  4, icon:'receipt' },
  { id:'refund',      label:'Refunds',            count:  2, icon:'arrows' },
  { id:'kyb',         label:'KYB / KYC',          count:  3, icon:'verified' },
  { id:'payout',      label:'Payouts',            count:  1, icon:'payout' },
  { id:'policy',      label:'Policy changes',     count:  1, icon:'shield' },
  { id:'partner',     label:'Partner config',     count:  1, icon:'network' },
];

const APPROVALS = [
  { id:'APR-1042', type:'order', urgency:'high', subject:'Order ORD-20250422-0047 — release to settlement',
    requester:{ name:'Maria Gomez',     init:'MG', color:'#055a60' },
    waitingOn:'You + 1 other',
    sla:'Expires in 48 min',
    policy:'Orders > £5,000 require dual approval (Finance + Compliance)',
    triggered:'£5,017.55 GBP equivalent · NGN+GBP mixed bundle',
    age:'12 min ago',
    payload:[
      { l:'Order',    v:'ORD-20250422-0047' },
      { l:'Customer', v:'Primrose Logistics' },
      { l:'Items',    v:'3 (1 transfer, 2 bill pay)' },
      { l:'FX exposure', v:'5.0K GBP / 39.5K NGN' },
      { l:'Risk score',  v:'0.21 — low' },
    ],
    progress:{ approved: 1, required: 2, approvers:[
      { name:'L. Chen', when:'8 min ago', state:'approved' },
      { name:'You',     when:null, state:'pending' },
    ]},
  },
  { id:'APR-1041', type:'refund', urgency:'medium', subject:'Refund INV-0041 — £840.00 to Northstar',
    requester:{ name:'Lin Chen',        init:'LC', color:'#3f41a0' },
    waitingOn:'You',
    sla:'Expires in 4 hours',
    policy:'Refunds > £500 require single approval',
    triggered:'Customer overpaid INV-0041 — duplicate wire',
    age:'34 min ago',
    payload:[
      { l:'Invoice',     v:'INV-0041' },
      { l:'Original',    v:'£12,840.00' },
      { l:'Overpayment', v:'£840.00' },
      { l:'Method',      v:'Reverse to source bank' },
    ],
    progress:{ approved: 0, required: 1, approvers:[ { name:'You', when:null, state:'pending' } ]},
  },
  { id:'APR-1040', type:'kyb', urgency:'medium', subject:'New customer — Saharan Logistics Ltd · activate',
    requester:{ name:'Operations Bot',  init:'OB', color:'var(--brand-primary)', isAgent:true },
    waitingOn:'You',
    sla:'No SLA',
    policy:'KYB activation requires Compliance sign-off',
    triggered:'Documents complete · Risk Agent score 0.18',
    age:'1 hr ago',
    payload:[
      { l:'Legal name',   v:'Saharan Logistics Ltd' },
      { l:'Country',      v:'NG' },
      { l:'UBOs verified',v:'2 of 2' },
      { l:'Sanctions',    v:'Clear' },
      { l:'Risk',         v:'0.18 — low' },
    ],
    progress:{ approved: 0, required: 1, approvers:[ { name:'You', when:null, state:'pending' } ]},
  },
  { id:'APR-1039', type:'policy', urgency:'low', subject:'Lower autopilot ceiling for Bill Pay to £25K',
    requester:{ name:'D. Adelekan',     init:'DA', color:'#6e7680' },
    waitingOn:'2 reviewers',
    sla:'Expires in 2 days',
    policy:'Policy edits require dual approval (Finance Lead + Risk)',
    triggered:'Quarterly review — currently £50K · proposed £25K',
    age:'5 hr ago',
    payload:[
      { l:'Current ceiling',  v:'£50,000' },
      { l:'Proposed',         v:'£25,000' },
      { l:'Affected agents',  v:'Billing Agent, Reconciliation Agent' },
      { l:'Effective',        v:'On approval' },
    ],
    progress:{ approved: 0, required: 2, approvers:[ { name:'You', when:null, state:'pending' }, { name:'F. Okafor', when:null, state:'pending' } ]},
  },
  { id:'APR-1038', type:'payout', urgency:'low', subject:'Manual payout — supplier wire to Acme Logistics',
    requester:{ name:'Maria Gomez',     init:'MG', color:'#055a60' },
    waitingOn:'You',
    sla:'No SLA',
    policy:'Manual payouts > £10K require Finance Lead approval',
    triggered:'Off-platform invoice INV-EXT-0021',
    age:'1 day ago',
    payload:[
      { l:'Recipient', v:'Acme Logistics · NatWest' },
      { l:'Amount',    v:'£14,200.00' },
      { l:'Reference', v:'INV-EXT-0021' },
      { l:'Memo',      v:'Off-platform supplier reimbursement' },
    ],
    progress:{ approved: 0, required: 1, approvers:[ { name:'You', when:null, state:'pending' } ]},
  },
];

function ScreenApprovals() {
  const [type, setType] = React.useState('all');
  const [selected, setSelected] = React.useState('APR-1042');
  const filtered = type === 'all' ? APPROVALS : APPROVALS.filter(a => a.type === type);
  const current = APPROVALS.find(a => a.id === selected) || filtered[0];

  return (
    <div style={{ height:'100%', display:'grid', gridTemplateColumns:'200px 380px 1fr', overflow:'hidden' }}>
      {/* Type filter rail */}
      <div style={{ borderRight:'1px solid var(--border-light)', padding:'20px 12px', overflow:'auto', display:'flex', flexDirection:'column', gap: 1, background:'var(--surface-inset)' }}>
        <div style={{ fontSize: 10, fontWeight: 700, textTransform:'uppercase', letterSpacing:'0.08em', color:'var(--text-tertiary)', padding:'4px 8px 8px' }}>By type</div>
        {APPROVAL_TYPES.map(t => (
          <button key={t.id} onClick={() => setType(t.id)} style={{
            display:'flex', alignItems:'center', gap: 8, padding:'7px 10px', borderRadius: 6,
            background: type === t.id ? 'var(--brand-primary-10)' : 'transparent',
            color: type === t.id ? 'var(--brand-primary)' : 'var(--text-secondary)',
            fontSize: 12.5, fontWeight: type === t.id ? 600 : 500,
            border: 'none', cursor:'pointer', textAlign:'left',
          }}>
            <Icon name={t.icon} size={13}/>
            <span style={{ flex: 1 }}>{t.label}</span>
            <span style={{ fontSize: 10.5, fontFamily:'var(--font-mono)', padding:'1px 6px', background: type === t.id ? 'var(--brand-primary)' : 'var(--surface)', color: type === t.id ? '#fff' : 'var(--text-tertiary)', borderRadius: 4, minWidth: 18, textAlign:'center' }}>{t.count}</span>
          </button>
        ))}
        <div style={{ height: 1, background:'var(--border-light)', margin:'12px 4px' }}/>
        <div style={{ fontSize: 10, fontWeight: 700, textTransform:'uppercase', letterSpacing:'0.08em', color:'var(--text-tertiary)', padding:'4px 8px 8px' }}>Filters</div>
        {['Awaiting me','High urgency','SLA breaching','I requested'].map(f => (
          <button key={f} style={{
            display:'flex', alignItems:'center', gap: 8, padding:'7px 10px', borderRadius: 6,
            background:'transparent', color:'var(--text-secondary)', fontSize: 12.5,
            border:'none', cursor:'pointer', textAlign:'left',
          }}>{f}</button>
        ))}
      </div>

      {/* Approval list */}
      <div style={{ borderRight:'1px solid var(--border-light)', overflow:'auto', display:'flex', flexDirection:'column' }}>
        <div style={{ padding:'16px 18px', borderBottom:'1px solid var(--border-light)', display:'flex', alignItems:'center', justifyContent:'space-between' }}>
          <div>
            <div style={{ fontSize: 16, fontWeight: 700, color:'var(--text-primary)' }}>Approvals</div>
            <div style={{ fontSize: 11.5, color:'var(--text-secondary)', marginTop: 1 }}>{filtered.length} pending · cross-product</div>
          </div>
          <button style={{ background:'transparent', border:'1px solid var(--border-light)', borderRadius: 6, padding:'5px 8px', cursor:'pointer', color:'var(--text-secondary)' }}>
            <Icon name="filter" size={12}/>
          </button>
        </div>
        <div style={{ flex: 1, overflow:'auto', display:'flex', flexDirection:'column' }}>
          {filtered.map(a => {
            const isSel = a.id === selected;
            const typeMeta = APPROVAL_TYPES.find(t => t.id === a.type);
            return (
              <button key={a.id} onClick={() => setSelected(a.id)} style={{
                display:'block', textAlign:'left', padding:'14px 18px',
                borderBottom:'1px solid var(--border-light)',
                background: isSel ? 'var(--brand-primary-10)' : 'transparent',
                borderLeft: isSel ? '3px solid var(--brand-primary)' : '3px solid transparent',
                cursor:'pointer', border:'none', borderRight:'none', borderTop:'none',
                width:'100%', borderBottomColor:'var(--border-light)', borderBottomStyle:'solid', borderBottomWidth: 1,
              }}>
                <div style={{ display:'flex', alignItems:'center', gap: 6, marginBottom: 4 }}>
                  <span style={{ display:'inline-flex', alignItems:'center', gap: 4, fontSize: 10, padding:'2px 6px', background:'var(--surface-inset)', border:'1px solid var(--border-light)', borderRadius: 4, color:'var(--text-secondary)', fontWeight: 600, textTransform:'uppercase', letterSpacing:'0.04em' }}>
                    <Icon name={typeMeta.icon} size={10}/> {typeMeta.label}
                  </span>
                  {a.urgency === 'high' && <span style={{ fontSize: 10, fontWeight: 700, color:'var(--danger)', textTransform:'uppercase', letterSpacing:'0.05em' }}>● HIGH</span>}
                  <span style={{ flex: 1 }}/>
                  <span style={{ fontSize: 10.5, fontFamily:'var(--font-mono)', color:'var(--text-tertiary)' }}>{a.id}</span>
                </div>
                <div style={{ fontSize: 13, fontWeight: 600, color:'var(--text-primary)', lineHeight: 1.35, marginBottom: 6 }}>{a.subject}</div>
                <div style={{ display:'flex', alignItems:'center', gap: 8, fontSize: 11, color:'var(--text-secondary)' }}>
                  <span style={{ width: 18, height: 18, borderRadius: 4, background: a.requester.color, color:'#fff', display:'inline-flex', alignItems:'center', justifyContent:'center', fontFamily:'var(--font-brand)', fontWeight: 700, fontSize: 9 }}>{a.requester.init}</span>
                  <span>{a.requester.name}{a.requester.isAgent && <span style={{ marginLeft: 4, color:'var(--brand-primary)', fontWeight: 600 }}>· agent</span>}</span>
                  <span style={{ flex: 1 }}/>
                  <span style={{ fontFamily:'var(--font-mono)', fontSize: 10.5, color:'var(--text-tertiary)' }}>{a.age}</span>
                </div>
                <div style={{ display:'flex', alignItems:'center', gap: 6, marginTop: 6, fontSize: 11 }}>
                  <span style={{ display:'inline-flex', alignItems:'center', gap: 3 }}>
                    {Array.from({ length: a.progress.required }).map((_, i) => (
                      <span key={i} style={{ width: 14, height: 4, borderRadius: 2, background: i < a.progress.approved ? 'var(--success)' : 'var(--border-medium)' }}/>
                    ))}
                  </span>
                  <span style={{ color:'var(--text-secondary)' }}>{a.progress.approved}/{a.progress.required} approved</span>
                  <span style={{ flex: 1 }}/>
                  <span style={{ color: a.sla.includes('min') ? 'var(--warning)' : 'var(--text-tertiary)' }}>{a.sla}</span>
                </div>
              </button>
            );
          })}
        </div>
      </div>

      {/* Detail panel */}
      <div style={{ overflow:'auto', display:'flex', flexDirection:'column' }}>
        {/* Sticky header */}
        <div style={{ padding:'18px 24px', borderBottom:'1px solid var(--border-light)', display:'flex', alignItems:'flex-start', justifyContent:'space-between', gap: 16 }}>
          <div>
            <div style={{ display:'flex', alignItems:'center', gap: 8, marginBottom: 6 }}>
              <span style={{ fontFamily:'var(--font-mono)', fontSize: 11.5, color:'var(--text-tertiary)' }}>{current.id}</span>
              <Pill tone={current.urgency === 'high' ? 'danger' : current.urgency === 'medium' ? 'warning' : 'default'} dot size="sm">{current.urgency} urgency</Pill>
              <Pill tone="tint" size="sm">{APPROVAL_TYPES.find(t => t.id === current.type).label}</Pill>
            </div>
            <div style={{ fontSize: 18, fontWeight: 700, color:'var(--text-primary)', letterSpacing:'-0.01em', maxWidth: 600 }}>{current.subject}</div>
          </div>
          <div style={{ display:'flex', gap: 6 }}>
            <button className="btn btn-sm" style={{ color:'var(--danger)', borderColor:'rgba(204,46,46,0.3)' }}><Icon name="x" size={12}/> Reject</button>
            <button className="btn btn-sm">Request changes</button>
            <button className="btn btn-primary btn-sm"><Icon name="check" size={12}/> Approve</button>
          </div>
        </div>

        {/* Body */}
        <div style={{ padding:'18px 24px', display:'flex', flexDirection:'column', gap: 16 }}>
          {/* Why it's here */}
          <div style={{ background:'var(--surface)', border:'1px solid var(--border-light)', borderRadius: 10, padding: 14 }}>
            <div style={{ display:'flex', alignItems:'center', gap: 6, fontSize: 10.5, fontWeight: 700, textTransform:'uppercase', letterSpacing:'0.06em', color:'var(--text-tertiary)', marginBottom: 8 }}>
              <Icon name="shield" size={11}/> Policy that triggered
            </div>
            <div style={{ fontSize: 13, color:'var(--text-primary)', lineHeight: 1.45, marginBottom: 4 }}>{current.policy}</div>
            <div style={{ fontSize: 12, color:'var(--text-secondary)' }}>Why now: <span style={{ color:'var(--text-primary)' }}>{current.triggered}</span></div>
          </div>

          {/* Typed payload */}
          <div style={{ background:'var(--surface)', border:'1px solid var(--border-light)', borderRadius: 10 }}>
            <div style={{ padding:'12px 14px', borderBottom:'1px solid var(--border-light)', display:'flex', alignItems:'center', justifyContent:'space-between' }}>
              <div style={{ fontSize: 13, fontWeight: 600, color:'var(--text-primary)' }}>Decision context</div>
              <button style={{ fontSize: 11.5, color:'var(--brand-primary)', background:'transparent', border:'none', cursor:'pointer' }}>Open full record →</button>
            </div>
            <div style={{ padding:'4px 0' }}>
              {current.payload.map((row, i) => (
                <div key={i} style={{ display:'grid', gridTemplateColumns:'160px 1fr', gap: 12, padding:'8px 14px', borderTop: i > 0 ? '1px dashed var(--border-light)' : 'none' }}>
                  <span style={{ fontSize: 12, color:'var(--text-tertiary)' }}>{row.l}</span>
                  <span style={{ fontSize: 12.5, color:'var(--text-primary)', fontFamily: row.l.toLowerCase().includes('order') || row.l.toLowerCase().includes('invoice') || row.l.toLowerCase().includes('amount') || row.l.toLowerCase().includes('exposure') ? 'var(--font-mono)' : 'var(--font-sans)' }}>{row.v}</span>
                </div>
              ))}
            </div>
          </div>

          {/* Approval chain */}
          <div style={{ background:'var(--surface)', border:'1px solid var(--border-light)', borderRadius: 10, padding: 14 }}>
            <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', marginBottom: 10 }}>
              <div style={{ fontSize: 13, fontWeight: 600, color:'var(--text-primary)' }}>Approval chain</div>
              <div style={{ fontSize: 11.5, color:'var(--text-secondary)' }}>
                <b style={{ color:'var(--text-primary)' }}>{current.progress.approved}</b> / {current.progress.required} approvals · {current.sla}
              </div>
            </div>
            <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
              {current.progress.approvers.map((ap, i) => (
                <div key={i} style={{ display:'flex', alignItems:'center', gap: 10, padding:'8px 10px', background:'var(--surface-inset)', borderRadius: 6 }}>
                  <span style={{ width: 26, height: 26, borderRadius: 5, background: ap.state === 'approved' ? 'var(--success)' : 'var(--border-medium)', color: '#fff', display:'inline-flex', alignItems:'center', justifyContent:'center' }}>
                    <Icon name={ap.state === 'approved' ? 'check' : 'clock'} size={12}/>
                  </span>
                  <div style={{ flex: 1 }}>
                    <div style={{ fontSize: 12.5, fontWeight: 500, color:'var(--text-primary)' }}>{ap.name}</div>
                    <div style={{ fontSize: 11, color:'var(--text-tertiary)' }}>
                      {ap.state === 'approved' ? `Approved ${ap.when}` : 'Pending'}
                    </div>
                  </div>
                  {ap.state === 'pending' && ap.name === 'You' && (
                    <span style={{ fontSize: 10.5, padding:'2px 7px', background:'var(--brand-primary-10)', color:'var(--brand-primary)', borderRadius: 4, fontWeight: 600, textTransform:'uppercase', letterSpacing:'0.04em' }}>Your turn</span>
                  )}
                </div>
              ))}
            </div>
          </div>

          {/* Comment composer */}
          <div style={{ background:'var(--surface)', border:'1px solid var(--border-light)', borderRadius: 10, padding: 12 }}>
            <textarea placeholder="Add a comment for the chain (optional)" style={{
              width:'100%', minHeight: 60, padding: 8, fontSize: 12.5, fontFamily:'var(--font-sans)',
              border:'none', outline:'none', resize:'vertical', background:'transparent', color:'var(--text-primary)',
              boxSizing:'border-box',
            }}/>
            <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', borderTop:'1px solid var(--border-light)', paddingTop: 8, marginTop: 6 }}>
              <span style={{ fontSize: 11, color:'var(--text-tertiary)' }}>Visible to approvers and requester</span>
              <button style={{ fontSize: 11, color:'var(--text-secondary)', background:'transparent', border:'1px solid var(--border-light)', borderRadius: 6, padding:'4px 8px', cursor:'pointer' }}>Send comment</button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { ScreenApprovals });
