// Agents page — built-in AI agents roster with card layout + slide-out edit panel.
// Two layout modes: 'card' (rich tiles, default) and 'grid' (denser list).
//
// Profile images are inline SVG portraits — each agent gets a distinctive,
// generative composition built from geometric forms tinted with the agent's hue.
// Avoids photo-realistic / human imagery for AI agents (which would feel wrong)
// while still giving each agent a unique recognizable "face".

// ─── Agent data ─────────────────────────────────────────────────────
const AGENT_LIST = [
  {
    id: 'orch', name: 'Orchestrator', glyph: 'orbital',
    color: '#055a60', tagline: 'Routes traffic across every agent',
    description: 'Top-level conductor. Decides which domain agent handles each request, holds policies, escalates to humans when confidence drops.',
    model: 'claude-opus-4',  temp: 0.2,
    state: 'running', runs: 1842, conf: 0.99, lastRun: 'now',
    tools: 14, policies: 4, kind: 'System', pinned: true,
    autoApply: false,
  },
  {
    id: 'ledger', name: 'Ledger Agent', glyph: 'columns',
    color: '#055a60', tagline: 'Books · journal entries · close',
    description: 'Drafts journal entries from settled transactions, runs balance checks, prepares the month-end close package.',
    model: 'claude-sonnet-4.5', temp: 0.1,
    state: 'idle', runs: 142, conf: 0.96, lastRun: '2m ago',
    tools: 9, policies: 6, kind: 'Domain',
    autoApply: true,
  },
  {
    id: 'billing', name: 'Billing Agent', glyph: 'docstack',
    color: '#eb5c37', tagline: 'Invoices · matching · dunning',
    description: 'Reconciles invoices against bank transactions, surfaces mismatches, drafts dunning emails for overdue receivables.',
    model: 'claude-sonnet-4.5', temp: 0.2,
    state: 'running', runs: 318, conf: 0.94, lastRun: 'now',
    tools: 11, policies: 5, kind: 'Domain',
    autoApply: true,
  },
  {
    id: 'fx', name: 'FX Agent', glyph: 'wave',
    color: '#3ab795', tagline: 'Cross-border rates + hedging',
    description: 'Reads live rate feeds, recommends hedging windows, drafts forward-contract proposals before any cross-border payout.',
    model: 'claude-sonnet-4.5', temp: 0.3,
    state: 'idle', runs: 84, conf: 0.91, lastRun: '12m ago',
    tools: 6, policies: 3, kind: 'Domain',
    autoApply: false,
  },
  {
    id: 'compl', name: 'Compliance Agent', glyph: 'shield',
    color: '#7b76b6', tagline: 'KYC · sanctions · audit trail',
    description: 'Runs sanctions and PEP screening on every counterparty, flags KYB anomalies, maintains the immutable audit log.',
    model: 'claude-opus-4', temp: 0.0,
    state: 'idle', runs: 42, conf: 0.98, lastRun: '1h ago',
    tools: 8, policies: 9, kind: 'Domain',
    autoApply: false,
  },
  {
    id: 'close', name: 'Close Agent', glyph: 'rings',
    color: '#0097a9', tagline: 'Month-end close orchestrator',
    description: 'Sequences the close playbook end-to-end: accruals, reclasses, FX revaluation, intercompany eliminations, sign-off.',
    model: 'claude-opus-4', temp: 0.1,
    state: 'running', runs: 6, conf: 0.89, lastRun: 'now',
    tools: 12, policies: 7, kind: 'Domain',
    autoApply: false,
  },
  {
    id: 'dunn', name: 'Dunning Agent', glyph: 'envelope',
    color: '#5facbd', tagline: 'Overdue outreach · cadenced',
    description: 'Composes politely-firm reminder emails on a per-customer cadence, escalates to phone scripts after the third reminder.',
    model: 'claude-haiku-4', temp: 0.4,
    state: 'paused', runs: 28, conf: 0.87, lastRun: '3h ago',
    tools: 5, policies: 4, kind: 'Domain',
    autoApply: false,
  },
  {
    id: 'insights', name: 'Insights Agent', glyph: 'pulse',
    color: '#d4a843', tagline: 'Spend · variance · narratives',
    description: 'Answers "where is my money going?" — generates pie + budget tool cards, writes a 3-line narrative for board reports.',
    model: 'claude-sonnet-4.5', temp: 0.5,
    state: 'idle', runs: 211, conf: 0.92, lastRun: '8m ago',
    tools: 7, policies: 2, kind: 'Domain',
    autoApply: true,
  },
];

const STATE_DOT = {
  running: { c: 'var(--success)',     t: 'Running' },
  idle:    { c: 'var(--gray-400)',    t: 'Idle' },
  paused:  { c: 'var(--warning)',     t: 'Paused' },
};

// ─── Profile portrait (inline SVG, generative, per-glyph) ──────────
// Each agent renders an 80x80 (or scalable) avatar:
//   - Tinted gradient field background from agent.color
//   - A unique geometric composition keyed on `glyph`
//   - Soft inner ring
//
function AgentPortrait({ agent, size = 64, ring = true }) {
  const c = agent.color;
  const id = `pt-${agent.id}`;
  return (
    <svg width={size} height={size} viewBox="0 0 80 80" style={{ flex: 'none', display: 'block', borderRadius: size * 0.18, overflow: 'hidden' }}>
      <defs>
        <linearGradient id={`${id}-bg`} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%"  stopColor={c} stopOpacity="0.95"/>
          <stop offset="100%" stopColor={c} stopOpacity="0.55"/>
        </linearGradient>
        <radialGradient id={`${id}-glow`} cx="0.7" cy="0.25" r="0.7">
          <stop offset="0%" stopColor="#fff" stopOpacity="0.35"/>
          <stop offset="100%" stopColor="#fff" stopOpacity="0"/>
        </radialGradient>
        <clipPath id={`${id}-clip`}>
          <rect x="0" y="0" width="80" height="80" rx={size * 0.18 * (80/size)}/>
        </clipPath>
      </defs>
      <g clipPath={`url(#${id}-clip)`}>
        <rect width="80" height="80" fill={`url(#${id}-bg)`}/>
        <rect width="80" height="80" fill={`url(#${id}-glow)`}/>
        {/* subtle grain dots */}
        {Array.from({ length: 18 }).map((_, i) => {
          const x = (i * 37) % 80, y = (i * 59) % 80;
          return <circle key={i} cx={x} cy={y} r="0.4" fill="#fff" opacity="0.15"/>;
        })}
        <PortraitGlyph glyph={agent.glyph} color={c}/>
      </g>
      {ring && <rect x="0.5" y="0.5" width="79" height="79" rx={size * 0.18 * (80/size)} fill="none" stroke="#fff" strokeOpacity="0.15"/>}
    </svg>
  );
}

