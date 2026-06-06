// ─────────────────────────────────────────────────────────────────────────
// Agent Detail — the production page (Billing Agent).
//
// "Profile + Map": a calm Living-Profile layout is the default surface; the
// Capabilities block carries a Gallery ⇄ Map toggle. Gallery shows the unified
// capability cards (tools + skills + sub-agents + servers as ONE set); Map
// renders that same set as an interactive constellation. Selecting a capability
// (card or node) drives the right-rail inspector; "Open details" launches a deep
// drawer (tool schema, SKILL.md + bundle, delegations, server sync); "Edit agent"
// opens the shared AgentEditPanel slide-over.
//
// Helpers are namespaced (Adx*/Lp*/Topo*/Blend*) to avoid colliding with
// agent-detail.jsx (Section/Card/MiniStat/ANIM_CSS/SkillFileTree/…) in the shared
// script scope. Reused as-is: AGENT_LIST, AgentPortrait, Icon, Pill,
// AgentEditPanel, SkillFileTree, React.
// (Exploration directions A/B/C were removed once this blend was chosen.)
// ─────────────────────────────────────────────────────────────────────────

const ADX_CSS = `
.adx-lift  { transition: transform 160ms ease, border-color 160ms ease, box-shadow 160ms ease; }
.adx-lift:hover { transform: translateY(-2px); }
.topo-node { cursor: pointer; transition: opacity 180ms ease; }
.topo-node:hover .topo-hit { opacity: .14; }
`;

// ─── Data (parity with the legacy page) ──────────────────────────────────
const ADX_TOOLS = [
  { name: 'search_invoices',        cat: 'read',    desc: 'Query the invoice store by counterparty, status or amount.', uses: 1842, p99: '142ms', enabled: true },
  { name: 'list_bank_transactions', cat: 'read',    desc: 'Read bank txns inside a date window from connected rails.',  uses: 1318, p99: '318ms', enabled: true },
  { name: 'match_invoice_to_txn',   cat: 'compute', desc: 'Score a candidate match (0–1) from ledger + memo signals.',  uses: 1284, p99: '211ms', enabled: true },
  { name: 'draft_journal_entry',    cat: 'write',   desc: 'Compose a balanced debit/credit pair (proposal only).',      uses:  892, p99: '88ms',  enabled: true },
  { name: 'apply_journal_entry',    cat: 'write',   desc: 'Post a drafted entry to the ledger after approval.',         uses:  416, p99: '142ms', enabled: true },
  { name: 'send_dunning_email',     cat: 'write',   desc: 'Compose and dispatch an overdue reminder.',                  uses:   28, p99: '512ms', enabled: false },
  { name: 'display_proposal_card',  cat: 'display', desc: 'Render an Apply / Review / Dismiss card in chat.',           uses:  892, p99: '12ms',  enabled: true },
  { name: 'confirm_action',         cat: 'display', desc: 'Halt and ask the human for explicit approval.',              uses:  142, p99: '8ms',   enabled: true },
];
const ADX_SKILLS = [
  { name: 'invoice-reconciliation', desc: 'Match incoming bank txns to open invoices and draft entries when confidence is high.', version: '1.4.2', source: 'org',       last24h: 412, status: 'active' },
  { name: 'bank-statement-intake',  desc: 'Parse uploaded CSV/OFX statements and post lines as draft staging transactions.',     version: '2.0.1', source: 'org',       last24h: 184, status: 'active' },
  { name: 'ar-aging-summary',       desc: 'Aging summary across the AR ledger with sub-totals by tier and a chase-list.',         version: '1.0.0', source: 'community', last24h:  88, status: 'active' },
  { name: 'dunning-cadence',        desc: 'Choose a dunning template + channel for an overdue invoice from tier + history.',      version: '1.2.0', source: 'org',       last24h:  42, status: 'active' },
  { name: 'currency-rounding-fix',  desc: 'Detect and reverse off-by-cent rounding when invoice + settlement currencies differ.', version: '0.3.0', source: 'private',   last24h:   6, status: 'beta'  },
];
const ADX_SUBS = [
  { ref: 'ledger', role: 'Posts journal entries from matched txns',   calls: 142, avgMs: 218, autonomy: 'auto',    success: 98.4 },
  { ref: 'fx',     role: 'Quotes rates for cross-currency invoices',  calls:  38, avgMs: 412, autonomy: 'propose', success: 94.7 },
  { ref: 'compl',  role: 'KYC re-checks before any new counterparty', calls:  12, avgMs: 894, autonomy: 'block',   success: 100  },
  { ref: 'dunn',   role: 'Drafts overdue reminders when match fails', calls:  28, avgMs: 142, autonomy: 'propose', success: 96.4 },
];
const ADX_SERVERS = [
  { name: 'aonik-ledger',     url: 'mcp://internal/aonik-ledger',   status: 'connected',  tools: 12, latency: '14ms',  auth: 'mTLS',    native: true },
  { name: 'open-banking-uk',  url: 'mcp://partner/open-banking-uk', status: 'connected',  tools:  8, latency: '188ms', auth: 'OAuth2' },
  { name: 'companies-house',  url: 'mcp://partner/companies-house', status: 'connected',  tools:  4, latency: '412ms', auth: 'API key' },
  { name: 'fx-quotes',        url: 'mcp://partner/fx-quotes-v2',    status: 'connecting', tools:  6, latency: '—',     auth: 'OAuth2' },
  { name: 'sanctions-screen', url: 'mcp://partner/ofac-sanctions',  status: 'error',      tools:  3, latency: '—',     auth: 'mTLS', err: 'TLS handshake failed' },
];
const ADX_RUNS = [
  { op: 'match_and_apply', status: 'ok',   dur: '3.14s', t: 'now', txn: 'INV-2041' },
  { op: 'apply_invoice',   status: 'held', dur: '0.84s', t: '11m', txn: 'INV-2038' },
  { op: 'match_and_apply', status: 'ok',   dur: '2.94s', t: '24m', txn: 'INV-2037' },
  { op: 'summarize_ar',    status: 'ok',   dur: '1.94s', t: '1h',  txn: '—' },
  { op: 'dunning_send',    status: 'ok',   dur: '0.42s', t: '2h',  txn: 'INV-2014' },
  { op: 'reconcile_fx',    status: 'err',  dur: '4.21s', t: '3h',  txn: 'INV-2009' },
];
const ADX_POLICIES = [
  { t: 'Dual-control payouts', d: 'Two approvers for any outbound payout',  enforced: true },
  { t: 'Amount ceiling',       d: 'Always require approval > £50,000',       enforced: true },
  { t: 'PII redaction',        d: 'Customer PII stripped from all prompts',  enforced: true },
  { t: 'Auto-apply',           d: 'Confidence ≥ 0.95 · audit on apply',      enforced: true, soft: true },
];