function PortraitGlyph({ glyph, color }) {
  const w = '#fff';
  switch (glyph) {
    case 'orbital':
      return (
        <g>
          <ellipse cx="40" cy="40" rx="26" ry="10" fill="none" stroke={w} strokeWidth="1.2" opacity="0.55"/>
          <ellipse cx="40" cy="40" rx="26" ry="10" fill="none" stroke={w} strokeWidth="1.2" opacity="0.4" transform="rotate(60 40 40)"/>
          <ellipse cx="40" cy="40" rx="26" ry="10" fill="none" stroke={w} strokeWidth="1.2" opacity="0.4" transform="rotate(-60 40 40)"/>
          <circle cx="40" cy="40" r="6" fill={w}/>
          <circle cx="40" cy="40" r="3" fill={color}/>
        </g>
      );
    case 'columns':
      return (
        <g fill={w}>
          <rect x="22" y="26" width="4"  height="28" rx="1" opacity="0.95"/>
          <rect x="30" y="34" width="4"  height="20" rx="1" opacity="0.85"/>
          <rect x="38" y="22" width="4"  height="32" rx="1"/>
          <rect x="46" y="30" width="4"  height="24" rx="1" opacity="0.85"/>
          <rect x="54" y="38" width="4"  height="16" rx="1" opacity="0.7"/>
          <line x1="20" y1="58" x2="60" y2="58" stroke={w} strokeWidth="1" opacity="0.5"/>
        </g>
      );
    case 'docstack':
      return (
        <g>
          <rect x="22" y="22" width="34" height="40" rx="3" fill={w} opacity="0.18" transform="rotate(-6 39 42)"/>
          <rect x="24" y="20" width="34" height="40" rx="3" fill={w} opacity="0.55" transform="rotate(-2 41 40)"/>
          <rect x="26" y="20" width="32" height="40" rx="3" fill={w} opacity="0.95"/>
          <line x1="30" y1="30" x2="50" y2="30" stroke={color} strokeWidth="1.8" strokeLinecap="round"/>
          <line x1="30" y1="36" x2="46" y2="36" stroke={color} strokeWidth="1.8" strokeLinecap="round" opacity="0.7"/>
          <line x1="30" y1="42" x2="48" y2="42" stroke={color} strokeWidth="1.8" strokeLinecap="round" opacity="0.7"/>
          <line x1="30" y1="48" x2="40" y2="48" stroke={color} strokeWidth="1.8" strokeLinecap="round" opacity="0.5"/>
        </g>
      );
    case 'wave':
      return (
        <g fill="none" stroke={w} strokeLinecap="round" strokeWidth="2">
          <path d="M14 50 Q24 36 32 44 T54 38 Q62 35 66 30" opacity="0.85"/>
          <path d="M14 56 Q24 44 32 50 T54 44 Q62 41 66 38" opacity="0.55"/>
          <circle cx="54" cy="38" r="3.4" fill={w} stroke="none"/>
        </g>
      );
    case 'shield':
      return (
        <g>
          <path d="M40 18 L58 25 L58 42 Q58 56 40 62 Q22 56 22 42 L22 25 Z" fill={w} opacity="0.95"/>
          <path d="M32 40 L38 46 L50 32" fill="none" stroke={color} strokeWidth="3" strokeLinecap="round" strokeLinejoin="round"/>
        </g>
      );
    case 'rings':
      return (
        <g fill="none" stroke={w} strokeWidth="2">
          <circle cx="40" cy="40" r="22" opacity="0.35"/>
          <circle cx="40" cy="40" r="14" opacity="0.6"/>
          <circle cx="40" cy="40" r="6" fill={w} stroke="none"/>
          <path d="M40 18 A22 22 0 0 1 62 40" stroke={w} strokeWidth="2.5" strokeLinecap="round"/>
        </g>
      );
    case 'envelope':
      return (
        <g>
          <rect x="20" y="26" width="40" height="28" rx="3" fill={w} opacity="0.95"/>
          <path d="M20 28 L40 44 L60 28" fill="none" stroke={color} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
          <circle cx="58" cy="50" r="6" fill={color}/>
          <circle cx="58" cy="50" r="2.2" fill={w}/>
        </g>
      );
    case 'pulse':
      return (
        <g fill="none" stroke={w} strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round">
          <path d="M14 42 H26 L30 32 L36 52 L42 36 L48 46 L54 42 H66" opacity="0.95"/>
          <circle cx="48" cy="46" r="3" fill={w} stroke="none"/>
        </g>
      );
    default:
      return <circle cx="40" cy="40" r="14" fill={w}/>;
  }
}

// ─── Small helpers ────────────────────────────────────────────────
function StateDot({ state }) {
  const s = STATE_DOT[state] || STATE_DOT.idle;
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
      <span style={{
        width: 7, height: 7, borderRadius: 999, background: s.c,
        boxShadow: state === 'running' ? `0 0 0 3px ${s.c}33` : 'none',
      }}/>
      <span style={{ fontSize: 11, color: 'var(--text-secondary)', fontWeight: 500 }}>{s.t}</span>
    </span>
  );
}

function MetaItem({ label, value, mono = false }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 2, minWidth: 0 }}>
      <span style={{ fontSize: 10, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase' }}>{label}</span>
      <span style={{
        fontSize: 12.5, color: 'var(--text-primary)', fontWeight: 500,
        fontFamily: mono ? 'var(--font-mono)' : 'inherit',
        fontVariantNumeric: mono ? 'tabular-nums' : 'normal',
        whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
      }}>{value}</span>
    </div>
  );
}

// ─── Card layout ────────────────────────────────────────────────────
function AgentCard({ agent, onEdit }) {
  return (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12,
      padding: 18, display: 'flex', flexDirection: 'column', gap: 14,
      boxShadow: 'var(--shadow-sm)', position: 'relative',
    }}>
      {/* coral pin for the orchestrator */}
      {agent.pinned && (
        <span style={{
          position: 'absolute', left: -1, top: 18, bottom: 18, width: 3,
          background: 'var(--brand-secondary)', borderRadius: 999,
        }}/>
      )}

      <div style={{ display: 'flex', gap: 14, alignItems: 'flex-start' }}>
        <AgentPortrait agent={agent} size={64}/>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
            <span style={{ fontSize: 15, fontWeight: 600, color: 'var(--text-primary)', letterSpacing: '-0.005em' }}>{agent.name}</span>
            <Pill tone="tint" size="sm">{agent.kind}</Pill>
          </div>
          <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 2 }}>{agent.tagline}</div>
          <div style={{ marginTop: 8 }}><StateDot state={agent.state}/></div>
        </div>
        <button className="hover-halo" onClick={onEdit} title="Edit agent" style={{ padding: 6 }}>
          <Icon name="edit" size={14} color="var(--text-secondary)"/>
        </button>
      </div>

      <p style={{ fontSize: 12, lineHeight: 1.55, color: 'var(--text-secondary)', margin: 0, textWrap: 'pretty' }}>
        {agent.description}
      </p>

      <div style={{
        display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12,
        paddingTop: 12, borderTop: '1px solid var(--border-light)',
      }}>
        <MetaItem label="Model"     value={agent.model}/>
        <MetaItem label="Tools"     value={agent.tools} mono/>
        <MetaItem label="Runs · 7d" value={agent.runs.toLocaleString()} mono/>
        <MetaItem label="Conf."     value={agent.conf.toFixed(2)} mono/>
      </div>

      <div style={{
        display: 'flex', alignItems: 'center', gap: 8,
        paddingTop: 4,
      }}>
        {agent.autoApply ? (
          <Pill tone="success" dot size="sm">Auto-apply</Pill>
        ) : (
          <Pill tone="tint" size="sm">Propose only</Pill>
        )}
        <span style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>
          last run {agent.lastRun}
        </span>
        <div style={{ flex: 1 }}/>
        <button className="btn btn-ghost btn-sm" style={{ height: 26, fontSize: 11 }} onClick={onEdit}>
          Configure <Icon name="arrowright" size={11}/>
        </button>
      </div>
    </div>
  );
}

// ─── Compact grid (denser list) row ────────────────────────────────
function AgentGridRow({ agent, onEdit }) {
  return (
    <div style={{
      display: 'grid', gridTemplateColumns: '44px 1fr 130px 80px 80px 90px 28px',
      gap: 14, alignItems: 'center', padding: '10px 14px',
      background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10,
      cursor: 'pointer',
    }} onClick={onEdit}>
      <AgentPortrait agent={agent} size={36}/>
      <div style={{ minWidth: 0 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{agent.name}</span>
          <Pill tone="tint" size="sm">{agent.kind}</Pill>
        </div>
        <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 1 }}>{agent.tagline}</div>
      </div>
      <span style={{ fontSize: 11.5, color: 'var(--text-secondary)', fontFamily: 'var(--font-mono)' }}>{agent.model}</span>
      <span style={{ fontSize: 11.5, fontFamily: 'var(--font-mono)', textAlign: 'right', color: 'var(--text-primary)', fontVariantNumeric: 'tabular-nums' }}>{agent.runs.toLocaleString()}</span>
      <span style={{ fontSize: 11.5, fontFamily: 'var(--font-mono)', textAlign: 'right', color: 'var(--text-secondary)', fontVariantNumeric: 'tabular-nums' }}>{agent.conf.toFixed(2)}</span>
      <StateDot state={agent.state}/>
      <Icon name="chevron" size={14} color="var(--text-tertiary)"/>
    </div>
  );
}