// Unified capability set — used by the gallery and as the source for the
// constellation, so counts always line up across views.
function adxCaps() {
  const caps = [
    ...ADX_TOOLS.map(t => ({ id: 'tool-' + t.name, type: 'tool', name: t.name, desc: t.desc, stat: t.uses.toLocaleString(), statL: 'calls · 24h', on: t.enabled, mono: true })),
    ...ADX_SKILLS.map(s => ({ id: 'skill-' + s.name, type: 'skill', name: s.name, desc: s.desc, stat: String(s.last24h), statL: 'activations', on: true, mono: true, beta: s.status === 'beta' })),
    ...ADX_SUBS.map(s => { const sub = AGENT_LIST.find(a => a.id === s.ref); return { id: 'sub-' + s.ref, type: 'agent', name: sub.name, desc: s.role, stat: String(s.calls), statL: 'calls · 24h', on: true }; }),
    ...ADX_SERVERS.map(s => ({ id: 'srv-' + s.name, type: 'server', name: s.name, desc: s.url, stat: String(s.tools), statL: 'tools', on: s.status !== 'error', mono: true })),
  ];
  const counts = { all: caps.length, tool: ADX_TOOLS.length, skill: ADX_SKILLS.length, agent: ADX_SUBS.length, server: ADX_SERVERS.length };
  return { caps, counts };
}

// Donut ring (hero health). value 0..1.
function AdxRing({ value, size = 168, stroke = 14, color, track = 'var(--surface-inset)', children }) {
  const r = (size - stroke) / 2, c = 2 * Math.PI * r, off = c * (1 - value);
  return (
    <div style={{ position: 'relative', width: size, height: size, flex: 'none' }}>
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} style={{ transform: 'rotate(-90deg)' }}>
        <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke={track} strokeWidth={stroke} />
        <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke={color} strokeWidth={stroke}
          strokeDasharray={c} strokeDashoffset={off} strokeLinecap="round" />
      </svg>
      <div style={{ position: 'absolute', inset: 0, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', textAlign: 'center' }}>
        {children}
      </div>
    </div>
  );
}

// Tool-category accent palette — used by the node inspector.
const CAT_COLOR = { read: '#5fb7c9', write: '#fb6f4d', compute: '#34d399', display: '#a78bfa' };

const LP_TYPE = {
  tool:   { label: 'Tool',      color: '#0e7490', icon: 'tools'  },
  skill:  { label: 'Skill',     color: '#055a60', icon: 'book'   },
  agent:  { label: 'Sub-agent', color: '#7b76b6', icon: 'bot'    },
  server: { label: 'Server',    color: '#b4741e', icon: 'server' },
};

// ─── Hero ────────────────────────────────────────────────────────────────
function LpHeroBlock({ agent, onEdit }) {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '1fr auto', gap: 32, alignItems: 'center', marginBottom: 26 }}>
      <div style={{ display: 'flex', gap: 22, alignItems: 'flex-start' }}>
        <div style={{ position: 'relative', flex: 'none' }}>
          <AgentPortrait agent={agent} size={92} />
          <span style={{ position: 'absolute', bottom: 4, right: 4, width: 14, height: 14, borderRadius: 9, background: 'var(--success)', boxShadow: '0 0 0 3px var(--surface)' }} />
        </div>
        <div style={{ minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 9, marginBottom: 8 }}>
            <span style={{ fontSize: 10, letterSpacing: '0.1em', textTransform: 'uppercase', fontWeight: 700, color: agent.color, padding: '3px 9px', borderRadius: 5, background: agent.color + '16' }}>{agent.kind} agent</span>
            <Pill tone="success" dot size="sm">Running</Pill>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)' }}>v0.42.1 · 12d ago</span>
          </div>
          <h1 style={{ fontFamily: 'var(--font-brand)', fontSize: 34, letterSpacing: '-0.02em', margin: 0, color: 'var(--text-primary)' }}>{agent.name}</h1>
          <p style={{ fontSize: 14.5, color: 'var(--text-secondary)', lineHeight: 1.55, margin: '8px 0 16px', maxWidth: 620 }}>{agent.description}</p>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-primary" onClick={onEdit}><Icon name="edit" size={13} /> Edit agent</button>
            <button className="btn btn-outline"><Icon name="play" size={13} /> Playground</button>
            <button className="btn btn-ghost"><Icon name="terminal" size={13} /> Traces</button>
          </div>
        </div>
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 22, flex: 'none' }}>
        <AdxRing value={agent.conf} size={132} stroke={12} color={agent.color}>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: 26, fontWeight: 700, color: 'var(--text-primary)', lineHeight: 1 }}>{Math.round(agent.conf * 100)}%</div>
          <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 3, letterSpacing: '0.04em' }}>confidence</div>
        </AdxRing>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          <LpQuiet l="Runs · 24h" v={agent.runs.toLocaleString()} />
          <LpQuiet l="p99 latency" v="892ms" />
          <LpQuiet l="Autonomy" v="Auto-apply" accent={agent.color} />
        </div>
      </div>
    </div>
  );
}

function LpQuiet({ l, v, accent }) {
  return (
    <div>
      <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', letterSpacing: '0.04em' }}>{l}</div>
      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 16, fontWeight: 600, color: accent || 'var(--text-primary)', marginTop: 2 }}>{v}</div>
    </div>
  );
}

// ─── Capabilities — shared filter / gallery ──────────────────────────────
function LpCapFilter({ filter, setFilter, counts, color }) {
  const segs = [
    { id: 'all', label: 'All' }, { id: 'tool', label: 'Tools' }, { id: 'skill', label: 'Skills' }, { id: 'agent', label: 'Sub-agents' }, { id: 'server', label: 'Servers' },
  ];
  return (
    <div style={{ display: 'inline-flex', padding: 4, gap: 2, background: 'var(--surface-inset)', borderRadius: 12 }}>
      {segs.map(s => {
        const on = filter === s.id;
        return (
          <button key={s.id} onClick={() => setFilter(s.id)} style={{
            display: 'inline-flex', alignItems: 'center', gap: 7, height: 32, padding: '0 14px', borderRadius: 9, cursor: 'pointer', border: 'none',
            fontSize: 12.5, fontWeight: on ? 600 : 500,
            background: on ? 'var(--surface)' : 'transparent',
            color: on ? 'var(--text-primary)' : 'var(--text-secondary)',
            boxShadow: on ? '0 1px 3px rgba(20,25,30,0.10)' : 'none',
          }}>
            {s.label}
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: on ? color : 'var(--text-tertiary)' }}>{counts[s.id]}</span>
          </button>
        );
      })}
    </div>
  );
}

function LpCapGrid({ shown, onSelect, selId }) {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 12 }}>
      {shown.map(cap => <LpCapCard key={cap.id || (cap.type + cap.name)} cap={cap} onSelect={onSelect} selected={selId === cap.id} />)}
    </div>
  );
}