// ─── Slide-out edit panel ──────────────────────────────────────────
function AgentEditPanel({ agent, onClose }) {
  if (!agent) return null;
  const tabs = ['Identity', 'Prompt', 'Tools', 'Policies', 'Triggers'];
  const [tab, setTab] = React.useState('Identity');

  return (
    <>
      {/* scrim */}
      <div onClick={onClose} style={{
        position: 'absolute', inset: 0, background: 'rgba(20, 25, 30, 0.28)',
      }}/>
      {/* panel */}
      <div style={{
        position: 'absolute', top: 0, right: 0, bottom: 0, width: 540,
        background: 'var(--surface)', borderLeft: '1px solid var(--border-light)',
        boxShadow: '-12px 0 32px -8px rgb(0 0 0 / 0.18)',
        display: 'flex', flexDirection: 'column',
      }}>
        {/* Header */}
        <div style={{
          padding: '18px 22px 14px', borderBottom: '1px solid var(--border-light)',
          display: 'flex', alignItems: 'center', gap: 14,
        }}>
          <AgentPortrait agent={agent} size={52}/>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ fontSize: 15, fontWeight: 600, color: 'var(--text-primary)' }}>{agent.name}</span>
              <Pill tone="tint" size="sm">{agent.kind}</Pill>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginTop: 4 }}>
              <StateDot state={agent.state}/>
              <span style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>· id {agent.id}</span>
            </div>
          </div>
          <button className="hover-halo" onClick={onClose} title="Close" style={{ padding: 6 }}>
            <Icon name="close" size={14} color="var(--text-secondary)"/>
          </button>
        </div>

        {/* Tabs */}
        <div style={{
          flex: 'none', padding: '0 22px',
          borderBottom: '1px solid var(--border-light)',
          display: 'flex', gap: 4,
        }}>
          {tabs.map(t => {
            const active = t === tab;
            return (
              <button key={t} onClick={() => setTab(t)}
                style={{
                  padding: '10px 12px', fontSize: 12, fontWeight: active ? 600 : 500,
                  color: active ? 'var(--text-primary)' : 'var(--text-secondary)',
                  border: 'none', background: 'transparent', cursor: 'pointer',
                  borderBottom: `2px solid ${active ? 'var(--brand-primary)' : 'transparent'}`,
                  marginBottom: -1,
                }}>{t}</button>
            );
          })}
        </div>

        {/* Body */}
        <div style={{ flex: 1, overflow: 'auto', padding: 22, display: 'flex', flexDirection: 'column', gap: 16 }}>
          {tab === 'Identity'  && <EditIdentity agent={agent}/>}
          {tab === 'Prompt'    && <EditPrompt agent={agent}/>}
          {tab === 'Tools'     && <EditTools agent={agent}/>}
          {tab === 'Policies'  && <EditPolicies agent={agent}/>}
          {tab === 'Triggers'  && <EditTriggers agent={agent}/>}
        </div>

        {/* Footer */}
        <div style={{
          flex: 'none', padding: '14px 22px',
          borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)',
          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        }}>
          <button className="btn btn-ghost btn-sm" style={{ color: 'var(--danger)' }}>
            <Icon name="trash" size={12}/> Delete agent
          </button>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-outline btn-sm" onClick={onClose}>Cancel</button>
            <button className="btn btn-primary btn-sm">
              <Icon name="check" size={12}/> Save changes
            </button>
          </div>
        </div>
      </div>
    </>
  );
}

// ── Edit panel: Identity tab (profile, name, description) ─────────
function EditIdentity({ agent }) {
  return (
    <>
      {/* Profile picture editor */}
      <div>
        <FieldLabel hint="Generated · keyed on agent ID">Profile image</FieldLabel>
        <div style={{
          display: 'flex', gap: 14, alignItems: 'center',
          padding: 14, background: 'var(--surface-inset)',
          border: '1px solid var(--border-light)', borderRadius: 10,
        }}>
          <AgentPortrait agent={agent} size={72}/>
          <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 4 }}>
            <span style={{ fontSize: 12, color: 'var(--text-primary)', fontWeight: 500 }}>
              Style · <span style={{ color: 'var(--brand-primary)', textTransform: 'capitalize' }}>{agent.glyph}</span>
            </span>
            <span style={{ fontSize: 11, color: 'var(--text-secondary)', lineHeight: 1.5 }}>
              Each agent gets a deterministic glyph portrait. Re-roll, pick a glyph, or upload a custom mark.
            </span>
            <div style={{ display: 'flex', gap: 6, marginTop: 6 }}>
              <button className="btn btn-outline btn-sm" style={{ height: 26, padding: '0 10px', fontSize: 11 }}>
                <Icon name="refresh" size={11}/> Re-roll
              </button>
              <button className="btn btn-ghost btn-sm" style={{ height: 26, padding: '0 10px', fontSize: 11 }}>
                <Icon name="upload" size={11}/> Upload
              </button>
            </div>
          </div>
        </div>

        {/* Glyph swatches */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(8, 1fr)', gap: 6, marginTop: 10 }}>
          {['orbital','columns','docstack','wave','shield','rings','envelope','pulse'].map(g => (
            <div key={g} title={g} style={{
              cursor: 'pointer', borderRadius: 8, padding: 2,
              border: `1.5px solid ${g === agent.glyph ? 'var(--brand-primary)' : 'transparent'}`,
            }}>
              <AgentPortrait agent={{ ...agent, glyph: g }} size={42} ring={false}/>
            </div>
          ))}
        </div>

        {/* Color */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 12 }}>
          <span style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginRight: 4 }}>Hue</span>
          {['#055a60','#eb5c37','#3ab795','#7b76b6','#0097a9','#5facbd','#d4a843'].map(c => (
            <span key={c} style={{
              width: 22, height: 22, borderRadius: 999, background: c, cursor: 'pointer',
              boxShadow: c === agent.color ? '0 0 0 2px var(--surface), 0 0 0 4px var(--brand-primary)' : 'none',
            }}/>
          ))}
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <TextField label="Name"   required value={agent.name}/>
        <TextField label="Handle" required mono prefix="@" value={agent.id}/>
      </div>

      <TextField label="Tagline" value={agent.tagline} hint="Shown on cards" helper="Keep under 60 characters · sentence case"/>

      <TextArea
        label="Description" rows={4}
        value={agent.description}
        helper="Plain English. What this agent is for, who it talks to, when to use it."
      />
    </>
  );
}

// ── Edit panel: Prompt tab (system prompt + model) ────────────────
function EditPrompt({ agent }) {
  return (
    <>
      <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: 12 }}>
        <SelectField label="Model" required value={agent.model}/>
        <TextField label="Temperature" mono value={String(agent.temp)} suffix="0–2"/>
      </div>

      <div>
        <FieldLabel required hint={`${(agent.description || '').length + 220} chars`}>System prompt</FieldLabel>
        <div style={{
          background: 'var(--surface)', border: '1px solid var(--border)',
          borderBottom: '2px solid var(--border)', borderRadius: 'var(--radius-md)',
          padding: 12,
        }}>
          <textarea
            rows={11}
            defaultValue={`You are Aonik's ${agent.name}.

Role
  ${agent.tagline}.

Guardrails
  • Require explicit user confirmation for any action above £50,000.
  • Never mutate closed periods. Propose a reversing entry instead.
  • When a counterparty is flagged KYC-pending, pause and surface a
    confirmAction card.

Response style
  Be concise. Prefer tool cards over prose. Always cite the specific
  invoice / txn ID in your reply.`}
            spellCheck={false}
            style={{
              width: '100%', boxSizing: 'border-box', border: 'none',
              background: 'transparent', outline: 'none', resize: 'vertical',
              fontFamily: 'var(--font-mono)', fontSize: 12, lineHeight: 1.55,
              color: 'var(--text-primary)', padding: 0,
            }}
          />
        </div>
        <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 6 }}>
          <button className="btn btn-ghost btn-sm" style={{ height: 24, padding: '0 6px', fontSize: 11, color: 'var(--brand-primary)' }}>
            <Icon name="sparkles" size={11}/> Regenerate with AI
          </button>
          <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>
            est. ~ 180 input tokens
          </span>
        </div>
      </div>

      <div style={{
        padding: 12, background: 'var(--brand-primary-10)',
        borderRadius: 8, display: 'flex', alignItems: 'flex-start', gap: 10,
      }}>
        <Icon name="sparkles" size={14} color="var(--brand-primary)"/>
        <div style={{ fontSize: 12, color: 'var(--text-primary)', flex: 1, lineHeight: 1.55 }}>
          Test in <b>Playground</b> before saving — agents propose, but humans approve every change.
        </div>
        <button className="btn btn-outline btn-sm" style={{ height: 24, padding: '0 8px', fontSize: 11 }}>
          Open in Playground
        </button>
      </div>
    </>
  );
}

// ── Edit panel: Tools tab (toggleable list) ───────────────────────
function EditTools({ agent }) {
  const tools = [
    { name: 'search_invoices',         desc: 'Query the invoice store by counterparty, status, or amount.', enabled: true,  cat: 'read' },
    { name: 'list_bank_transactions',  desc: 'Read bank txns inside a date window.',                       enabled: true,  cat: 'read' },
    { name: 'match_invoice_to_txn',    desc: 'Score a candidate match (0–1).',                             enabled: true,  cat: 'read' },
    { name: 'draft_journal_entry',     desc: 'Compose a balanced debit/credit pair (proposal only).',      enabled: true,  cat: 'write' },
    { name: 'apply_journal_entry',     desc: 'Post a drafted entry to the ledger.',                        enabled: false, cat: 'write' },
    { name: 'display_autopilot_proposal', desc: 'Render an Apply / Review / Dismiss tool card.',           enabled: true,  cat: 'display' },
    { name: 'confirmAction',           desc: 'Halt and ask the human for approval.',                       enabled: true,  cat: 'display' },
    { name: 'send_dunning_email',      desc: 'Compose and dispatch an overdue reminder.',                  enabled: false, cat: 'write' },
  ];
  const catColor = { read: 'var(--brand-primary)', write: 'var(--brand-secondary)', display: 'var(--accent-violet, #7b76b6)' };

  return (
    <>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <span style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{tools.filter(t => t.enabled).length} of {tools.length} enabled</span>
        <div style={{ flex: 1 }}/>
        <button className="btn btn-outline btn-sm" style={{ height: 26, padding: '0 10px', fontSize: 11 }}>
          <Icon name="plus" size={11}/> Add tool
        </button>
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        {tools.map((t, i) => (
          <div key={i} style={{
            display: 'grid', gridTemplateColumns: '8px 1fr auto', gap: 12, alignItems: 'center',
            padding: '10px 12px', background: 'var(--surface-inset)',
            border: '1px solid var(--border-light)', borderRadius: 8,
            opacity: t.enabled ? 1 : 0.55,
          }}>
            <span style={{ width: 6, height: 6, borderRadius: 999, background: catColor[t.cat] }}/>
            <div>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, fontWeight: 600, color: 'var(--text-primary)' }}>{t.name}</div>
              <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 2 }}>{t.desc}</div>
            </div>
            <span style={{
              width: 28, height: 16, borderRadius: 999, padding: 2, flex: 'none',
              background: t.enabled ? 'var(--brand-primary)' : 'var(--gray-300)',
              display: 'inline-flex', alignItems: 'center', cursor: 'pointer',
            }}>
              <span style={{
                width: 12, height: 12, borderRadius: 999, background: '#fff',
                transform: t.enabled ? 'translateX(12px)' : 'translateX(0)',
                transition: 'transform 150ms',
              }}/>
            </span>
          </div>
        ))}
      </div>
    </>
  );
}

// ── Edit panel: Policies tab ──────────────────────────────────────
function EditPolicies({ agent }) {
  return (
    <>
      <div style={{
        padding: 14, background: 'var(--surface-inset)',
        border: '1px solid var(--border-light)', borderRadius: 10,
        display: 'flex', flexDirection: 'column', gap: 14,
      }}>
        <ToggleField
          label="Auto-apply when confidence ≥ threshold"
          description={`Skip the proposal step when the agent is sure. Currently ${agent.autoApply ? 'on' : 'off'}.`}
          on={agent.autoApply}/>
        <div style={{ borderTop: '1px solid var(--border-light)' }}/>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
          <TextField label="Confidence threshold" mono value="0.95" suffix="0–1"/>
          <TextField label="Amount ceiling" mono value="50000" prefix="£" helper="Always require human approval above this."/>
        </div>
      </div>

      <div>
        <FieldLabel>Inherited from organization</FieldLabel>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {[
            { t: 'Dual-control payouts', d: 'Two approvers required for outbound payouts' },
            { t: 'PII redaction',        d: 'Customer PII stripped from all prompts' },
            { t: 'Audit log retention',  d: 'Immutable log of every tool call · 7 years' },
          ].map((p, i) => (
            <div key={i} style={{
              display: 'grid', gridTemplateColumns: '14px 1fr auto', gap: 10, alignItems: 'center',
              padding: '10px 12px', background: 'var(--surface)',
              border: '1px solid var(--border-light)', borderRadius: 8,
            }}>
              <Icon name="lock" size={12} color="var(--text-secondary)"/>
              <div>
                <div style={{ fontSize: 12, fontWeight: 500, color: 'var(--text-primary)' }}>{p.t}</div>
                <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 1 }}>{p.d}</div>
              </div>
              <Pill tone="tint" size="sm">enforced</Pill>
            </div>
          ))}
        </div>
      </div>
    </>
  );
}