function LpCapCard({ cap, onSelect, selected }) {
  const ty = LP_TYPE[cap.type];
  return (
    <div className="adx-lift" onClick={onSelect ? () => onSelect(cap.id) : undefined} style={{
      background: selected ? ty.color + '0c' : 'var(--surface)', borderRadius: 14, padding: 16,
      border: `1px solid ${selected ? ty.color : 'var(--border-light)'}`,
      boxShadow: selected ? `0 0 0 1px ${ty.color}` : '0 1px 2px rgba(20,25,30,0.04)',
      display: 'flex', flexDirection: 'column', gap: 10, opacity: cap.on ? 1 : 0.62, cursor: 'pointer', minHeight: 118,
    }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 11 }}>
        <span style={{ width: 36, height: 36, borderRadius: 10, background: ty.color + '14', color: ty.color, display: 'grid', placeItems: 'center', flex: 'none' }}>
          <Icon name={ty.icon} size={17} />
        </span>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
            <span style={{ fontFamily: cap.mono ? 'var(--font-mono)' : 'inherit', fontSize: cap.mono ? 12.5 : 13.5, fontWeight: 600, color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{cap.name}</span>
            {cap.beta && <span style={{ fontSize: 9, fontWeight: 700, color: '#b4741e', padding: '1px 5px', borderRadius: 3, background: '#b4741e18' }}>BETA</span>}
          </div>
          <span style={{ fontSize: 10, fontWeight: 600, letterSpacing: '0.04em', textTransform: 'uppercase', color: ty.color }}>{ty.label}</span>
        </div>
        {!cap.on && <span style={{ fontSize: 10, color: 'var(--text-tertiary)', border: '1px solid var(--border-light)', padding: '1px 6px', borderRadius: 4 }}>off</span>}
      </div>
      <p style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5, margin: 0, flex: 1, display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden' }}>{cap.desc}</p>
      <div style={{ display: 'flex', alignItems: 'baseline', gap: 6, borderTop: '1px solid var(--border-light)', paddingTop: 9 }}>
        <span style={{ fontFamily: 'var(--font-mono)', fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{cap.stat}</span>
        <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>{cap.statL}</span>
      </div>
    </div>
  );
}

// ─── Vitals rail (gallery default) ───────────────────────────────────────
function LpVitalsRail({ agent }) {
  return (
    <>
      <LpCard title="Configuration">
        <div style={{ padding: '4px 16px 14px', display: 'flex', flexDirection: 'column' }}>
          {[['Model', agent.model, true], ['Temperature', agent.temp.toFixed(1), true], ['Region', 'eu-west-2', true], ['Owner', 'Treasury team', false], ['Auto-apply', 'Enabled', false]].map(([k, v, mono], i) => (
            <div key={k} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '10px 0', borderBottom: i < 4 ? '1px solid var(--border-light)' : 'none' }}>
              <span style={{ fontSize: 12, color: 'var(--text-tertiary)' }}>{k}</span>
              <span style={{ fontFamily: mono ? 'var(--font-mono)' : 'inherit', fontSize: 12.5, fontWeight: 600, color: k === 'Auto-apply' ? 'var(--success)' : 'var(--text-primary)' }}>{v}</span>
            </div>
          ))}
        </div>
      </LpCard>

      <LpCard title="Recent activity" eyebrow="318 runs · 24h">
        <div>
          {ADX_RUNS.slice(0, 5).map((r, i) => {
            const tc = r.status === 'ok' ? 'var(--success)' : r.status === 'held' ? 'var(--warning)' : 'var(--danger)';
            return (
              <div key={i} style={{ display: 'grid', gridTemplateColumns: '8px 1fr auto', gap: 10, alignItems: 'center', padding: '10px 16px', borderTop: '1px solid var(--border-light)' }}>
                <span style={{ width: 7, height: 7, borderRadius: 9, background: tc }} />
                <div style={{ minWidth: 0 }}>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{r.op}</div>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)' }}>{r.txn} · {r.dur}</div>
                </div>
                <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{r.t}</span>
              </div>
            );
          })}
        </div>
      </LpCard>

      <LpCard title="Policies" eyebrow="4 active">
        <div style={{ padding: '6px 16px 14px', display: 'flex', flexDirection: 'column', gap: 12 }}>
          {ADX_POLICIES.map((p, i) => (
            <div key={i} style={{ display: 'flex', alignItems: 'flex-start', gap: 10 }}>
              <Icon name={p.soft ? 'sparkles' : 'lock'} size={13} color={p.soft ? agent.color : 'var(--text-secondary)'} style={{ marginTop: 1 }} />
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontSize: 12, fontWeight: 500, color: 'var(--text-primary)' }}>{p.t}</div>
                <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 1 }}>{p.d}</div>
              </div>
              <Pill tone={p.soft ? 'tint' : 'success'} size="sm">{p.soft ? 'on' : 'enforced'}</Pill>
            </div>
          ))}
        </div>
      </LpCard>
    </>
  );
}

function LpCard({ title, eyebrow, children }) {
  return (
    <div style={{ background: 'var(--surface)', borderRadius: 14, border: '1px solid var(--border-light)', boxShadow: '0 1px 2px rgba(20,25,30,0.04)', overflow: 'hidden' }}>
      <div style={{ padding: '14px 16px 10px' }}>
        <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{title}</div>
        {eyebrow && <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 1, fontFamily: 'var(--font-mono)' }}>{eyebrow}</div>}
      </div>
      {children}
    </div>
  );
}

// ─── Map — constellation canvas ──────────────────────────────────────────
const TOPO_CX = 452, TOPO_CY = 264;
const topoPlace = (deg, r) => ({ x: TOPO_CX + r * Math.cos(deg * Math.PI / 180), y: TOPO_CY + r * Math.sin(deg * Math.PI / 180) });

// Plots the FULL capability set (8 tools, 5 skills, 4 sub-agents, 5 servers =
// 22 nodes) so the constellation matches the gallery's counts exactly.
function topoNodes() {
  const nodes = [];
  const subAng = [240, 260, 280, 300];                       // top arc
  ADX_SUBS.forEach((s, i) => {
    const sub = AGENT_LIST.find(a => a.id === s.ref);
    nodes.push({ id: 'sub-' + s.ref, type: 'agent', label: sub.name.replace(' Agent', ''), color: sub.color, r: 21, ...topoPlace(subAng[i], 150), data: { ...s, sub } });
  });
  const tAng = [-52, -37, -22, -7, 8, 23, 38, 52];           // right arc
  ADX_TOOLS.forEach((t, i) => nodes.push({ id: 'tool-' + t.name, type: 'tool', label: t.name, color: LP_TYPE.tool.color, r: 13, ...topoPlace(tAng[i], 240), data: t, short: true }));
  const skAng = [66, 83, 99, 116, 132];                       // bottom arc
  ADX_SKILLS.forEach((s, i) => nodes.push({ id: 'skill-' + s.name, type: 'skill', label: s.name, color: LP_TYPE.skill.color, r: 13, ...topoPlace(skAng[i], 208), data: s, short: true }));
  const vAng = [146, 166, 186, 206, 226];                     // left arc
  ADX_SERVERS.forEach((s, i) => nodes.push({ id: 'srv-' + s.name, type: 'server', label: s.name, color: LP_TYPE.server.color, r: 15, ...topoPlace(vAng[i], 214), data: s }));
  return nodes;
}