// ── Edit panel: Triggers tab ──────────────────────────────────────
function EditTriggers({ agent }) {
  const initialTriggers = [
    { kind: 'event',    label: 'New bank transaction received', enabled: true,  src: 'banking.transaction.received',  detail: 'Open Banking · all UK rails',  workflow: 'match_and_apply' },
    { kind: 'schedule', label: 'Hourly · top of the hour',      enabled: true,  src: 'cron 0 * * * *',                  detail: 'Every hour · UTC',              workflow: 'sweep_unmatched' },
    { kind: 'event',    label: 'Invoice marked overdue',        enabled: false, src: 'invoice.overdue',                 detail: 'Threshold ≥ 7 days',           workflow: 'dunning_cadence' },
    { kind: 'manual',   label: 'Run from My Space',             enabled: true,  src: 'human.invocation',                detail: 'Available to: Treasury · @maria', workflow: '—' },
    { kind: 'webhook',  label: 'External hook · Stripe',        enabled: true,  src: 'webhook.stripe.invoice.paid',    detail: 'Signed · last hit 2m ago',     workflow: 'match_and_apply' },
  ];
  const [triggers, setTriggers] = React.useState(initialTriggers);
  const [adding, setAdding] = React.useState(false);

  const iconFor = { webhook: 'globe', schedule: 'clock', manual: 'play', event: 'bolt' };
  const kindLabel = { webhook: 'Webhook', schedule: 'Schedule', manual: 'Manual', event: 'Event' };

  return (
    <>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <span style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{triggers.filter(t => t.enabled).length} of {triggers.length} active</span>
        <div style={{ flex: 1 }}/>
        <button className="btn btn-outline btn-sm" onClick={() => setAdding(true)} style={{ height: 26, padding: '0 10px', fontSize: 11 }}>
          <Icon name="plus" size={11}/> Add trigger
        </button>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        {triggers.map((t, i) => (
          <div key={i} style={{
            display: 'grid', gridTemplateColumns: '34px 1fr auto auto', gap: 12, alignItems: 'center',
            padding: '12px', background: 'var(--surface-inset)',
            border: '1px solid var(--border-light)', borderRadius: 8,
            opacity: t.enabled ? 1 : 0.55,
          }}>
            <div style={{
              width: 30, height: 30, borderRadius: 8,
              background: 'var(--brand-primary-10)', color: 'var(--brand-primary)',
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            }}><Icon name={iconFor[t.kind] || 'bolt'} size={14}/></div>
            <div style={{ minWidth: 0 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <span style={{ fontSize: 12.5, fontWeight: 500, color: 'var(--text-primary)' }}>{t.label}</span>
                <span style={{
                  fontSize: 9.5, padding: '1px 6px', borderRadius: 3,
                  background: 'var(--surface)', color: 'var(--text-tertiary)', fontWeight: 600,
                  letterSpacing: '0.06em', textTransform: 'uppercase',
                }}>{kindLabel[t.kind]}</span>
              </div>
              <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 2, fontFamily: 'var(--font-mono)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                {t.src}
              </div>
              {t.workflow && t.workflow !== '—' && (
                <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 2 }}>
                  → runs <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--brand-primary)' }}>{t.workflow}</span>
                </div>
              )}
            </div>
            <Pill tone={t.enabled ? 'success' : 'tint'} dot={t.enabled} size="sm">{t.enabled ? 'on' : 'off'}</Pill>
            <button className="hover-halo" style={{ padding: 6 }}>
              <Icon name="more" size={12} color="var(--text-tertiary)"/>
            </button>
          </div>
        ))}
      </div>

      {adding && <AddTriggerDialog agent={agent} onClose={() => setAdding(false)}
        onSave={(t) => { setTriggers([...triggers, t]); setAdding(false); }}/>}
    </>
  );
}

// ─── Add Trigger dialog ──────────────────────────────────────────────
// Modal that lets the user pick a trigger kind, then fills in kind-specific
// detail (event picker / cron builder / webhook config / manual policy)
// and finally pairs it with a workflow to run.
function AddTriggerDialog({ agent, onClose, onSave }) {
  const [kind, setKind] = React.useState('event');           // event | schedule | webhook | manual
  const [step, setStep] = React.useState('kind');             // kind | configure | workflow
  const [eventCat, setEventCat] = React.useState('Banking');
  const [eventId, setEventId] = React.useState('banking.transaction.received');
  const [cronPreset, setCronPreset] = React.useState('hourly');
  const [cronExpr, setCronExpr] = React.useState('0 * * * *');
  const [webhookName, setWebhookName] = React.useState('stripe-invoice-paid');
  const [manualGroup, setManualGroup] = React.useState('Treasury');
  const [workflow, setWorkflow] = React.useState('match_and_apply');
  const [filterExpr, setFilterExpr] = React.useState('amount > 0');

  // Event catalog — categorized for the picker
  const eventCatalog = {
    Banking: [
      { id: 'banking.transaction.received', label: 'Bank transaction received', desc: 'Any inbound or outbound txn on a connected rail' },
      { id: 'banking.statement.uploaded',   label: 'Statement uploaded',         desc: 'CSV or OFX statement file ingested' },
      { id: 'banking.account.balance.low',  label: 'Account balance low',        desc: 'Available balance dropped below floor' },
    ],
    Invoicing: [
      { id: 'invoice.created',  label: 'Invoice created',            desc: 'New AR or AP invoice was created' },
      { id: 'invoice.paid',     label: 'Invoice paid',               desc: 'Invoice marked as fully paid' },
      { id: 'invoice.overdue',  label: 'Invoice overdue',            desc: 'Past the due date by the policy threshold' },
      { id: 'invoice.matched',  label: 'Invoice matched to txn',     desc: 'A bank txn was successfully matched' },
    ],
    Customers: [
      { id: 'customer.created',     label: 'Customer created',     desc: 'New customer record added' },
      { id: 'customer.kyc.pending', label: 'KYC re-check pending', desc: 'Periodic re-verification triggered' },
      { id: 'customer.flagged',     label: 'Customer flagged',     desc: 'Risk or compliance flag raised' },
    ],
    Compliance: [
      { id: 'compliance.sanctions.hit', label: 'Sanctions match',       desc: 'Counterparty matched a sanctions list' },
      { id: 'compliance.audit.export',  label: 'Audit export requested', desc: 'External auditor requested a snapshot' },
    ],
    Workflows: [
      { id: 'workflow.completed', label: 'Workflow completed',  desc: 'A different workflow finished successfully' },
      { id: 'workflow.failed',    label: 'Workflow failed',     desc: 'A workflow exited with an error' },
    ],
  };

  // Cron presets — friendly labels
  const cronPresets = [
    { id: 'every_5min', label: 'Every 5 minutes',    expr: '*/5 * * * *' },
    { id: 'hourly',     label: 'Hourly',              expr: '0 * * * *' },
    { id: 'every_6h',   label: 'Every 6 hours',       expr: '0 */6 * * *' },
    { id: 'daily_09',   label: 'Daily · 9 AM UTC',    expr: '0 9 * * *' },
    { id: 'weekly_mon', label: 'Mondays · 9 AM',      expr: '0 9 * * 1' },
    { id: 'monthly_1',  label: 'First of month · 9 AM', expr: '0 9 1 * *' },
    { id: 'custom',     label: 'Custom · cron',       expr: '' },
  ];

  // Workflows that this agent can run
  const workflows = [
    { id: 'match_and_apply',  name: 'Match & apply',          desc: 'Reconcile invoice → txn, draft entry, surface for review',  steps: 4 },
    { id: 'sweep_unmatched',  name: 'Sweep unmatched',        desc: 'Re-attempt matching for invoices that fell through earlier', steps: 3 },
    { id: 'dunning_cadence',  name: 'Dunning cadence',        desc: 'Send overdue reminders on a per-customer rhythm',          steps: 5 },
    { id: 'forward_quote',    name: 'Forward quote',          desc: 'Quote a forward FX contract for cross-border invoices',    steps: 3 },
    { id: 'kyc_recheck',      name: 'KYC re-check',           desc: 'Re-screen counterparty against sanctions + PEP lists',     steps: 2 },
  ];

  const kindOpts = [
    { id: 'event',    icon: 'bolt',   label: 'Event',    desc: 'Fire when something happens in the system' },
    { id: 'schedule', icon: 'clock',  label: 'Schedule', desc: 'Fire on a recurring time-based cadence' },
    { id: 'webhook',  icon: 'globe',  label: 'Webhook',  desc: 'Fire when an external system POSTs to us' },
    { id: 'manual',   icon: 'play',   label: 'Manual',   desc: 'Run only when a human invokes it' },
  ];

  const presetActive = cronPresets.find(p => p.id === cronPreset) || cronPresets[0];
  const cronFinal = cronPreset === 'custom' ? cronExpr : presetActive.expr;

  // Build the trigger payload to save
  const buildTrigger = () => {
    const ev = Object.values(eventCatalog).flat().find(e => e.id === eventId);
    const wf = workflows.find(w => w.id === workflow);
    if (kind === 'event')    return { kind: 'event',    label: ev?.label || 'Event',     enabled: true, src: eventId,                   detail: filterExpr ? `where ${filterExpr}` : '—',       workflow: wf?.id };
    if (kind === 'schedule') return { kind: 'schedule', label: presetActive.label,        enabled: true, src: `cron ${cronFinal}`,        detail: 'UTC',                                          workflow: wf?.id };
    if (kind === 'webhook')  return { kind: 'webhook',  label: `External hook · ${webhookName}`, enabled: true, src: `webhook.${webhookName}`, detail: 'Signed (HMAC-SHA256)',                       workflow: wf?.id };
    return                          { kind: 'manual',   label: 'Run from My Space',       enabled: true, src: 'human.invocation',         detail: `Available to: ${manualGroup}`,                workflow: wf?.id };
  };

  return (
    <>
      <div onClick={onClose} style={{ position: 'fixed', inset: 0, background: 'rgba(20,25,30,0.4)', zIndex: 100 }}/>
      <div style={{
        position: 'fixed', top: '50%', left: '50%', transform: 'translate(-50%, -50%)',
        width: 720, maxHeight: '88vh',
        background: 'var(--surface)', borderRadius: 14,
        boxShadow: '0 24px 60px -8px rgba(0,0,0,0.32)',
        zIndex: 101, display: 'flex', flexDirection: 'column', overflow: 'hidden',
      }}>
        {/* Header */}
        <div style={{
          padding: '18px 22px 14px', borderBottom: '1px solid var(--border-light)',
          display: 'flex', alignItems: 'center', gap: 12,
        }}>
          <div style={{
            width: 36, height: 36, borderRadius: 10,
            background: 'var(--brand-primary-10)', color: 'var(--brand-primary)',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          }}><Icon name="bolt" size={16}/></div>
          <div style={{ flex: 1 }}>
            <div style={{ fontSize: 15, fontWeight: 600, color: 'var(--text-primary)' }}>Add trigger</div>
            <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>
              Define when <b>{agent.name}</b> should run, and which workflow to kick off.
            </div>
          </div>
          <button className="hover-halo" onClick={onClose} style={{ padding: 6 }}>
            <Icon name="close" size={14} color="var(--text-secondary)"/>
          </button>
        </div>

        {/* Stepper */}
        <div style={{ padding: '12px 22px', display: 'flex', alignItems: 'center', gap: 10, borderBottom: '1px solid var(--border-light)', background: 'var(--surface-inset)' }}>
          {[
            { id: 'kind',      label: 'Kind' },
            { id: 'configure', label: 'Configure' },
            { id: 'workflow',  label: 'Workflow' },
          ].map((s, i, arr) => {
            const order = ['kind','configure','workflow'];
            const stepIdx = order.indexOf(step);
            const myIdx   = order.indexOf(s.id);
            const reached = myIdx <= stepIdx;
            const active  = myIdx === stepIdx;
            return (
              <React.Fragment key={s.id}>
                <div onClick={() => myIdx <= stepIdx && setStep(s.id)} style={{
                  display: 'flex', alignItems: 'center', gap: 8, cursor: myIdx <= stepIdx ? 'pointer' : 'default',
                }}>
                  <span style={{
                    width: 22, height: 22, borderRadius: 999,
                    background: reached ? 'var(--brand-primary)' : 'var(--gray-300, #cbd1d8)',
                    color: '#fff', fontSize: 11, fontWeight: 600,
                    display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                    fontFamily: 'var(--font-mono)',
                  }}>{i + 1}</span>
                  <span style={{ fontSize: 12, fontWeight: active ? 600 : 500, color: active ? 'var(--text-primary)' : 'var(--text-secondary)' }}>{s.label}</span>
                </div>
                {i < arr.length - 1 && <div style={{ flex: 1, height: 1, background: 'var(--border-light)' }}/>}
              </React.Fragment>
            );
          })}
        </div>

        {/* Body */}
        <div style={{ flex: 1, overflow: 'auto', padding: 22 }}>
          {step === 'kind' && (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 10 }}>
              {kindOpts.map(k => {
                const active = k.id === kind;
                return (
                  <div key={k.id} onClick={() => setKind(k.id)} style={{
                    padding: 14, cursor: 'pointer',
                    background: active ? 'var(--brand-primary-10)' : 'var(--surface-inset)',
                    border: '1px solid ' + (active ? 'var(--brand-primary)' : 'var(--border-light)'),
                    borderRadius: 10,
                    display: 'flex', alignItems: 'flex-start', gap: 12,
                  }}>
                    <div style={{
                      width: 36, height: 36, borderRadius: 8, flex: 'none',
                      background: active ? 'var(--brand-primary)' : 'var(--surface)',
                      color: active ? '#fff' : 'var(--brand-primary)',
                      display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                    }}><Icon name={k.icon} size={16}/></div>
                    <div>
                      <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{k.label}</div>
                      <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 3, lineHeight: 1.5 }}>{k.desc}</div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}

          {step === 'configure' && kind === 'event' && (
            <EventConfigure
              catalog={eventCatalog} cat={eventCat} setCat={setEventCat}
              eventId={eventId} setEventId={setEventId}
              filterExpr={filterExpr} setFilterExpr={setFilterExpr}/>
          )}

          {step === 'configure' && kind === 'schedule' && (
            <ScheduleConfigure
              presets={cronPresets} preset={cronPreset} setPreset={setCronPreset}
              cronExpr={cronExpr} setCronExpr={setCronExpr}
              cronFinal={cronFinal}/>
          )}

          {step === 'configure' && kind === 'webhook' && (
            <WebhookConfigure name={webhookName} setName={setWebhookName}/>
          )}

          {step === 'configure' && kind === 'manual' && (
            <ManualConfigure group={manualGroup} setGroup={setManualGroup}/>
          )}

          {step === 'workflow' && (
            <WorkflowPicker workflows={workflows} sel={workflow} setSel={setWorkflow}/>
          )}
        </div>

        {/* Footer */}
        <div style={{
          padding: '14px 22px', borderTop: '1px solid var(--border-light)',
          background: 'var(--surface-inset)',
          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        }}>
          <div style={{ fontSize: 11, color: 'var(--text-tertiary)', display: 'flex', alignItems: 'center', gap: 6 }}>
            <Icon name="info" size={11}/> Triggers can be enabled or paused at any time after saving.
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-ghost btn-sm" onClick={onClose}>Cancel</button>
            {step !== 'kind' && (
              <button className="btn btn-outline btn-sm" onClick={() => setStep(step === 'workflow' ? 'configure' : 'kind')}>Back</button>
            )}
            {step !== 'workflow' ? (
              <button className="btn btn-primary btn-sm" onClick={() => setStep(step === 'kind' ? 'configure' : 'workflow')}>Continue</button>
            ) : (
              <button className="btn btn-primary btn-sm" onClick={() => onSave(buildTrigger())}>
                <Icon name="check" size={12}/> Add trigger
              </button>
            )}
          </div>
        </div>
      </div>
    </>
  );
}

function EventConfigure({ catalog, cat, setCat, eventId, setEventId, filterExpr, setFilterExpr }) {
  const evs = catalog[cat] || [];
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '160px 1fr', gap: 18 }}>
      {/* Categories */}
      <div>
        <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 8 }}>Category</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          {Object.keys(catalog).map(k => {
            const active = k === cat;
            return (
              <div key={k} onClick={() => setCat(k)} style={{
                padding: '7px 10px', borderRadius: 6, cursor: 'pointer',
                fontSize: 12, fontWeight: active ? 600 : 500,
                color: active ? 'var(--brand-primary)' : 'var(--text-secondary)',
                background: active ? 'var(--brand-primary-10)' : 'transparent',
                display: 'flex', alignItems: 'center', justifyContent: 'space-between',
              }}>
                <span>{k}</span>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)' }}>{catalog[k].length}</span>
              </div>
            );
          })}
        </div>
      </div>

      {/* Event list */}
      <div>
        <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 8 }}>Event</div>
        <div style={{
          background: 'var(--surface)', border: '1px solid var(--border-light)',
          borderRadius: 8, overflow: 'hidden',
        }}>
          {evs.map((e, i) => {
            const active = e.id === eventId;
            return (
              <div key={e.id} onClick={() => setEventId(e.id)} style={{
                padding: '12px 14px', cursor: 'pointer',
                background: active ? 'var(--brand-primary-10)' : 'transparent',
                borderLeft: '3px solid ' + (active ? 'var(--brand-primary)' : 'transparent'),
                borderBottom: i < evs.length - 1 ? '1px solid var(--border-light)' : 'none',
                display: 'grid', gridTemplateColumns: '14px 1fr', gap: 10, alignItems: 'flex-start',
              }}>
                <span style={{
                  width: 12, height: 12, borderRadius: 999,
                  border: '2px solid ' + (active ? 'var(--brand-primary)' : 'var(--gray-300, #cbd1d8)'),
                  background: active ? 'var(--brand-primary)' : 'transparent',
                  marginTop: 3,
                }}/>
                <div>
                  <div style={{ fontSize: 12.5, fontWeight: 500, color: 'var(--text-primary)' }}>{e.label}</div>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 2 }}>{e.id}</div>
                  <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 4, lineHeight: 1.5 }}>{e.desc}</div>
                </div>
              </div>
            );
          })}
        </div>

        {/* Filter expression */}
        <div style={{ marginTop: 14 }}>
          <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 8 }}>
            Filter <span style={{ color: 'var(--text-tertiary)', fontWeight: 500, textTransform: 'none' }}>· optional · only fire when this evaluates true</span>
          </div>
          <div style={{
            display: 'flex', alignItems: 'center', gap: 6,
            background: 'var(--surface)', border: '1px solid var(--border)',
            borderBottom: '2px solid var(--border)', borderRadius: 8,
            padding: '8px 10px',
          }}>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)' }}>where</span>
            <input value={filterExpr} onChange={e => setFilterExpr(e.target.value)}
              style={{ flex: 1, border: 'none', outline: 'none', background: 'transparent', fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-primary)' }}/>
          </div>
          <div style={{ display: 'flex', gap: 6, marginTop: 6, flexWrap: 'wrap' }}>
            {['amount > 1000', 'currency != "GBP"', 'counterparty.kyc == "verified"', 'memo contains "INV"'].map(s => (
              <span key={s} onClick={() => setFilterExpr(s)} style={{
                fontFamily: 'var(--font-mono)', fontSize: 10.5, padding: '2px 8px',
                border: '1px solid var(--border-light)', borderRadius: 999,
                color: 'var(--text-secondary)', cursor: 'pointer', background: 'var(--surface)',
              }}>+ {s}</span>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

function ScheduleConfigure({ presets, preset, setPreset, cronExpr, setCronExpr, cronFinal }) {
  // Decode the cron into human-readable
  const explain = (expr) => {
    if (!expr) return 'Enter a cron expression';
    if (expr === '*/5 * * * *') return 'Every 5 minutes';
    if (expr === '0 * * * *')   return 'At minute 0 of every hour';
    if (expr === '0 */6 * * *') return 'At minute 0, every 6 hours';
    if (expr === '0 9 * * *')   return 'Every day at 09:00 UTC';
    if (expr === '0 9 * * 1')   return 'Every Monday at 09:00 UTC';
    if (expr === '0 9 1 * *')   return 'On the 1st of every month at 09:00 UTC';
    return 'Custom schedule · ' + expr;
  };
  const next3 = (expr) => {
    // Faux next-run hints — would be real in prod
    if (expr === '*/5 * * * *') return ['12:05 UTC today', '12:10 UTC today', '12:15 UTC today'];
    if (expr === '0 * * * *')   return ['13:00 UTC today', '14:00 UTC today', '15:00 UTC today'];
    if (expr === '0 9 * * *')   return ['09:00 UTC tomorrow', '09:00 UTC Wed', '09:00 UTC Thu'];
    if (expr === '0 9 * * 1')   return ['09:00 UTC Mon · 5 May', '09:00 UTC Mon · 12 May', '09:00 UTC Mon · 19 May'];
    return ['—', '—', '—'];
  };

  return (
    <div>
      <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 8 }}>Cadence</div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 8 }}>
        {presets.map(p => {
          const active = p.id === preset;
          return (
            <div key={p.id} onClick={() => setPreset(p.id)} style={{
              padding: '12px', cursor: 'pointer',
              background: active ? 'var(--brand-primary-10)' : 'var(--surface-inset)',
              border: '1px solid ' + (active ? 'var(--brand-primary)' : 'var(--border-light)'),
              borderRadius: 8,
            }}>
              <div style={{ fontSize: 12.5, fontWeight: active ? 600 : 500, color: 'var(--text-primary)' }}>{p.label}</div>
              {p.expr && <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 2 }}>{p.expr}</div>}
            </div>
          );
        })}
      </div>

      {/* Custom cron */}
      {preset === 'custom' && (
        <div style={{ marginTop: 14 }}>
          <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 8 }}>Cron expression</div>
          <div style={{
            display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 6, marginBottom: 8,
            fontSize: 9.5, color: 'var(--text-tertiary)', textAlign: 'center', letterSpacing: '0.04em', textTransform: 'uppercase',
          }}>
            <span>min</span><span>hour</span><span>dom</span><span>month</span><span>dow</span>
          </div>
          <input value={cronExpr} onChange={e => setCronExpr(e.target.value)}
            placeholder="0 * * * *"
            style={{
              width: '100%', boxSizing: 'border-box',
              fontFamily: 'var(--font-mono)', fontSize: 14, letterSpacing: '0.06em', textAlign: 'center',
              padding: '10px 12px',
              background: 'var(--surface)', border: '1px solid var(--border)',
              borderBottom: '2px solid var(--border)', borderRadius: 8,
            }}/>
        </div>
      )}

      {/* Live preview */}
      <div style={{
        marginTop: 16, padding: 14,
        background: 'var(--brand-primary-10)', borderRadius: 10,
        display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14,
      }}>
        <div>
          <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--brand-primary)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 6 }}>In plain English</div>
          <div style={{ fontSize: 13, color: 'var(--text-primary)', fontWeight: 500 }}>{explain(cronFinal)}</div>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)', marginTop: 4 }}>{cronFinal || '—'}</div>
        </div>
        <div>
          <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--brand-primary)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 6 }}>Next 3 runs</div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            {next3(cronFinal).map((s, i) => (
              <div key={i} style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)' }}>{s}</div>
            ))}
          </div>
        </div>
      </div>

      {/* Timezone */}
      <div style={{ marginTop: 14, display: 'flex', alignItems: 'center', gap: 12 }}>
        <span style={{ fontSize: 11.5, color: 'var(--text-secondary)' }}>Timezone</span>
        <select style={{
          fontSize: 12, padding: '6px 10px',
          background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 6,
        }}>
          <option>UTC</option><option>Europe/London</option><option>Europe/Berlin</option><option>America/New_York</option><option>Asia/Singapore</option>
        </select>
        <div style={{ flex: 1 }}/>
        <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>Skip if previous run still in flight</span>
        <Toggle on/>
      </div>
    </div>
  );
}