function TopoCanvas({ agent, nodes, lens, selId, onSelect, onReset, height = 560 }) {
  const visible = n => lens === 'all' || n.type === lens;
  return (
    <div style={{
      position: 'relative', height, borderRadius: 16, overflow: 'hidden',
      backgroundColor: 'var(--surface-canvas, #f3f5f8)',
      border: '1px solid var(--border-light)',
      backgroundImage: 'radial-gradient(circle, rgba(20,25,30,0.05) 1px, transparent 1px)',
      backgroundSize: '24px 24px',
    }}>
      <svg width="100%" height="100%" viewBox="0 0 904 560" preserveAspectRatio="xMidYMid meet" style={{ display: 'block' }}>
        <defs>
          <radialGradient id="topo-core" cx="0.5" cy="0.5" r="0.5">
            <stop offset="0%" stopColor={agent.color} stopOpacity="0.30" />
            <stop offset="100%" stopColor={agent.color} stopOpacity="0" />
          </radialGradient>
        </defs>
        {[150, 208, 240].map((r, i) => <circle key={i} cx={TOPO_CX} cy={TOPO_CY} r={r} fill="none" stroke="var(--border-light)" strokeDasharray="2 6" opacity="0.7" />)}
        <circle cx={TOPO_CX} cy={TOPO_CY} r="120" fill="url(#topo-core)" />

        {nodes.map(n => {
          const dim = !visible(n);
          return (
            <g key={'l' + n.id} opacity={dim ? 0.08 : 1}>
              <line x1={TOPO_CX} y1={TOPO_CY} x2={n.x} y2={n.y} stroke={n.color} strokeOpacity={selId === n.id ? 0.7 : 0.24} strokeWidth={selId === n.id ? 1.8 : 1} />
              {n.type === 'agent' && !dim && (
                <circle r="3" fill={n.color}>
                  <animateMotion dur={`${2.6 + n.r * 0.05}s`} repeatCount="indefinite" path={`M${TOPO_CX} ${TOPO_CY} L${n.x} ${n.y}`} />
                </circle>
              )}
            </g>
          );
        })}

        {nodes.map(n => {
          const dim = !visible(n), on = selId === n.id;
          return (
            <g key={n.id} className="topo-node" opacity={dim ? 0.16 : 1} onClick={() => onSelect(n.id)} style={{ cursor: 'pointer' }}>
              {on && <circle cx={n.x} cy={n.y} r={n.r + 7} fill="none" stroke={n.color} strokeWidth="1.5" strokeOpacity="0.5" />}
              <circle className="topo-hit" cx={n.x} cy={n.y} r={n.r + 10} fill={n.color} opacity="0" />
              <circle cx={n.x} cy={n.y} r={n.r} fill="var(--surface)" stroke={n.color} strokeWidth={on ? 2.4 : 1.6} />
              <circle cx={n.x} cy={n.y} r={n.r - 6} fill={n.color} fillOpacity={n.type === 'agent' ? 0.18 : 0.9} />
              {n.type === 'agent'
                ? <g transform={`translate(${n.x - 13},${n.y - 13})`}><AgentPortrait agent={n.data.sub} size={26} ring={false} /></g>
                : <g transform={`translate(${n.x},${n.y})`}><TopoGlyph type={n.type} /></g>}
              <text x={n.x} y={n.y + n.r + 13} textAnchor="middle" fontSize={n.short ? 8.5 : 9.5} fontFamily="var(--font-mono)" fill="var(--text-secondary)">
                {n.label.length > 14 ? n.label.slice(0, 13) + '…' : n.label}
              </text>
            </g>
          );
        })}

        <g onClick={() => onSelect('center')} style={{ cursor: 'pointer' }}>
          <circle cx={TOPO_CX} cy={TOPO_CY} r="48" fill="var(--surface)" stroke={agent.color} strokeWidth={selId === 'center' ? 3 : 2} />
          <circle cx={TOPO_CX} cy={TOPO_CY} r="48" fill="none" stroke={agent.color} strokeOpacity="0.3" strokeWidth="2">
            <animate attributeName="r" from="48" to="64" dur="2.4s" repeatCount="indefinite" />
            <animate attributeName="stroke-opacity" from="0.4" to="0" dur="2.4s" repeatCount="indefinite" />
          </circle>
          <g transform={`translate(${TOPO_CX - 38},${TOPO_CY - 38})`}><AgentPortrait agent={agent} size={76} /></g>
        </g>
      </svg>

      <div style={{ position: 'absolute', left: 16, top: 14, display: 'flex', gap: 14, fontSize: 11, color: 'var(--text-secondary)', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '8px 12px' }}>
        {Object.entries(LP_TYPE).map(([k, v]) => (
          <span key={k} style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
            <span style={{ width: 8, height: 8, borderRadius: 9, background: k === 'agent' ? '#7b76b6' : v.color }} />{v.label}
          </span>
        ))}
      </div>
      <div style={{ position: 'absolute', right: 14, top: 14, display: 'flex', gap: 6 }}>
        <button className="btn btn-ghost btn-sm" style={{ background: 'var(--surface)', border: '1px solid var(--border-light)' }}><Icon name="search" size={12} /></button>
        {onReset && <button className="btn btn-ghost btn-sm" style={{ background: 'var(--surface)', border: '1px solid var(--border-light)' }} onClick={onReset}><Icon name="refresh" size={12} /></button>}
      </div>
    </div>
  );
}

function TopoGlyph({ type }) {
  const name = type === 'tool' ? 'tools' : type === 'skill' ? 'book' : 'server';
  return <g transform="translate(-6,-6)"><Icon name={name} size={12} color="#fff" /></g>;
}