function WebhookConfigure({ name, setName }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
      <div>
        <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 8 }}>Webhook name</div>
        <input value={name} onChange={e => setName(e.target.value)}
          style={{
            width: '100%', boxSizing: 'border-box',
            fontFamily: 'var(--font-mono)', fontSize: 13,
            padding: '9px 12px',
            background: 'var(--surface)', border: '1px solid var(--border)',
            borderBottom: '2px solid var(--border)', borderRadius: 8,
          }}/>
      </div>
      <div style={{ background: 'var(--surface-inset)', padding: 12, borderRadius: 8 }}>
        <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 6 }}>Endpoint URL</div>
        <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--brand-primary)', wordBreak: 'break-all' }}>
          https://hooks.aonik.io/v1/triggers/{name || '<name>'}
        </div>
        <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 6 }}>
          Configure your external system to POST signed JSON to this URL. We'll verify the HMAC signature before invoking the workflow.
        </div>
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <div>
          <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 6 }}>Auth</div>
          <select style={{ width: '100%', fontSize: 12, padding: '8px 10px', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 6 }}>
            <option>HMAC-SHA256 (recommended)</option><option>Bearer token</option><option>None (testing only)</option>
          </select>
        </div>
        <div>
          <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 6 }}>Allowed origins</div>
          <input placeholder="*.stripe.com, hooks.example.io"
            style={{ width: '100%', boxSizing: 'border-box', fontFamily: 'var(--font-mono)', fontSize: 12, padding: '8px 10px', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 6 }}/>
        </div>
      </div>
    </div>
  );
}