// ─── Right-rail node inspector (drives both gallery + map selection) ──────
function TopoInspector({ agent, sel, onClear, onEdit, onOpenDetails }) {
  const wrap = (children) => (
    <div style={{ background: 'var(--surface)', borderRadius: 16, border: '1px solid var(--border-light)', boxShadow: '0 8px 28px -18px rgba(20,25,30,0.22)', overflow: 'hidden', position: 'sticky', top: 20 }}>{children}</div>
  );

  if (!sel) {
    return wrap(
      <div>
        <div style={{ padding: '20px 18px', background: `linear-gradient(135deg, ${agent.color}14, transparent)`, borderBottom: '1px solid var(--border-light)' }}>
          <div style={{ display: 'flex', gap: 14, alignItems: 'center' }}>
            <AgentPortrait agent={agent} size={56} />
            <div>
              <div style={{ fontFamily: 'var(--font-brand)', fontSize: 19, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>{agent.name}</div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 7, marginTop: 4 }}>
                <Pill tone="success" dot size="sm">Running</Pill>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>v0.42.1</span>
              </div>
            </div>
          </div>
          <p style={{ fontSize: 12, color: 'var(--text-secondary)', lineHeight: 1.55, margin: '14px 0 0' }}>{agent.description}</p>
        </div>
        <div style={{ padding: 16 }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, marginBottom: 14 }}>
            <TopoStat l="Runs · 24h" v={agent.runs.toLocaleString()} />
            <TopoStat l="Confidence" v={Math.round(agent.conf * 100) + '%'} tone="var(--success)" />
            <TopoStat l="Tools" v={ADX_TOOLS.length} />
            <TopoStat l="Sub-agents" v={ADX_SUBS.length} />
          </div>
          <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.6, padding: '12px 14px', background: 'var(--surface-inset)', borderRadius: 10 }}>
            <b style={{ color: 'var(--text-primary)' }}>Tip</b> — select any capability (a card, or a node in Map view) to inspect it here.
          </div>
          <div style={{ display: 'flex', gap: 8, marginTop: 14 }}>
            <button className="btn btn-primary btn-sm" style={{ flex: 1 }} onClick={onEdit}><Icon name="edit" size={12} /> Edit</button>
            <button className="btn btn-outline btn-sm" style={{ flex: 1 }}><Icon name="play" size={12} /> Playground</button>
          </div>
        </div>
      </div>
    );
  }

  const ty = LP_TYPE[sel.type];
  const header = (extra) => (
    <div style={{ padding: '16px 18px', borderBottom: '1px solid var(--border-light)' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 10 }}>
        <span style={{ fontSize: 9.5, fontWeight: 700, letterSpacing: '0.06em', textTransform: 'uppercase', color: ty.color, padding: '3px 8px', borderRadius: 5, background: ty.color + '16' }}>{ty.label}</span>
        <div style={{ flex: 1 }} />
        <button onClick={onClear} style={{ width: 24, height: 24, borderRadius: 6, border: '1px solid var(--border-light)', background: 'var(--surface)', cursor: 'pointer', display: 'grid', placeItems: 'center' }}><Icon name="x" size={12} color="var(--text-tertiary)" /></button>
      </div>
      {extra}
    </div>
  );

  if (sel.type === 'agent') {
    const s = sel.data, tone = { auto: 'success', propose: 'warning', block: 'tint' }[s.autonomy], tl = { auto: 'Auto-apply', propose: 'Propose', block: 'Required' }[s.autonomy];
    return wrap(<div>
      {header(<div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
        <AgentPortrait agent={s.sub} size={42} ring={false} />
        <div><div style={{ fontSize: 16, fontWeight: 600, color: 'var(--text-primary)' }}>{s.sub.name}</div>
          <div style={{ marginTop: 3 }}><Pill tone={tone} size="sm">{tl}</Pill></div></div>
      </div>)}
      <div style={{ padding: 16 }}>
        <p style={{ fontSize: 12, color: 'var(--text-secondary)', lineHeight: 1.55, margin: '0 0 14px' }}>{s.role}</p>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 8 }}>
          <TopoStat l="Calls · 24h" v={s.calls} /><TopoStat l="Success" v={s.success + '%'} tone={s.success >= 98 ? 'var(--success)' : 'var(--warning)'} /><TopoStat l="Avg" v={s.avgMs + 'ms'} />
        </div>
        <button className="btn btn-outline btn-sm" style={{ width: '100%', marginTop: 14 }} onClick={onOpenDetails ? () => onOpenDetails(sel) : undefined}>{onOpenDetails ? 'Open details' : 'Open agent'} <Icon name="arrowright" size={12} /></button>
      </div>
    </div>);
  }

  if (sel.type === 'tool') {
    const t = sel.data, cc = CAT_COLOR[t.cat];
    return wrap(<div>
      {header(<div style={{ fontFamily: 'var(--font-mono)', fontSize: 15, fontWeight: 600, color: 'var(--text-primary)' }}>{t.name}</div>)}
      <div style={{ padding: 16 }}>
        <div style={{ display: 'inline-flex', marginBottom: 12, fontSize: 9.5, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.06em', color: cc, padding: '2px 8px', borderRadius: 4, background: cc + '18', fontFamily: 'var(--font-mono)' }}>{t.cat}</div>
        <p style={{ fontSize: 12, color: 'var(--text-secondary)', lineHeight: 1.55, margin: '0 0 14px' }}>{t.desc}</p>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, marginBottom: 14 }}>
          <TopoStat l="Uses · 24h" v={t.uses.toLocaleString()} /><TopoStat l="p99" v={t.p99} />
        </div>
        <div style={{ fontSize: 10, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 6 }}>Recent calls</div>
        <div style={{ borderRadius: 10, border: '1px solid var(--border-light)', overflow: 'hidden' }}>
          {[['ok', 'INV-2041', 'now'], ['ok', 'INV-2038', '11m'], ['err', 'INV-2031', '1h']].map((r, i) => (
            <div key={i} style={{ display: 'grid', gridTemplateColumns: '34px 1fr auto', gap: 8, alignItems: 'center', padding: '8px 12px', borderTop: i ? '1px solid var(--border-light)' : 'none' }}>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 9, fontWeight: 700, textTransform: 'uppercase', textAlign: 'center', padding: '2px 0', borderRadius: 3, color: r[0] === 'ok' ? 'var(--success)' : 'var(--danger)', background: (r[0] === 'ok' ? 'var(--success)' : 'var(--danger)') + '1a' }}>{r[0]}</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-primary)' }}>{r[1]}</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>{r[2]}</span>
            </div>
          ))}
        </div>
        {onOpenDetails && <button className="btn btn-outline btn-sm" style={{ width: '100%', marginTop: 14 }} onClick={() => onOpenDetails(sel)}>Open details <Icon name="arrowright" size={12} /></button>}
      </div>
    </div>);
  }

  if (sel.type === 'skill') {
    const s = sel.data;
    return wrap(<div>
      {header(<div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <span style={{ fontFamily: 'var(--font-mono)', fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>{s.name}</span>
        <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>v{s.version}</span>
      </div>)}
      <div style={{ padding: 16 }}>
        <p style={{ fontSize: 12, color: 'var(--text-secondary)', lineHeight: 1.55, margin: '0 0 14px' }}>{s.desc}</p>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, marginBottom: 14 }}>
          <TopoStat l="Activations" v={s.last24h} /><TopoStat l="Source" v={s.source} />
        </div>
        <div style={{ background: '#1a1d21', borderRadius: 10, padding: '10px 12px', fontFamily: 'var(--font-mono)', fontSize: 10.5, lineHeight: 1.6, color: '#d8dde2' }}>
          <div style={{ color: '#7b76b6' }}>---</div>
          <div style={{ color: '#5facbd' }}>name: {s.name}</div>
          <div style={{ color: '#5facbd' }}>description: |</div>
          <div style={{ color: '#a8dbcb' }}>&nbsp;&nbsp;Use when {s.desc.slice(0, 42).toLowerCase()}…</div>
          <div style={{ color: '#7b76b6' }}>---</div>
        </div>
        {onOpenDetails && <button className="btn btn-outline btn-sm" style={{ width: '100%', marginTop: 14 }} onClick={() => onOpenDetails(sel)}>Open details <Icon name="arrowright" size={12} /></button>}
      </div>
    </div>);
  }

  const s = sel.data;
  const stc = s.status === 'connected' ? 'var(--success)' : s.status === 'error' ? 'var(--danger)' : 'var(--warning)';
  return wrap(<div>
    {header(<div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
      <span style={{ fontSize: 15, fontWeight: 600, color: 'var(--text-primary)' }}>{s.name}</span>
      {s.native && <span style={{ fontSize: 9, fontWeight: 700, color: 'var(--brand-primary)', padding: '1px 5px', borderRadius: 3, background: 'var(--brand-primary-10)' }}>NATIVE</span>}
    </div>)}
    <div style={{ padding: 16 }}>
      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)', marginBottom: 12, wordBreak: 'break-all' }}>{s.url}</div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 7, marginBottom: 14, fontSize: 12, fontWeight: 600, color: stc }}>
        <span style={{ width: 7, height: 7, borderRadius: 9, background: stc }} />{s.status}
        {s.err && <span style={{ fontWeight: 400, color: 'var(--text-secondary)' }}>· {s.err}</span>}
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 8 }}>
        <TopoStat l="Tools" v={s.tools} /><TopoStat l="Auth" v={s.auth} /><TopoStat l="Latency" v={s.latency} />
      </div>
      <button className="btn btn-outline btn-sm" style={{ width: '100%', marginTop: 14 }} onClick={onOpenDetails ? () => onOpenDetails(sel) : undefined}>{onOpenDetails ? 'Open details' : 'Manage server'}</button>
    </div>
  </div>);
}

function TopoStat({ l, v, tone }) {
  return (
    <div style={{ background: 'var(--surface-inset)', borderRadius: 9, padding: '9px 11px' }}>
      <div style={{ fontSize: 9.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase' }}>{l}</div>
      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 13.5, fontWeight: 600, color: tone || 'var(--text-primary)', marginTop: 3 }}>{v}</div>
    </div>
  );
}

// ═════════════════════════════════════════════════════════════════════════
// THE PAGE — "Profile + Map"  (gallery default · Gallery ⇄ Map toggle)
// ═════════════════════════════════════════════════════════════════════════

function ScreenAgentDetailBlend() {
  const agent = AGENT_LIST.find(a => a.id === 'billing');
  const nodes = React.useMemo(() => topoNodes(), []);
  const [view, setView] = React.useState('gallery');       // 'gallery' | 'map'
  const [filter, setFilter] = React.useState('all');       // doubles as the map lens
  const [selId, setSelId] = React.useState('center');      // selected capability (both views)
  const [editing, setEditing] = React.useState(false);     // edit slide-over
  const [detailNode, setDetailNode] = React.useState(null);// deep-detail drawer

  const { caps, counts } = adxCaps();
  const shown = filter === 'all' ? caps : caps.filter(x => x.type === filter);
  const sel = selId === 'center' ? null : nodes.find(n => n.id === selId);

  // one control drives both views; switching lens in map mode resets selection
  const onFilter = (f) => { setFilter(f); if (view === 'map') setSelId('center'); };

  const ToggleBtn = ({ id, icon, label }) => {
    const on = view === id;
    return (
      <button onClick={() => setView(id)} style={{
        display: 'inline-flex', alignItems: 'center', gap: 7, height: 32, padding: '0 13px', borderRadius: 9, cursor: 'pointer', border: 'none',
        fontSize: 12.5, fontWeight: on ? 600 : 500,
        background: on ? 'var(--surface)' : 'transparent',
        color: on ? 'var(--text-primary)' : 'var(--text-secondary)',
        boxShadow: on ? '0 1px 3px rgba(20,25,30,0.10)' : 'none',
      }}>
        <Icon name={icon} size={13} color={on ? agent.color : 'var(--text-tertiary)'} /> {label}
      </button>
    );
  };

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden', background: 'var(--surface-canvas, #f7f8fa)' }}>
      <style>{ADX_CSS}</style>
      <div style={{ height: '100%', overflow: 'auto' }}>
        <div style={{ maxWidth: 1240, margin: '0 auto', padding: '28px 32px 48px' }}>
          <LpHeroBlock agent={agent} onEdit={() => setEditing(true)} />

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 320px', gap: 24, alignItems: 'start' }}>
            <div>
              {/* Capabilities header — title + the Gallery/Map view toggle */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 14 }}>
                <h2 style={{ fontFamily: 'var(--font-brand)', fontSize: 19, letterSpacing: '-0.01em', margin: 0, color: 'var(--text-primary)' }}>Capabilities</h2>
                <span style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}>{view === 'map' ? 'how it all connects' : 'everything this agent can do'}</span>
                <div style={{ flex: 1 }} />
                <div style={{ display: 'inline-flex', padding: 4, gap: 2, background: 'var(--surface-inset)', borderRadius: 11 }}>
                  <ToggleBtn id="gallery" icon="grid" label="Gallery" />
                  <ToggleBtn id="map" icon="network" label="Map" />
                </div>
                <button className="btn btn-outline btn-sm"><Icon name="plus" size={11} /> Add</button>
              </div>

              {/* shared type filter / map lens */}
              <div style={{ marginBottom: 16 }}><LpCapFilter filter={filter} setFilter={onFilter} counts={counts} color={agent.color} /></div>

              {view === 'gallery'
                ? <LpCapGrid shown={shown} onSelect={setSelId} selId={selId} />
                : <TopoCanvas agent={agent} nodes={nodes} lens={filter} selId={selId} onSelect={setSelId} onReset={() => { setFilter('all'); setSelId('center'); }} height={548} />}
            </div>

            {/* right rail — node inspector whenever a capability is selected; else
                vitals (gallery) or the agent overview (map). Both views drive the
                SAME inspector, and its "Open details" launches the deep drawer. */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 16, position: 'sticky', top: 20 }}>
              {sel
                ? <TopoInspector agent={agent} sel={sel} onClear={() => setSelId('center')} onEdit={() => setEditing(true)} onOpenDetails={(n) => setDetailNode(n)} />
                : (view === 'gallery'
                    ? <LpVitalsRail agent={agent} />
                    : <TopoInspector agent={agent} sel={null} onClear={() => setSelId('center')} onEdit={() => setEditing(true)} />)}
            </div>
          </div>
        </div>
      </div>

      {editing && <AgentEditPanel agent={agent} onClose={() => setEditing(false)} />}
      {detailNode && <BlendDetailDrawer node={detailNode} agent={agent} onClose={() => setDetailNode(null)} />}
    </div>
  );
}

// ─── Deep-detail drawer (per-capability full view) ───────────────────────
// Wide right slide-over reached from the rail inspector's "Open details".
// Tool → input/output schema + recent invocations · Skill → full SKILL.md +
// bundle file tree · Sub-agent → role + delegations · Server → connection + sync.
function adxToolSchema(t) {
  const input = t.cat === 'read'
    ? `{\n  "query": "string",\n  "window": "string   // e.g. 72h",\n  "limit": "number   // default 50"\n}`
    : t.cat === 'compute'
    ? `{\n  "invoice_id": "string",\n  "candidate_txn_id": "string",\n  "tolerance_pct": "number   // default 0.5"\n}`
    : t.cat === 'write'
    ? `{\n  "entry": "JournalEntry",\n  "idempotency_key": "string"\n}`
    : `{\n  "payload": "object",\n  "channel": "chat | voice"\n}`;
  const returns = t.cat === 'read'
    ? `{\n  "rows": [Row],\n  "cursor": "string | null"\n}`
    : t.cat === 'compute'
    ? `{\n  "score": 0.0..1.0,\n  "reasons": [string],\n  "tentative_journal": JournalEntry | null\n}`
    : t.cat === 'write'
    ? `{\n  "posted": boolean,\n  "ref": "string",\n  "ledger_id": "string"\n}`
    : `{\n  "rendered": boolean\n}`;
  return { input, returns };
}