function ManualConfigure({ group, setGroup }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
      <div style={{ padding: 14, background: 'var(--brand-primary-10)', borderRadius: 10, display: 'flex', gap: 10 }}>
        <Icon name="play" size={14} color="var(--brand-primary)"/>
        <div style={{ fontSize: 12, color: 'var(--text-secondary)', lineHeight: 1.55 }}>
          Manual triggers add a button to <b>My Space</b> for the selected group. The agent will not run unless someone clicks it.
        </div>
      </div>
      <div>
        <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 8 }}>Available to</div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 8 }}>
          {['Treasury', 'Finance', 'Compliance', 'Anyone (organization-wide)'].map(g => {
            const active = g === group;
            return (
              <div key={g} onClick={() => setGroup(g)} style={{
                padding: '10px 12px', cursor: 'pointer',
                background: active ? 'var(--brand-primary-10)' : 'var(--surface-inset)',
                border: '1px solid ' + (active ? 'var(--brand-primary)' : 'var(--border-light)'),
                borderRadius: 8, display: 'flex', alignItems: 'center', gap: 10,
              }}>
                <span style={{
                  width: 14, height: 14, borderRadius: 999,
                  border: '2px solid ' + (active ? 'var(--brand-primary)' : 'var(--gray-300, #cbd1d8)'),
                  background: active ? 'var(--brand-primary)' : 'transparent',
                }}/>
                <span style={{ fontSize: 12.5, color: 'var(--text-primary)' }}>{g}</span>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}

function WorkflowPicker({ workflows, sel, setSel }) {
  return (
    <div>
      <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 8 }}>
        Workflow to run · {workflows.length} available
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        {workflows.map(w => {
          const active = w.id === sel;
          return (
            <div key={w.id} onClick={() => setSel(w.id)} style={{
              padding: '12px 14px', cursor: 'pointer',
              background: active ? 'var(--brand-primary-10)' : 'var(--surface-inset)',
              border: '1px solid ' + (active ? 'var(--brand-primary)' : 'var(--border-light)'),
              borderRadius: 8, display: 'grid', gridTemplateColumns: '14px 1fr auto', gap: 12, alignItems: 'center',
            }}>
              <span style={{
                width: 12, height: 12, borderRadius: 999,
                border: '2px solid ' + (active ? 'var(--brand-primary)' : 'var(--gray-300, #cbd1d8)'),
                background: active ? 'var(--brand-primary)' : 'transparent',
              }}/>
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{w.name}</span>
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)' }}>{w.id}</span>
                </div>
                <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2, lineHeight: 1.5 }}>{w.desc}</div>
              </div>
              <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{w.steps} steps</span>
            </div>
          );
        })}
      </div>
      <button className="btn btn-ghost btn-sm" style={{ marginTop: 8, fontSize: 11 }}>
        <Icon name="plus" size={11}/> Create new workflow…
      </button>
    </div>
  );
}

// Tiny shared toggle for the dialog
function Toggle({ on }) {
  return (
    <span style={{
      width: 28, height: 16, borderRadius: 999, padding: 2,
      background: on ? 'var(--brand-primary)' : 'var(--gray-300, #cbd1d8)',
      display: 'inline-flex', alignItems: 'center', flex: 'none',
    }}>
      <span style={{ width: 12, height: 12, borderRadius: 999, background: '#fff', transform: on ? 'translateX(12px)' : 'translateX(0)' }}/>
    </span>
  );
}

// ─── Page screen ───────────────────────────────────────────────────
function ScreenAgentsPage() {
  const [layout, setLayout] = React.useState('card');     // card | grid
  const [filter, setFilter] = React.useState('All');
  const [editing, setEditing] = React.useState(null);

  const tabs = ['All', 'System', 'Domain', 'Running', 'Paused'];
  const counts = {
    All: AGENT_LIST.length,
    System: AGENT_LIST.filter(a => a.kind === 'System').length,
    Domain: AGENT_LIST.filter(a => a.kind === 'Domain').length,
    Running: AGENT_LIST.filter(a => a.state === 'running').length,
    Paused: AGENT_LIST.filter(a => a.state === 'paused').length,
  };

  const visible = AGENT_LIST.filter(a => {
    if (filter === 'All') return true;
    if (filter === 'System' || filter === 'Domain') return a.kind === filter;
    if (filter === 'Running') return a.state === 'running';
    if (filter === 'Paused') return a.state === 'paused';
    return true;
  });

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <div style={{ height: '100%', overflow: 'auto', padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        <PageHeader
          eyebrow="AI · Agents"
          title="Agents"
          subtitle={`${AGENT_LIST.length} built-in agents · ${counts.Running} running now · 0.93 avg confidence`}
          actions={<>
            <button className="btn btn-outline btn-sm"><Icon name="refresh" size={12}/> Re-sync tools</button>
            <button className="btn btn-outline btn-sm"><Icon name="filter" size={12}/> Filter</button>
            <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> New agent</button>
          </>}/>

        {/* Tabs + layout switch */}
        <div style={{
          display: 'flex', alignItems: 'center', gap: 14,
          paddingBottom: 12, borderBottom: '1px solid var(--border-light)',
        }}>
          <Tabs tabs={tabs} active={filter} onChange={setFilter} counts={counts}/>
          <div style={{ flex: 1 }}/>
          <div style={{
            display: 'inline-flex', borderRadius: 6, border: '1px solid var(--border-light)',
            overflow: 'hidden', height: 28,
          }}>
            {[['card','grid','Cards'], ['grid','list','List']].map(([k, ic, lab]) => {
              const active = layout === k;
              return (
                <button key={k} onClick={() => setLayout(k)}
                  style={{
                    display: 'inline-flex', alignItems: 'center', gap: 5, padding: '0 10px',
                    fontSize: 11.5, fontWeight: active ? 600 : 500, cursor: 'pointer', border: 'none',
                    background: active ? 'var(--brand-primary)' : 'transparent',
                    color: active ? '#fff' : 'var(--text-secondary)',
                  }}>
                  <Icon name={ic} size={11}/> {lab}
                </button>
              );
            })}
          </div>
        </div>

        {/* List */}
        {layout === 'card' ? (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(360px, 1fr))', gap: 14 }}>
            {visible.map(a => <AgentCard key={a.id} agent={a} onEdit={() => setEditing(a)}/>)}
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <div style={{
              display: 'grid', gridTemplateColumns: '44px 1fr 130px 80px 80px 90px 28px',
              gap: 14, padding: '0 14px', fontSize: 10, color: 'var(--text-tertiary)',
              letterSpacing: '0.06em', textTransform: 'uppercase', fontWeight: 600,
            }}>
              <span/>
              <span>Agent</span>
              <span>Model</span>
              <span style={{ textAlign: 'right' }}>Runs · 7d</span>
              <span style={{ textAlign: 'right' }}>Conf.</span>
              <span>State</span>
              <span/>
            </div>
            {visible.map(a => <AgentGridRow key={a.id} agent={a} onEdit={() => setEditing(a)}/>)}
          </div>
        )}
      </div>

      {/* Slide-out edit panel */}
      {editing && <AgentEditPanel agent={editing} onClose={() => setEditing(null)}/>}
    </div>
  );
}

Object.assign(window, { ScreenAgentsPage, AgentPortrait, AgentEditPanel, AGENT_LIST, STATE_DOT });