function adxSkillMd(s) {
  const lead = s.desc.charAt(0).toLowerCase() + s.desc.slice(1);
  return `---\nname: ${s.name}\ndescription: |\n  Use when the user wants to ${lead}\nallowed-tools: [search_invoices, list_bank_transactions, match_invoice_to_txn, draft_journal_entry]\n---\n\n# ${s.name}\n\n## When to use this skill\n\n- The task matches the description above.\n- A relevant domain event is in scope and the preconditions hold.\n\n## Procedure\n\n1. Gather the candidates with the read tools.\n2. Score / transform per the rules in references/policy.md.\n3. Only auto-apply when confidence ≥ 0.95 and within policy ceilings.\n4. Otherwise return the top results for human review.\n\n## Anti-patterns\n\nSee references/idioms.md for cases this skill must NOT touch.\n`;
}

const ADX_SKILL_TREE = [
  { type: 'file',   path: 'SKILL.md', size: '1.8 KB' },
  { type: 'folder', path: 'scripts', children: [
    { type: 'file', path: 'scripts/score.py', size: '3.2 KB' },
    { type: 'file', path: 'scripts/normalize.py', size: '1.4 KB' },
  ] },
  { type: 'folder', path: 'references', children: [
    { type: 'file', path: 'references/policy.md', size: '2.1 KB' },
    { type: 'file', path: 'references/idioms.md', size: '1.0 KB' },
  ] },
  { type: 'folder', path: 'assets', children: [
    { type: 'file', path: 'assets/proposal.tmpl', size: '0.6 KB' },
  ] },
];

function BlendField({ children }) {
  return <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 8 }}>{children}</div>;
}
function BlendCode({ children }) {
  return <pre style={{ margin: 0, fontFamily: 'var(--font-mono)', fontSize: 11, lineHeight: 1.55, background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 8, padding: 12, color: 'var(--text-primary)', whiteSpace: 'pre-wrap' }}>{children}</pre>;
}
function BlendMd({ md }) {
  return (
    <div style={{ background: '#1a1d21', borderRadius: 8, fontFamily: 'var(--font-mono)', fontSize: 11, lineHeight: 1.55, color: '#d8dde2', maxHeight: 280, overflow: 'auto', padding: '10px 12px' }}>
      {md.split('\n').map((line, i) => {
        let color = '#d8dde2';
        if (line.startsWith('---')) color = '#7b76b6';
        else if (line.match(/^[a-z-]+:/i)) color = '#5facbd';
        else if (line.startsWith('#')) color = '#eb9b6b';
        else if (line.startsWith('- ')) color = '#a8dbcb';
        return <div key={i} style={{ color, whiteSpace: 'pre-wrap' }}>{line || ' '}</div>;
      })}
    </div>
  );
}

function BlendDetailDrawer({ node, agent, onClose }) {
  if (!node) return null;
  const ty = LP_TYPE[node.type];
  const d = node.data;
  const name = node.type === 'agent' ? d.sub.name : node.label;
  const subtitle = node.type === 'tool' ? `${d.cat} tool · schema v2.1.0`
    : node.type === 'skill' ? `${d.source} · v${d.version}`
    : node.type === 'agent' ? `sub-agent · ${d.autonomy === 'auto' ? 'auto-apply' : d.autonomy}`
    : d.url;
  const mono = node.type === 'tool' || node.type === 'skill';
  return (
    <>
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(20,25,30,0.28)', zIndex: 30 }} />
      <div style={{ position: 'absolute', top: 0, right: 0, bottom: 0, width: 560, background: 'var(--surface)', borderLeft: '1px solid var(--border-light)', boxShadow: '-12px 0 32px -8px rgba(0,0,0,0.18)', zIndex: 31, display: 'flex', flexDirection: 'column' }}>
        <div style={{ padding: '18px 22px 14px', borderBottom: '1px solid var(--border-light)', display: 'flex', alignItems: 'center', gap: 12 }}>
          {node.type === 'agent'
            ? <AgentPortrait agent={d.sub} size={40} ring={false} />
            : <span style={{ width: 40, height: 40, borderRadius: 10, background: ty.color + '16', color: ty.color, display: 'grid', placeItems: 'center' }}><Icon name={ty.icon} size={19} /></span>}
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ fontFamily: mono ? 'var(--font-mono)' : 'inherit', fontSize: 15, fontWeight: 600, color: 'var(--text-primary)' }}>{name}</span>
              <span style={{ fontSize: 9.5, fontWeight: 700, letterSpacing: '0.06em', textTransform: 'uppercase', color: ty.color, padding: '2px 7px', borderRadius: 4, background: ty.color + '16' }}>{ty.label}</span>
            </div>
            <div style={{ fontSize: 11, color: 'var(--text-tertiary)', marginTop: 2, fontFamily: node.type === 'server' ? 'var(--font-mono)' : 'inherit', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{subtitle}</div>
          </div>
          <button className="btn btn-ghost btn-sm" onClick={onClose} style={{ padding: 6 }}><Icon name="close" size={14} color="var(--text-secondary)" /></button>
        </div>

        <div style={{ flex: 1, overflow: 'auto', padding: 22, display: 'flex', flexDirection: 'column', gap: 18 }}>
          {node.type === 'tool' && <BlendToolBody t={d} />}
          {node.type === 'skill' && <BlendSkillBody s={d} />}
          {node.type === 'agent' && <BlendAgentBody s={d} />}
          {node.type === 'server' && <BlendServerBody s={d} />}
        </div>

        <div style={{ flex: 'none', padding: '14px 22px', borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <button className="btn btn-ghost btn-sm" onClick={onClose}>Close</button>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-outline btn-sm"><Icon name="edit" size={12} /> Edit</button>
            <button className="btn btn-primary btn-sm"><Icon name="play" size={12} /> Test in Playground</button>
          </div>
        </div>
      </div>
    </>
  );
}

function BlendToolBody({ t }) {
  const sch = adxToolSchema(t);
  const calls = [
    { s: 'ok', ref: 'INV-2041', ms: '142ms', at: 'now' },
    { s: 'ok', ref: 'INV-2038', ms: '88ms', at: '11m' },
    { s: 'ok', ref: 'INV-2037', ms: '211ms', at: '24m' },
    { s: 'err', ref: 'INV-2031', ms: '318ms', at: '1h' },
    { s: 'ok', ref: 'INV-2024', ms: '142ms', at: '2h' },
  ];
  return (
    <>
      <p style={{ margin: 0, fontSize: 13, color: 'var(--text-secondary)', lineHeight: 1.6 }}>{t.desc}</p>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 8 }}>
        <TopoStat l="Uses · 24h" v={t.uses.toLocaleString()} />
        <TopoStat l="p99" v={t.p99} />
        <TopoStat l="Enabled" v={t.enabled ? 'Yes' : 'No'} tone={t.enabled ? 'var(--success)' : 'var(--text-tertiary)'} />
      </div>
      <div><BlendField>Input schema</BlendField><BlendCode>{sch.input}</BlendCode></div>
      <div><BlendField>Returns</BlendField><BlendCode>{sch.returns}</BlendCode></div>
      <div>
        <BlendField>Recent invocations</BlendField>
        <div style={{ borderRadius: 10, border: '1px solid var(--border-light)', overflow: 'hidden' }}>
          {calls.map((r, i) => (
            <div key={i} style={{ display: 'grid', gridTemplateColumns: '34px 1fr 70px 50px', gap: 10, alignItems: 'center', padding: '9px 12px', borderTop: i ? '1px solid var(--border-light)' : 'none' }}>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 9, fontWeight: 700, textTransform: 'uppercase', textAlign: 'center', padding: '2px 0', borderRadius: 3, color: r.s === 'ok' ? 'var(--success)' : 'var(--danger)', background: (r.s === 'ok' ? 'var(--success)' : 'var(--danger)') + '1a' }}>{r.s}</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-primary)' }}>{r.ref}</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)', textAlign: 'right' }}>{r.ms}</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)', textAlign: 'right' }}>{r.at}</span>
            </div>
          ))}
        </div>
      </div>
    </>
  );
}

function BlendSkillBody({ s }) {
  return (
    <>
      <p style={{ margin: 0, fontSize: 13, color: 'var(--text-secondary)', lineHeight: 1.6 }}>{s.desc}</p>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 8 }}>
        <TopoStat l="Activations · 24h" v={s.last24h} />
        <TopoStat l="Source" v={s.source} />
        <TopoStat l="Status" v={s.status} tone={s.status === 'beta' ? '#b4741e' : 'var(--success)'} />
      </div>
      <div><BlendField>SKILL.md · loaded on activation</BlendField><BlendMd md={adxSkillMd(s)} /></div>
      <div><BlendField>Bundle</BlendField><SkillFileTree tree={ADX_SKILL_TREE} accent="#055a60" /></div>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px 18px', fontSize: 11, color: 'var(--text-tertiary)', borderTop: '1px solid var(--border-light)', paddingTop: 12 }}>
        <span>version <b style={{ color: 'var(--text-secondary)' }}>v{s.version}</b></span>
        <span>installed <b style={{ color: 'var(--text-secondary)' }}>2 weeks ago</b></span>
        <span>visibility <b style={{ color: 'var(--text-secondary)' }}>{s.source}</b></span>
      </div>
    </>
  );
}

function BlendAgentBody({ s }) {
  const tone = { auto: 'success', propose: 'warning', block: 'tint' }[s.autonomy];
  const tl = { auto: 'Auto-apply', propose: 'Propose', block: 'Required' }[s.autonomy];
  const log = [
    { op: 'apply_journal_entry', at: 'now', ms: '142ms', s: 'ok' },
    { op: 'apply_journal_entry', at: '11m', ms: '188ms', s: 'held' },
    { op: 'kyc_recheck', at: '24m', ms: '894ms', s: 'ok' },
    { op: 'queue_reminder', at: '1h', ms: '142ms', s: 'ok' },
  ];
  const lt = { ok: 'var(--success)', held: 'var(--warning)', err: 'var(--danger)' };
  return (
    <>
      <p style={{ margin: 0, fontSize: 13, color: 'var(--text-secondary)', lineHeight: 1.6 }}>{s.role}</p>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <Pill tone={tone} size="sm">{tl}</Pill>
        <span style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>Calls run under {s.sub.name}'s own policies.</span>
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 8 }}>
        <TopoStat l="Calls · 24h" v={s.calls} />
        <TopoStat l="Success" v={s.success + '%'} tone={s.success >= 98 ? 'var(--success)' : 'var(--warning)'} />
        <TopoStat l="Avg latency" v={s.avgMs + 'ms'} />
      </div>
      <div>
        <BlendField>Recent delegations</BlendField>
        <div style={{ borderRadius: 10, border: '1px solid var(--border-light)', overflow: 'hidden' }}>
          {log.map((r, i) => (
            <div key={i} style={{ display: 'grid', gridTemplateColumns: '40px 1fr 60px 44px', gap: 10, alignItems: 'center', padding: '9px 12px', borderTop: i ? '1px solid var(--border-light)' : 'none' }}>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 9, fontWeight: 700, textTransform: 'uppercase', textAlign: 'center', padding: '2px 0', borderRadius: 3, color: lt[r.s], background: lt[r.s] + '1a' }}>{r.s}</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-primary)' }}>{r.op}</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)', textAlign: 'right' }}>{r.ms}</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)', textAlign: 'right' }}>{r.at}</span>
            </div>
          ))}
        </div>
      </div>
    </>
  );
}

function BlendServerBody({ s }) {
  const stc = s.status === 'connected' ? 'var(--success)' : s.status === 'error' ? 'var(--danger)' : 'var(--warning)';
  return (
    <>
      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-secondary)', wordBreak: 'break-all', padding: '10px 12px', background: 'var(--surface-inset)', borderRadius: 8 }}>{s.url}</div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12.5, fontWeight: 600, color: stc }}>
        <span style={{ width: 8, height: 8, borderRadius: 9, background: stc }} />{s.status}
        {s.native && <span style={{ fontSize: 9, fontWeight: 700, color: 'var(--brand-primary)', padding: '1px 6px', borderRadius: 3, background: 'var(--brand-primary-10)' }}>NATIVE</span>}
      </div>
      {s.err && (
        <div style={{ padding: '10px 12px', background: '#c4453608', border: '1px solid #c4453633', borderRadius: 8, fontSize: 11.5, color: '#c44536', display: 'flex', alignItems: 'center', gap: 8 }}>
          <Icon name="warn" size={13} color="#c44536" /> {s.err}
        </div>
      )}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 8 }}>
        <TopoStat l="Tools" v={s.tools} />
        <TopoStat l="Auth" v={s.auth} />
        <TopoStat l="Latency" v={s.latency} />
      </div>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px 18px', fontSize: 11, color: 'var(--text-tertiary)', borderTop: '1px solid var(--border-light)', paddingTop: 12 }}>
        <span>last sync <b style={{ color: 'var(--text-secondary)' }}>{s.status === 'connected' ? '12s ago' : s.status === 'error' ? 'failed 18m ago' : 'in progress'}</b></span>
        <span>protocol <b style={{ color: 'var(--text-secondary)' }}>MCP v1.4.2</b></span>
      </div>
    </>
  );
}

Object.assign(window, { ScreenAgentDetailBlend });
