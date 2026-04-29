// Agent detail page — rich profile of a single built-in agent.
// Animated portrait hero, live status, KPI strip, sub-agents network,
// tools list, skills, MCP servers, recent runs, and slide-out edit panel.

function ScreenAgentDetail() {
  // Show the Billing Agent — most interesting one (running, multi-tool, sub-agents, MCP)
  const agent = AGENT_LIST.find(a => a.id === 'billing');
  const [editing, setEditing] = React.useState(false);
  const [tab, setTab] = React.useState('Overview');

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      {/* injected keyframes for the hero */}
      <style>{ANIM_CSS}</style>

      <div style={{ height: '100%', overflow: 'auto' }}>
        {/* ─── Hero ─────────────────────────────────────────────── */}
        <AgentDetailHero agent={agent} onEdit={() => setEditing(true)}/>

        {/* ─── Sticky tabs ──────────────────────────────────────── */}
        <div style={{
          position: 'sticky', top: 0, zIndex: 3,
          background: 'var(--surface-canvas, var(--surface))',
          borderBottom: '1px solid var(--border-light)',
          padding: '0 32px',
          display: 'flex', alignItems: 'center', gap: 4,
        }}>
          {['Overview', 'Sub-agents', 'Tools', 'Skills', 'MCP Servers', 'Activity', 'Settings'].map(t => (
            <div key={t} onClick={() => setTab(t)} style={{
              padding: '14px 14px 13px', fontSize: 13,
              fontWeight: t === tab ? 600 : 500,
              color: t === tab ? 'var(--text-primary)' : 'var(--text-secondary)',
              borderBottom: t === tab ? '2px solid ' + agent.color : '2px solid transparent',
              cursor: 'pointer',
            }}>
              {t}
            </div>
          ))}
          <div style={{ flex: 1 }}/>
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>
            agent_id: agt_billing_v3
          </span>
        </div>

        {/* ─── Body ───────────────────────────────────────────────── */}
        <div style={{ padding: '24px 32px 40px', maxWidth: 1600, margin: '0 auto' }}>
          {tab === 'Overview' && (
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 360px', gap: 24 }}>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 20, minWidth: 0 }}>
                <KPIStrip agent={agent}/>
                <SubAgentsSection agent={agent}/>
                <ToolsSection agent={agent}/>
                <SkillsSection agent={agent}/>
                <McpSection agent={agent}/>
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
                <ConnectionMap agent={agent}/>
                <RecentRunsCard agent={agent}/>
                <PolicyCard agent={agent}/>
              </div>
            </div>
          )}
          {tab === 'Sub-agents'   && <TabSubAgents agent={agent}/>}
          {tab === 'Tools'        && <TabTools agent={agent}/>}
          {tab === 'Skills'       && <TabSkills agent={agent}/>}
          {tab === 'MCP Servers'  && <TabMcp agent={agent}/>}
          {tab === 'Activity'     && <TabActivity agent={agent}/>}
          {tab === 'Settings'     && <TabSettings agent={agent}/>}
        </div>
      </div>

      {/* Slide-out edit */}
      {editing && (
        <>
          <div onClick={() => setEditing(false)} style={{
            position: 'absolute', inset: 0, background: 'rgba(20,25,30,0.18)', zIndex: 20,
          }}/>
          <div style={{ position: 'absolute', top: 0, right: 0, bottom: 0, zIndex: 21 }}>
            <AgentEditPanel agent={agent} onClose={() => setEditing(false)}/>
          </div>
        </>
      )}
    </div>
  );
}

// ─── Hero ────────────────────────────────────────────────────────────
function AgentDetailHero({ agent, onEdit }) {
  const c = agent.color;
  return (
    <div style={{
      position: 'relative',
      padding: '36px 32px 32px',
      background: `linear-gradient(135deg, ${c}1a 0%, ${c}08 60%, transparent 100%)`,
      borderBottom: '1px solid var(--border-light)',
      overflow: 'hidden',
    }}>
      {/* Floating orbs background */}
      <HeroOrbs color={c}/>

      <div style={{ position: 'relative', display: 'flex', alignItems: 'flex-start', gap: 28, maxWidth: 1600, margin: '0 auto' }}>
        {/* Animated portrait */}
        <div style={{ position: 'relative', flex: 'none', width: 144, height: 144 }}>
          <div style={{
            position: 'absolute', inset: -8,
            border: `1.5px dashed ${c}66`, borderRadius: '50%',
            animation: 'agt-spin-slow 28s linear infinite',
          }}/>
          <div style={{
            position: 'absolute', inset: -18,
            border: `1px solid ${c}33`, borderRadius: '50%',
            animation: 'agt-spin-rev 42s linear infinite',
          }}/>
          {/* orbital dots on the outer ring */}
          <div style={{ position: 'absolute', inset: -18, animation: 'agt-spin-rev 42s linear infinite' }}>
            <span style={{ position: 'absolute', top: -3, left: '50%', width: 6, height: 6, borderRadius: 999, background: c, boxShadow: `0 0 10px ${c}` }}/>
          </div>
          <div style={{ position: 'absolute', inset: -8, animation: 'agt-spin-slow 28s linear infinite' }}>
            <span style={{ position: 'absolute', bottom: 4, left: '50%', width: 4, height: 4, borderRadius: 999, background: c, opacity: 0.8 }}/>
          </div>
          <div style={{
            position: 'relative', width: 144, height: 144,
            animation: 'agt-float 6s ease-in-out infinite',
            filter: `drop-shadow(0 12px 24px ${c}33)`,
          }}>
            <AgentPortrait agent={agent} size={144}/>
            {/* state pulse */}
            {agent.state === 'running' && (
              <span style={{
                position: 'absolute', bottom: 6, right: 6,
                width: 14, height: 14, borderRadius: 999,
                background: 'var(--success)',
                boxShadow: '0 0 0 3px var(--surface)',
              }}>
                <span style={{
                  position: 'absolute', inset: 0, borderRadius: 999,
                  background: 'var(--success)', opacity: 0.4,
                  animation: 'agt-pulse 1.6s ease-out infinite',
                }}/>
              </span>
            )}
          </div>
        </div>

        {/* Identity */}
        <div style={{ flex: 1, minWidth: 0, paddingTop: 6 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 10 }}>
            <span style={{
              fontSize: 10.5, letterSpacing: '0.12em', textTransform: 'uppercase', fontWeight: 600,
              color: c,
              padding: '3px 9px', borderRadius: 4,
              background: `${c}1a`,
            }}>{agent.kind} Agent</span>
            <Pill tone={agent.state === 'running' ? 'success' : agent.state === 'paused' ? 'warning' : 'muted'} dot size="sm">
              {STATE_DOT[agent.state].t}
            </Pill>
            <span style={{ fontSize: 11.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>v0.42.1</span>
            <span style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>· deployed 12d ago</span>
          </div>

          <h1 style={{
            fontFamily: 'var(--font-brand)', fontSize: 38, lineHeight: 1.05,
            letterSpacing: '-0.02em', color: 'var(--text-primary)',
            margin: 0, marginBottom: 8,
          }}>
            {agent.name}
          </h1>

          <p style={{ fontSize: 15, color: 'var(--text-secondary)', maxWidth: 720, lineHeight: 1.55, margin: 0, marginBottom: 18 }}>
            {agent.description}
          </p>

          {/* CTA row */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <button className="btn btn-primary" onClick={onEdit}>
              <Icon name="edit" size={13}/> Edit agent
            </button>
            <button className="btn btn-outline">
              <Icon name="play" size={13}/> Run in Playground
            </button>
            <button className="btn btn-ghost">
              <Icon name="terminal" size={13}/> View traces
            </button>
            <div style={{ flex: 1 }}/>
            <button className="btn btn-ghost btn-sm" title="Pause"><Icon name="pause" size={13}/></button>
            <button className="btn btn-ghost btn-sm" title="More"><Icon name="more" size={13}/></button>
          </div>
        </div>

        {/* Right-side identity stat block */}
        <div style={{
          flex: 'none', width: 220,
          background: 'var(--surface)', border: '1px solid var(--border-light)',
          borderRadius: 12, padding: 16,
          boxShadow: '0 4px 16px -8px rgba(20,25,30,0.08)',
        }}>
          <div style={{ fontSize: 10, letterSpacing: '0.08em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)', marginBottom: 10 }}>
            Configuration
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            <ConfRow k="Model"        v={agent.model} mono/>
            <ConfRow k="Temperature"  v={agent.temp.toFixed(1)} mono/>
            <ConfRow k="Owner"        v="Treasury team"/>
            <ConfRow k="Auto-apply"   v={agent.autoApply ? 'Enabled' : 'Off'} accent={agent.autoApply ? 'var(--success)' : null}/>
            <ConfRow k="Region"       v="eu-west-2" mono/>
          </div>
        </div>
      </div>
    </div>
  );
}

function HeroOrbs({ color }) {
  return (
    <>
      <span style={{
        position: 'absolute', top: -80, right: '18%',
        width: 220, height: 220, borderRadius: '50%',
        background: `radial-gradient(circle, ${color}1f 0%, transparent 65%)`,
        animation: 'agt-drift 18s ease-in-out infinite',
      }}/>
      <span style={{
        position: 'absolute', bottom: -60, left: '8%',
        width: 180, height: 180, borderRadius: '50%',
        background: `radial-gradient(circle, ${color}14 0%, transparent 65%)`,
        animation: 'agt-drift 22s ease-in-out infinite reverse',
      }}/>
    </>
  );
}

function ConfRow({ k, v, mono, accent }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10 }}>
      <span style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>{k}</span>
      <span style={{
        fontFamily: mono ? 'var(--font-mono)' : 'inherit',
        fontSize: 12, fontWeight: 600,
        color: accent || 'var(--text-primary)',
      }}>{v}</span>
    </div>
  );
}

// ─── KPI strip ──────────────────────────────────────────────────────
function KPIStrip({ agent }) {
  const sparkline = (vals, w = 100, h = 28) => {
    const max = Math.max(...vals), min = Math.min(...vals);
    const range = max - min || 1;
    return vals.map((v, i) => `${(i / (vals.length - 1)) * w},${h - ((v - min) / range) * h}`).join(' ');
  };
  const c = agent.color;
  const kpis = [
    { l: 'Runs (24h)',    v: agent.runs.toLocaleString(), d: '+12%',  spark: [40,42,38,46,52,58,55,60,68,72,78,82], pos: true },
    { l: 'Avg confidence', v: (agent.conf * 100).toFixed(1) + '%', d: '+0.4%', spark: [88,89,90,89,92,93,92,94,93,94,94,94], pos: true },
    { l: 'Tool calls',    v: '4.8k',  d: '+18%',     spark: [60,68,72,80,88,92,98,108,118,124,132,142], pos: true },
    { l: 'Avg latency',   v: '892ms', d: '-42ms',    spark: [120,118,114,112,108,106,102,100,98,95,92,89], pos: true },
  ];
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
      {kpis.map(k => (
        <div key={k.l} style={{
          background: 'var(--surface)', border: '1px solid var(--border-light)',
          borderRadius: 12, padding: 14,
        }}>
          <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', fontWeight: 600 }}>
            {k.l}
          </div>
          <div style={{ display: 'flex', alignItems: 'flex-end', gap: 8, marginTop: 6 }}>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 22, fontWeight: 600, lineHeight: 1, color: 'var(--text-primary)' }}>{k.v}</div>
            <div style={{ flex: 1 }}/>
            <svg width={70} height={22}>
              <polyline points={sparkline(k.spark, 70, 22)} fill="none" stroke={c} strokeWidth={1.5}/>
            </svg>
          </div>
          <div style={{ marginTop: 4, fontSize: 11, color: k.pos ? 'var(--success)' : '#c44536', fontFamily: 'var(--font-mono)' }}>
            {k.d}
          </div>
        </div>
      ))}
    </div>
  );
}

// ─── Sub-agents ─────────────────────────────────────────────────────
function SubAgentsSection({ agent }) {
  // Sub-agents this one delegates to. Pull names from AGENT_LIST so they look real.
  const subs = [
    { ref: 'ledger',  role: 'Posts journal entries from matched txns',     calls: 142, avgMs: 218, autonomy: 'auto' },
    { ref: 'fx',      role: 'Quotes rates for cross-currency invoices',    calls:  38, avgMs: 412, autonomy: 'propose' },
    { ref: 'compl',   role: 'KYC re-checks before any new counterparty',   calls:  12, avgMs: 894, autonomy: 'block' },
    { ref: 'dunn',    role: 'Drafts overdue reminders when match fails',   calls:  28, avgMs: 142, autonomy: 'propose' },
  ];
  const autoTone = { auto: { bg: 'var(--success-tint, #1f7a5e1a)', fg: 'var(--success, #1f7a5e)', t: 'Auto-apply' },
                     propose: { bg: '#b4741e1a', fg: '#b4741e', t: 'Propose' },
                     block: { bg: '#7b76b61a', fg: '#7b76b6', t: 'Required' } };

  return (
    <Section
      title="Connected sub-agents"
      subtitle="Other agents this one delegates work to. Calls require the destination's policies to allow the operation."
      count={subs.length}
      action={<button className="btn btn-outline btn-sm"><Icon name="plus" size={11}/> Connect agent</button>}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 10 }}>
        {subs.map(s => {
          const sub = AGENT_LIST.find(a => a.id === s.ref);
          const tone = autoTone[s.autonomy];
          return (
            <div key={s.ref} className="hover-lift" style={{
              display: 'flex', gap: 12, padding: 12,
              background: 'var(--surface)', border: '1px solid var(--border-light)',
              borderRadius: 10, cursor: 'pointer',
              transition: 'border-color 150ms, transform 150ms',
            }}>
              <AgentPortrait agent={sub} size={42} ring={false}/>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 2 }}>
                  <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{sub.name}</span>
                  <span style={{ fontSize: 9.5, padding: '1px 6px', borderRadius: 3, background: tone.bg, color: tone.fg, fontWeight: 600, letterSpacing: '0.04em', textTransform: 'uppercase' }}>{tone.t}</span>
                </div>
                <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5, marginBottom: 6 }}>
                  {s.role}
                </div>
                <div style={{ display: 'flex', gap: 14, fontSize: 10.5, fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>
                  <span>{s.calls} calls / 24h</span>
                  <span>~{s.avgMs}ms</span>
                </div>
              </div>
              <Icon name="chevron" size={11} color="var(--text-tertiary)" style={{ alignSelf: 'center' }}/>
            </div>
          );
        })}
      </div>
    </Section>
  );
}

// ─── Tools ───────────────────────────────────────────────────────────
function ToolsSection({ agent }) {
  const tools = [
    { name: 'search_invoices',         cat: 'read',    desc: 'Query the invoice store by counterparty, status or amount.', uses: 1842, p99: '142ms' },
    { name: 'list_bank_transactions',  cat: 'read',    desc: 'Read bank txns inside a date window from connected rails.',  uses: 1318, p99: '318ms' },
    { name: 'match_invoice_to_txn',    cat: 'compute', desc: 'Score a candidate match (0–1) using ledger + memo signals.', uses: 1284, p99: '211ms' },
    { name: 'draft_journal_entry',     cat: 'write',   desc: 'Compose a balanced debit/credit pair (proposal only).',      uses:  892, p99: '88ms'  },
    { name: 'apply_journal_entry',     cat: 'write',   desc: 'Post a drafted entry to the ledger after approval.',         uses:  416, p99: '142ms' },
    { name: 'send_dunning_email',      cat: 'write',   desc: 'Compose and dispatch an overdue reminder.',                  uses:   28, p99: '512ms' },
    { name: 'display_proposal_card',   cat: 'display', desc: 'Render an Apply / Review / Dismiss tool card in chat.',      uses:  892, p99: '12ms'  },
    { name: 'confirm_action',          cat: 'display', desc: 'Halt and ask the human for explicit approval.',              uses:  142, p99: '8ms'   },
  ];
  const catColor = {
    read:    { bg: 'var(--brand-primary-10)', fg: 'var(--brand-primary)' },
    write:   { bg: '#eb5c371a',               fg: '#eb5c37' },
    compute: { bg: '#3ab7951a',               fg: '#3ab795' },
    display: { bg: '#7b76b61a',               fg: '#7b76b6' },
  };

  return (
    <Section
      title="Tools"
      subtitle="Functions the agent can invoke. Read tools fetch data; write tools mutate state and require policy clearance; display tools render UI in chat."
      count={tools.length}
      action={<>
        <button className="btn btn-ghost btn-sm">View schema</button>
        <button className="btn btn-outline btn-sm"><Icon name="plus" size={11}/> Add tool</button>
      </>}>
      <div style={{
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 10, overflow: 'hidden',
      }}>
        <div style={{
          display: 'grid', gridTemplateColumns: '90px 1fr 80px 80px 24px',
          gap: 14, padding: '8px 14px',
          background: 'var(--surface-inset)',
          fontSize: 10, fontWeight: 600, letterSpacing: '0.06em', textTransform: 'uppercase',
          color: 'var(--text-tertiary)', borderBottom: '1px solid var(--border-light)',
        }}>
          <div>Kind</div><div>Tool</div>
          <div style={{ textAlign: 'right' }}>Uses · 24h</div>
          <div style={{ textAlign: 'right' }}>p99</div>
          <div/>
        </div>
        {tools.map((t, i) => {
          const tone = catColor[t.cat];
          return (
            <div key={t.name} style={{
              display: 'grid', gridTemplateColumns: '90px 1fr 80px 80px 24px',
              gap: 14, padding: '12px 14px', alignItems: 'center',
              borderBottom: i < tools.length - 1 ? '1px solid var(--border-light)' : 'none',
            }}>
              <span style={{
                fontSize: 9.5, padding: '2px 7px', borderRadius: 3,
                background: tone.bg, color: tone.fg, fontWeight: 600,
                letterSpacing: '0.06em', textTransform: 'uppercase', textAlign: 'center',
                fontFamily: 'var(--font-mono)', justifySelf: 'start',
              }}>{t.cat}</span>
              <div>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{t.name}</div>
                <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{t.desc}</div>
              </div>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-secondary)', textAlign: 'right' }}>{t.uses.toLocaleString()}</div>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-secondary)', textAlign: 'right' }}>{t.p99}</div>
              <Icon name="more" size={12} color="var(--text-tertiary)"/>
            </div>
          );
        })}
      </div>
    </Section>
  );
}

// ─── Skills (Overview tab summary) ───────────────────────────────────
// Aligned to the Agent Skills open spec: each skill is a folder with a
// SKILL.md (frontmatter: name + description), loaded by progressive
// disclosure. The Skills tab has the full registry, file tree, and md viewer.
function SkillsSection({ agent }) {
  const skills = [
    { name: 'invoice-reconciliation', desc: 'Match incoming bank txns to open invoices and draft journal entries when confidence is high.', version: '1.4.2', source: 'org', last24h: 412, status: 'active' },
    { name: 'bank-statement-intake',  desc: 'Parse uploaded CSV/OFX bank statements and post lines as draft staging-ledger transactions.',   version: '2.0.1', source: 'org', last24h: 184, status: 'active' },
    { name: 'ar-aging-summary',       desc: 'Produce an aging summary across the AR ledger with sub-totals by tier and a chase-list.',       version: '1.0.0', source: 'community', last24h: 88, status: 'active' },
    { name: 'dunning-cadence',        desc: 'Choose a dunning template + channel for an overdue invoice using tier and prior-contact data.', version: '1.2.0', source: 'org', last24h: 42, status: 'active' },
    { name: 'currency-rounding-fix',  desc: 'Detect and reverse off-by-cent rounding errors when invoice + settlement currencies differ.',   version: '0.3.0', source: 'private', last24h: 6, status: 'beta' },
  ];
  const sourceTone = { org: '#055a60', community: '#7b76b6', private: '#b4741e' };

  return (
    <Section
      title="Skills"
      subtitle="Folders with a SKILL.md. The agent reads name + description on every turn, and only loads the full instructions when a task matches."
      count={skills.length}
      action={<>
        <button className="btn btn-ghost btn-sm"><Icon name="search" size={11}/> Browse registry</button>
        <button className="btn btn-outline btn-sm"><Icon name="upload" size={11}/> Install skill</button>
      </>}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {skills.map(s => (
          <div key={s.name} className="hover-lift" style={{
            padding: 12, cursor: 'pointer',
            background: 'var(--surface)', border: '1px solid var(--border-light)',
            borderRadius: 10,
            display: 'grid', gridTemplateColumns: '30px 1fr auto', gap: 12, alignItems: 'flex-start',
          }}>
            <div style={{
              width: 30, height: 30, borderRadius: 7,
              background: `${agent.color}14`, color: agent.color,
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            }}><Icon name="folderOpen" size={14}/></div>
            <div style={{ minWidth: 0 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{s.name}</span>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>v{s.version}</span>
                {s.status === 'beta' && <span style={{ fontSize: 9.5, padding: '1px 5px', borderRadius: 3, background: '#b4741e1a', color: '#b4741e', fontWeight: 600, letterSpacing: '0.04em' }}>BETA</span>}
                <span style={{ fontSize: 9.5, padding: '1px 6px', borderRadius: 3, background: sourceTone[s.source] + '14', color: sourceTone[s.source], fontWeight: 600, letterSpacing: '0.04em', textTransform: 'uppercase' }}>{s.source}</span>
              </div>
              <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 3, lineHeight: 1.5 }}>{s.desc}</div>
            </div>
            <div style={{ textAlign: 'right', whiteSpace: 'nowrap' }}>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-primary)' }}>{s.last24h}</div>
              <div style={{ fontSize: 10, color: 'var(--text-tertiary)', marginTop: 1 }}>activations · 24h</div>
            </div>
          </div>
        ))}
      </div>
    </Section>
  );
}

// ─── MCP Servers ─────────────────────────────────────────────────────
function McpSection({ agent }) {
  const servers = [
    { name: 'aonik-ledger',     url: 'mcp://internal/aonik-ledger',      status: 'connected',  tools: 12, latency: '14ms', auth: 'mTLS', native: true },
    { name: 'open-banking-uk',  url: 'mcp://partner/open-banking-uk',    status: 'connected',  tools:  8, latency: '188ms', auth: 'OAuth2' },
    { name: 'companies-house',  url: 'mcp://partner/companies-house',    status: 'connected',  tools:  4, latency: '412ms', auth: 'API key' },
    { name: 'fx-quotes',        url: 'mcp://partner/fx-quotes-v2',       status: 'connecting', tools:  6, latency: '—',     auth: 'OAuth2' },
    { name: 'sanctions-screen', url: 'mcp://partner/ofac-sanctions',     status: 'error',      tools:  3, latency: '—',     auth: 'mTLS', err: 'TLS handshake failed' },
  ];
  const stTone = {
    connected:  { c: 'var(--success, #1f7a5e)', t: 'Connected' },
    connecting: { c: '#b4741e',                 t: 'Connecting' },
    error:      { c: '#c44536',                 t: 'Error' },
  };

  return (
    <Section
      title="MCP Servers"
      subtitle="External Model Context Protocol servers this agent connects to. Each server exposes a typed tool surface guarded by tenant-level auth."
      count={servers.length}
      action={<>
        <button className="btn btn-ghost btn-sm">Browse marketplace</button>
        <button className="btn btn-outline btn-sm"><Icon name="plus" size={11}/> Connect server</button>
      </>}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {servers.map(s => {
          const st = stTone[s.status];
          return (
            <div key={s.name} style={{
              display: 'grid', gridTemplateColumns: '36px 1fr 110px 90px 90px 24px',
              gap: 14, alignItems: 'center', padding: '12px 14px',
              background: 'var(--surface)', border: '1px solid var(--border-light)',
              borderRadius: 10,
            }}>
              <div style={{
                width: 32, height: 32, borderRadius: 7,
                background: 'var(--surface-inset)',
                display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                position: 'relative',
              }}>
                <Icon name="server" size={14} color="var(--text-secondary)"/>
                <span style={{
                  position: 'absolute', bottom: -2, right: -2,
                  width: 10, height: 10, borderRadius: 999,
                  background: st.c, border: '2px solid var(--surface)',
                  ...(s.status === 'connecting' ? { animation: 'agt-pulse 1.4s ease-out infinite' } : {}),
                }}/>
              </div>
              <div style={{ minWidth: 0 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{s.name}</span>
                  {s.native && <span style={{ fontSize: 9.5, padding: '1px 5px', borderRadius: 3, background: 'var(--brand-primary-10)', color: 'var(--brand-primary)', fontWeight: 600, letterSpacing: '0.04em' }}>NATIVE</span>}
                </div>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {s.url}
                  {s.err && <span style={{ color: '#c44536', marginLeft: 8 }}>· {s.err}</span>}
                </div>
              </div>
              <span style={{ fontSize: 11, color: st.c, fontWeight: 600, display: 'inline-flex', alignItems: 'center', gap: 5 }}>
                <span style={{ width: 6, height: 6, borderRadius: 999, background: st.c }}/>
                {st.t}
              </span>
              <div style={{ fontSize: 11, color: 'var(--text-secondary)', fontFamily: 'var(--font-mono)' }}>
                <span style={{ color: 'var(--text-tertiary)' }}>tools </span>{s.tools}
              </div>
              <div style={{ fontSize: 11, color: 'var(--text-secondary)', fontFamily: 'var(--font-mono)' }}>
                {s.latency}
              </div>
              <Icon name="more" size={12} color="var(--text-tertiary)"/>
            </div>
          );
        })}
      </div>
    </Section>
  );
}

// ─── Right rail: Connection map (animated SVG) ─────────────────────
function ConnectionMap({ agent }) {
  // Subtle network: agent center, sub-agents around, faint links pulsing
  const subs = [
    { ref: 'ledger',  angle: -90 },
    { ref: 'fx',      angle: -20 },
    { ref: 'compl',   angle:  60 },
    { ref: 'dunn',    angle: 160 },
  ];
  const cx = 160, cy = 130, r = 72;

  return (
    <Card title="Network" eyebrow="Live · last 60s">
      <div style={{ position: 'relative', height: 240 }}>
        <svg width="100%" height="240" viewBox="0 0 320 240" style={{ display: 'block' }}>
          <defs>
            <radialGradient id="cm-glow" cx="0.5" cy="0.5" r="0.5">
              <stop offset="0%" stopColor={agent.color} stopOpacity="0.35"/>
              <stop offset="100%" stopColor={agent.color} stopOpacity="0"/>
            </radialGradient>
          </defs>
          {/* glow */}
          <circle cx={cx} cy={cy} r="70" fill="url(#cm-glow)"/>
          {/* concentric */}
          <circle cx={cx} cy={cy} r="58" fill="none" stroke="var(--border-light)" strokeDasharray="2 4"/>
          <circle cx={cx} cy={cy} r="84" fill="none" stroke="var(--border-light)" strokeDasharray="2 4" opacity="0.6"/>

          {/* link lines + animated dots */}
          {subs.map((s, i) => {
            const sub = AGENT_LIST.find(a => a.id === s.ref);
            const x = cx + Math.cos(s.angle * Math.PI / 180) * r;
            const y = cy + Math.sin(s.angle * Math.PI / 180) * r;
            return (
              <g key={s.ref}>
                <line x1={cx} y1={cy} x2={x} y2={y}
                  stroke={sub.color} strokeOpacity="0.3" strokeWidth="1"/>
                <circle r="3" fill={sub.color}>
                  <animateMotion dur={`${3 + i * 0.6}s`} repeatCount="indefinite"
                    path={`M${cx} ${cy} L${x} ${y}`}/>
                </circle>
              </g>
            );
          })}

          {/* sub-agent nodes */}
          {subs.map(s => {
            const sub = AGENT_LIST.find(a => a.id === s.ref);
            const x = cx + Math.cos(s.angle * Math.PI / 180) * r;
            const y = cy + Math.sin(s.angle * Math.PI / 180) * r;
            return (
              <g key={'n'+s.ref}>
                <circle cx={x} cy={y} r="14" fill={sub.color} fillOpacity="0.15" stroke={sub.color} strokeWidth="1"/>
                <circle cx={x} cy={y} r="6"  fill={sub.color}/>
                <text x={x} y={y + 26} textAnchor="middle" fontSize="9.5" fontFamily="var(--font-mono)" fill="var(--text-secondary)">
                  {sub.name.split(' ')[0]}
                </text>
              </g>
            );
          })}

          {/* center node — the agent */}
          <circle cx={cx} cy={cy} r="22" fill={agent.color}/>
          <circle cx={cx} cy={cy} r="22" fill="none" stroke={agent.color} strokeOpacity="0.3" strokeWidth="2">
            <animate attributeName="r" from="22" to="34" dur="2s" repeatCount="indefinite"/>
            <animate attributeName="stroke-opacity" from="0.4" to="0" dur="2s" repeatCount="indefinite"/>
          </circle>
          <text x={cx} y={cy + 4} textAnchor="middle" fontSize="11" fontFamily="var(--font-brand)" fontWeight="600" fill="#fff">
            {agent.name.split(' ')[0]}
          </text>
        </svg>
      </div>
      <div style={{
        marginTop: 4, padding: '10px 12px', borderTop: '1px solid var(--border-light)',
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      }}>
        <span style={{ fontSize: 11, color: 'var(--text-secondary)' }}>4 sub-agents · 220 calls / hr</span>
        <button className="btn btn-ghost btn-sm" style={{ fontSize: 11 }}>Open graph →</button>
      </div>
    </Card>
  );
}

// ─── Right rail: Recent runs ───────────────────────────────────────
function RecentRunsCard({ agent }) {
  const runs = [
    { op: 'match_and_apply',  status: 'ok',    dur: '3.14s', t: 'now',    txn: 'INV-2041' },
    { op: 'apply_invoice',    status: 'held',  dur: '0.84s', t: '11m',    txn: 'INV-2038' },
    { op: 'match_and_apply',  status: 'ok',    dur: '2.94s', t: '24m',    txn: 'INV-2037' },
    { op: 'summarize_ar',     status: 'ok',    dur: '1.94s', t: '1h',     txn: '—' },
    { op: 'dunning_send',     status: 'ok',    dur: '0.42s', t: '2h',     txn: 'INV-2014' },
    { op: 'reconcile_fx',     status: 'err',   dur: '4.21s', t: '3h',     txn: 'INV-2009' },
  ];
  const tone = {
    ok:   { c: 'var(--success, #1f7a5e)', t: 'ok' },
    held: { c: '#b4741e',                 t: 'held' },
    err:  { c: '#c44536',                 t: 'err' },
  };
  return (
    <Card title="Recent runs" eyebrow={`${runs.length} of 318`} action={<button className="btn btn-ghost btn-sm" style={{ fontSize: 11 }}>View all →</button>}>
      <div style={{ display: 'flex', flexDirection: 'column' }}>
        {runs.map((r, i) => (
          <div key={i} style={{
            display: 'grid', gridTemplateColumns: '36px 1fr 50px',
            gap: 8, padding: '9px 12px', alignItems: 'center',
            borderTop: i === 0 ? '1px solid var(--border-light)' : 'none',
            borderBottom: '1px solid var(--border-light)',
          }}>
            <span style={{
              fontFamily: 'var(--font-mono)', fontSize: 9.5,
              padding: '2px 6px', borderRadius: 3,
              background: tone[r.status].c + '1a', color: tone[r.status].c, fontWeight: 600,
              textAlign: 'center', textTransform: 'uppercase', letterSpacing: '0.04em',
            }}>{tone[r.status].t}</span>
            <div style={{ minWidth: 0 }}>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{r.op}</div>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)', marginTop: 1 }}>{r.txn} · {r.dur}</div>
            </div>
            <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)', textAlign: 'right' }}>{r.t}</span>
          </div>
        ))}
      </div>
    </Card>
  );
}

// ─── Right rail: Policy / safety summary ──────────────────────────
function PolicyCard({ agent }) {
  return (
    <Card title="Policies & safety" eyebrow="5 active">
      <div style={{ padding: '12px 14px', display: 'flex', flexDirection: 'column', gap: 10 }}>
        {[
          { i: 'shield',   t: 'Dual-control payouts', d: 'Two approvers for any outbound payout', enforced: true },
          { i: 'lock',     t: 'Amount ceiling',       d: 'Always require approval > £50,000',     enforced: true },
          { i: 'lock',     t: 'PII redaction',        d: 'Customer PII stripped from all prompts', enforced: true },
          { i: 'sparkles', t: 'Auto-apply',            d: 'Confidence ≥ 0.95 · audit on apply',     enforced: agent.autoApply, soft: true },
        ].map((p, i) => (
          <div key={i} style={{ display: 'flex', alignItems: 'flex-start', gap: 10 }}>
            <Icon name={p.i} size={13} color={p.soft ? 'var(--brand-primary)' : 'var(--text-secondary)'} style={{ marginTop: 2 }}/>
            <div style={{ flex: 1 }}>
              <div style={{ fontSize: 12.5, fontWeight: 500, color: 'var(--text-primary)' }}>{p.t}</div>
              <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 1 }}>{p.d}</div>
            </div>
            <Pill tone={p.enforced ? (p.soft ? 'tint' : 'success') : 'muted'} size="sm">
              {p.enforced ? (p.soft ? 'on' : 'enforced') : 'off'}
            </Pill>
          </div>
        ))}
      </div>
    </Card>
  );
}

// ─── Generic primitives ─────────────────────────────────────────────
function Section({ title, subtitle, count, action, children }) {
  return (
    <section>
      <div style={{ display: 'flex', alignItems: 'flex-end', gap: 14, marginBottom: 12 }}>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <h2 style={{ fontFamily: 'var(--font-brand)', fontSize: 18, letterSpacing: '-0.01em', margin: 0, color: 'var(--text-primary)' }}>{title}</h2>
            {count != null && (
              <span style={{
                fontFamily: 'var(--font-mono)', fontSize: 11, fontWeight: 600,
                padding: '2px 8px', borderRadius: 999,
                background: 'var(--surface-inset)', color: 'var(--text-tertiary)',
              }}>{count}</span>
            )}
          </div>
          {subtitle && <p style={{ fontSize: 12.5, color: 'var(--text-secondary)', margin: '4px 0 0', maxWidth: 720, lineHeight: 1.5 }}>{subtitle}</p>}
        </div>
        <div style={{ display: 'flex', gap: 8, flex: 'none' }}>{action}</div>
      </div>
      {children}
    </section>
  );
}

function Card({ title, eyebrow, action, children }) {
  return (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 12, overflow: 'hidden',
    }}>
      {(title || eyebrow) && (
        <div style={{
          padding: '12px 14px',
          display: 'flex', alignItems: 'center', gap: 8,
          borderBottom: '1px solid var(--border-light)',
        }}>
          <div style={{ flex: 1, minWidth: 0 }}>
            {title && <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{title}</div>}
            {eyebrow && <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 1, fontFamily: 'var(--font-mono)' }}>{eyebrow}</div>}
          </div>
          {action}
        </div>
      )}
      {children}
    </div>
  );
}

const ANIM_CSS = `
@keyframes agt-spin-slow { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
@keyframes agt-spin-rev  { from { transform: rotate(360deg); } to { transform: rotate(0deg); } }
@keyframes agt-float     { 0%, 100% { transform: translateY(0); } 50% { transform: translateY(-6px); } }
@keyframes agt-pulse     { 0% { transform: scale(1); opacity: 0.5; } 100% { transform: scale(2.4); opacity: 0; } }
@keyframes agt-drift     { 0%, 100% { transform: translate(0, 0); } 50% { transform: translate(20px, 14px); } }
.hover-lift:hover { border-color: var(--text-secondary) !important; transform: translateY(-1px); }
`;

// ─── Deep tab content ───────────────────────────────────────────────

// Sub-agents tab — full directory + interactive call graph + invocation history
function TabSubAgents({ agent }) {
  const subs = [
    { ref: 'ledger', role: 'Posts journal entries from matched txns', calls: 142, avgMs: 218, autonomy: 'auto',    last: '2m', success: 98.4, sla: 99 },
    { ref: 'fx',     role: 'Quotes rates for cross-currency invoices', calls:  38, avgMs: 412, autonomy: 'propose', last: '8m', success: 94.7, sla: 98 },
    { ref: 'compl',  role: 'KYC re-checks before any new counterparty', calls: 12, avgMs: 894, autonomy: 'block',   last: '24m', success: 100,  sla: 99.9 },
    { ref: 'dunn',   role: 'Drafts overdue reminders when match fails', calls:  28, avgMs: 142, autonomy: 'propose', last: '1h', success: 96.4, sla: 98 },
  ];
  const autoTone = {
    auto:    { bg: '#1f7a5e1a', fg: '#1f7a5e', t: 'Auto-apply' },
    propose: { bg: '#b4741e1a', fg: '#b4741e', t: 'Propose' },
    block:   { bg: '#7b76b61a', fg: '#7b76b6', t: 'Required' },
  };

  // Recent invocation log
  const log = [
    { from: 'self',   to: 'ledger', op: 'apply_journal_entry',  at: 'now',    ms: 142, status: 'ok'   },
    { from: 'self',   to: 'compl',  op: 'kyc_recheck',          at: '2m',     ms: 894, status: 'ok'   },
    { from: 'self',   to: 'fx',     op: 'quote_forward',        at: '4m',     ms: 412, status: 'ok'   },
    { from: 'self',   to: 'ledger', op: 'apply_journal_entry',  at: '11m',    ms: 188, status: 'held' },
    { from: 'self',   to: 'dunn',   op: 'queue_reminder',       at: '1h',     ms: 142, status: 'ok'   },
    { from: 'self',   to: 'fx',     op: 'quote_forward',        at: '3h',     ms: 521, status: 'err'  },
  ];
  const lt = { ok: '#1f7a5e', held: '#b4741e', err: '#c44536' };

  return (
    <div style={{ display: 'grid', gridTemplateColumns: '1fr 380px', gap: 24 }}>
      {/* Main column */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
        <Section
          title="Connected sub-agents"
          subtitle="Other agents Billing delegates work to. Calls go through the orchestrator and are subject to the destination's policies."
          count={subs.length}
          action={<>
            <button className="btn btn-ghost btn-sm">Open call graph</button>
            <button className="btn btn-outline btn-sm"><Icon name="plus" size={11}/> Connect agent</button>
          </>}>
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, overflow: 'hidden' }}>
            <div style={{
              display: 'grid', gridTemplateColumns: '52px 1.5fr 1fr 110px 80px 90px 24px',
              gap: 14, padding: '10px 16px',
              background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)',
              fontSize: 10, fontWeight: 600, letterSpacing: '0.06em', textTransform: 'uppercase', color: 'var(--text-tertiary)',
            }}>
              <div/>
              <div>Agent · role</div>
              <div>Autonomy</div>
              <div style={{ textAlign: 'right' }}>Calls · 24h</div>
              <div style={{ textAlign: 'right' }}>Success</div>
              <div style={{ textAlign: 'right' }}>Avg · last</div>
              <div/>
            </div>
            {subs.map((s, i) => {
              const sub = AGENT_LIST.find(a => a.id === s.ref);
              const tone = autoTone[s.autonomy];
              return (
                <div key={s.ref} className="hover-lift" style={{
                  display: 'grid', gridTemplateColumns: '52px 1.5fr 1fr 110px 80px 90px 24px',
                  gap: 14, padding: '14px 16px', alignItems: 'center', cursor: 'pointer',
                  borderBottom: i < subs.length - 1 ? '1px solid var(--border-light)' : 'none',
                  background: 'var(--surface)',
                }}>
                  <AgentPortrait agent={sub} size={42} ring={false}/>
                  <div style={{ minWidth: 0 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <span style={{ fontSize: 13.5, fontWeight: 600, color: 'var(--text-primary)' }}>{sub.name}</span>
                      <Pill tone="tint" size="sm">{sub.kind}</Pill>
                    </div>
                    <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{s.role}</div>
                  </div>
                  <span style={{
                    fontSize: 9.5, padding: '3px 8px', borderRadius: 4, justifySelf: 'start',
                    background: tone.bg, color: tone.fg, fontWeight: 600,
                    letterSpacing: '0.06em', textTransform: 'uppercase',
                  }}>{tone.t}</span>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-secondary)', textAlign: 'right' }}>{s.calls}</div>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: s.success >= s.sla ? 'var(--success, #1f7a5e)' : '#b4741e', textAlign: 'right' }}>{s.success.toFixed(1)}%</div>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-tertiary)', textAlign: 'right' }}>
                    <div>{s.avgMs}ms</div>
                    <div style={{ fontSize: 10, marginTop: 1 }}>{s.last} ago</div>
                  </div>
                  <Icon name="chevron" size={11} color="var(--text-tertiary)"/>
                </div>
              );
            })}
          </div>
        </Section>

        <Section
          title="Recent delegations"
          subtitle="Invocation log across all sub-agents (24h)."
          count={log.length}
          action={<button className="btn btn-ghost btn-sm">Open in Traces →</button>}>
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
            {log.map((l, i) => {
              const target = AGENT_LIST.find(a => a.id === l.to);
              return (
                <div key={i} style={{
                  display: 'grid', gridTemplateColumns: '40px 1fr 1fr 80px 60px 50px',
                  gap: 14, padding: '10px 14px', alignItems: 'center',
                  borderBottom: i < log.length - 1 ? '1px solid var(--border-light)' : 'none',
                }}>
                  <span style={{
                    fontFamily: 'var(--font-mono)', fontSize: 9.5, fontWeight: 600,
                    padding: '2px 6px', borderRadius: 3, textAlign: 'center',
                    background: lt[l.status] + '1a', color: lt[l.status],
                    textTransform: 'uppercase', letterSpacing: '0.04em',
                  }}>{l.status}</span>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <AgentPortrait agent={agent} size={20} ring={false}/>
                    <span style={{ fontSize: 11, color: 'var(--text-secondary)' }}>Billing</span>
                    <Icon name="chevron" size={10} color="var(--text-tertiary)"/>
                    <AgentPortrait agent={target} size={20} ring={false}/>
                    <span style={{ fontSize: 11, color: 'var(--text-primary)', fontWeight: 500 }}>{target.name.replace(' Agent','')}</span>
                  </div>
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-primary)' }}>{l.op}</span>
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)', textAlign: 'right' }}>{l.ms}ms</span>
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)', textAlign: 'right' }}>{l.at}</span>
                  <button className="btn btn-ghost btn-sm" style={{ height: 22, padding: '0 6px', fontSize: 10.5 }}>trace</button>
                </div>
              );
            })}
          </div>
        </Section>
      </div>

      {/* Right column — full-size graph + invitation card */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
        <ConnectionMap agent={agent}/>
        <Card title="How sub-agents work">
          <div style={{ padding: '12px 14px', fontSize: 12, color: 'var(--text-secondary)', lineHeight: 1.6 }}>
            <p style={{ margin: '0 0 10px' }}>
              When this agent calls another, the request is wrapped in the caller's trace and inherits its tenant scope.
            </p>
            <p style={{ margin: '0 0 10px' }}>
              The callee runs under <b>its own</b> policies — Billing can ask Compliance to run a check, but Compliance still requires its own approvals before mutating anything.
            </p>
            <p style={{ margin: 0 }}>
              Use <b>Required</b> when the callee must approve before this agent can proceed.
            </p>
          </div>
        </Card>
      </div>
    </div>
  );
}

// Tools tab — toolbox view, schemas, recent calls
function TabTools({ agent }) {
  const tools = [
    { name: 'search_invoices',         cat: 'read',    desc: 'Query the invoice store by counterparty, status or amount.', uses: 1842, p99: '142ms', errors: 0,  enabled: true },
    { name: 'list_bank_transactions',  cat: 'read',    desc: 'Read bank txns inside a date window from connected rails.',  uses: 1318, p99: '318ms', errors: 4,  enabled: true },
    { name: 'match_invoice_to_txn',    cat: 'compute', desc: 'Score a candidate match (0–1) using ledger + memo signals.', uses: 1284, p99: '211ms', errors: 12, enabled: true },
    { name: 'draft_journal_entry',     cat: 'write',   desc: 'Compose a balanced debit/credit pair (proposal only).',      uses:  892, p99: '88ms',  errors: 0,  enabled: true },
    { name: 'apply_journal_entry',     cat: 'write',   desc: 'Post a drafted entry to the ledger after approval.',         uses:  416, p99: '142ms', errors: 2,  enabled: true },
    { name: 'send_dunning_email',      cat: 'write',   desc: 'Compose and dispatch an overdue reminder.',                  uses:   28, p99: '512ms', errors: 0,  enabled: false },
    { name: 'display_proposal_card',   cat: 'display', desc: 'Render an Apply / Review / Dismiss tool card in chat.',      uses:  892, p99: '12ms',  errors: 0,  enabled: true },
    { name: 'confirm_action',          cat: 'display', desc: 'Halt and ask the human for explicit approval.',              uses:  142, p99: '8ms',   errors: 0,  enabled: true },
  ];
  const catColor = {
    read:    { bg: 'var(--brand-primary-10)', fg: 'var(--brand-primary)' },
    write:   { bg: '#eb5c371a', fg: '#eb5c37' },
    compute: { bg: '#3ab7951a', fg: '#3ab795' },
    display: { bg: '#7b76b61a', fg: '#7b76b6' },
  };
  const [sel, setSel] = React.useState(2);
  const t = tools[sel];

  return (
    <div style={{ display: 'grid', gridTemplateColumns: '1fr 460px', gap: 24 }}>
      <Section
        title="Tools"
        subtitle="Read pulls data; write mutates state and runs through policy clearance; compute is pure scoring; display renders UI in the chat."
        count={tools.length}
        action={<>
          <button className="btn btn-ghost btn-sm"><Icon name="filter" size={11}/> Kind</button>
          <button className="btn btn-outline btn-sm"><Icon name="plus" size={11}/> Add tool</button>
        </>}>
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
          {tools.map((tt, i) => {
            const tone = catColor[tt.cat];
            const active = i === sel;
            return (
              <div key={tt.name} onClick={() => setSel(i)} style={{
                display: 'grid', gridTemplateColumns: '90px 1fr 80px 80px 16px',
                gap: 14, padding: '14px', alignItems: 'center', cursor: 'pointer',
                borderBottom: i < tools.length - 1 ? '1px solid var(--border-light)' : 'none',
                background: active ? agent.color + '0a' : 'transparent',
                borderLeft: '3px solid ' + (active ? agent.color : 'transparent'),
                opacity: tt.enabled ? 1 : 0.55,
              }}>
                <span style={{
                  fontSize: 9.5, padding: '2px 7px', borderRadius: 3,
                  background: tone.bg, color: tone.fg, fontWeight: 600,
                  letterSpacing: '0.06em', textTransform: 'uppercase', textAlign: 'center',
                  fontFamily: 'var(--font-mono)', justifySelf: 'start',
                }}>{tt.cat}</span>
                <div>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{tt.name}</div>
                  <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{tt.desc}</div>
                </div>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-secondary)', textAlign: 'right' }}>{tt.uses.toLocaleString()}</div>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: tt.errors > 0 ? '#c44536' : 'var(--text-secondary)', textAlign: 'right' }}>
                  {tt.errors > 0 ? `${tt.errors} err` : tt.p99}
                </div>
                <Icon name="chevron" size={10} color={active ? agent.color : 'var(--text-tertiary)'}/>
              </div>
            );
          })}
        </div>
      </Section>

      {/* Inspector */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <Card title={t.name} eyebrow={`${t.cat} tool · v2.1.0`} action={<button className="btn btn-ghost btn-sm">Open in Playground →</button>}>
          <div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 14 }}>
            <p style={{ margin: 0, fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.55 }}>{t.desc}</p>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 8 }}>
              <MiniStat l="Uses · 24h" v={t.uses.toLocaleString()}/>
              <MiniStat l="p99" v={t.p99}/>
              <MiniStat l="Errors" v={t.errors} tone={t.errors > 0 ? '#c44536' : null}/>
            </div>
            <div>
              <FieldLabel>Input schema</FieldLabel>
              <pre style={{
                margin: 0, fontFamily: 'var(--font-mono)', fontSize: 11, lineHeight: 1.55,
                background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 8,
                padding: 12, color: 'var(--text-primary)', whiteSpace: 'pre-wrap',
              }}>{`{
  "invoice_id": "string",
  "candidate_txn_id": "string",
  "tolerance_pct": "number  // default 0.5"
}`}</pre>
            </div>
            <div>
              <FieldLabel>Returns</FieldLabel>
              <pre style={{
                margin: 0, fontFamily: 'var(--font-mono)', fontSize: 11, lineHeight: 1.55,
                background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 8,
                padding: 12, color: 'var(--text-primary)', whiteSpace: 'pre-wrap',
              }}>{`{
  "score": 0.0..1.0,
  "reasons": [string],
  "tentative_journal": JournalEntry | null
}`}</pre>
            </div>
          </div>
        </Card>
        <Card title="Recent invocations" eyebrow={`last 5 of ${t.uses.toLocaleString()}`}>
          <div>
            {[
              { ms: 142, at: 'now',  status: 'ok',  ref: 'INV-2041' },
              { ms: 88,  at: '11m',  status: 'ok',  ref: 'INV-2038' },
              { ms: 211, at: '24m',  status: 'ok',  ref: 'INV-2037' },
              { ms: 318, at: '1h',   status: 'err', ref: 'INV-2031' },
              { ms: 142, at: '2h',   status: 'ok',  ref: 'INV-2024' },
            ].map((r, i, arr) => (
              <div key={i} style={{
                display: 'grid', gridTemplateColumns: '40px 1fr 60px 50px',
                gap: 10, padding: '8px 14px', alignItems: 'center',
                borderBottom: i < arr.length - 1 ? '1px solid var(--border-light)' : 'none',
              }}>
                <span style={{
                  fontFamily: 'var(--font-mono)', fontSize: 9.5,
                  padding: '2px 6px', borderRadius: 3, textAlign: 'center',
                  background: (r.status === 'ok' ? '#1f7a5e' : '#c44536') + '1a',
                  color: r.status === 'ok' ? '#1f7a5e' : '#c44536',
                  fontWeight: 600, textTransform: 'uppercase',
                }}>{r.status}</span>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-primary)' }}>{r.ref}</span>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)', textAlign: 'right' }}>{r.ms}ms</span>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)', textAlign: 'right' }}>{r.at}</span>
              </div>
            ))}
          </div>
        </Card>
      </div>
    </div>
  );
}

// ─── Skills tab ──────────────────────────────────────────────────────
// Models the Agent Skills open spec (agentskills.io): each skill is a folder
// with a SKILL.md (YAML frontmatter: name + description + optional fields),
// optional bundled scripts/, references/, assets/. Loaded by progressive
// disclosure — Discovery (name + description only) → Activation (full
// SKILL.md into context) → Execution (instructions + bundled files).
function TabSkills({ agent }) {
  const skills = [
    {
      slug: 'invoice-reconciliation',
      name: 'Invoice reconciliation',
      description: 'Match incoming bank transactions to outstanding invoices and draft journal entries when the confidence is high enough to skip review.',
      version: '1.4.2',
      tokens: 1.8,           // KB on disk for SKILL.md
      activation: { last24h: 412, lastFire: '2m ago', hitRate: 0.96 },
      source: { kind: 'org', label: 'aonik-org/skills', path: 'finance/invoice-reconciliation' },
      installed: '2 weeks ago',
      visibility: 'org',
      status: 'active',
      tree: [
        { type: 'file',   path: 'SKILL.md',            size: '1.8 KB' },
        { type: 'folder', path: 'scripts',             children: [
          { type: 'file', path: 'scripts/score.py',      size: '3.2 KB' },
          { type: 'file', path: 'scripts/normalize.py',  size: '1.4 KB' },
        ]},
        { type: 'folder', path: 'references',          children: [
          { type: 'file', path: 'references/policy.md',  size: '2.1 KB' },
          { type: 'file', path: 'references/idioms.md',  size: '1.0 KB' },
        ]},
        { type: 'folder', path: 'assets',              children: [
          { type: 'file', path: 'assets/proposal.tmpl', size: '0.6 KB' },
        ]},
      ],
      skillMd: `---
name: invoice-reconciliation
description: |
  Use when the user wants to match a bank transaction to an open invoice,
  reconcile incoming receipts, or draft the journal entry that closes them.
  Triggers on phrases like "match invoice X to txn Y" or on banking.transaction.received events.
allowed-tools: [search_invoices, list_bank_transactions, match_invoice_to_txn, draft_journal_entry]
---

# Invoice reconciliation

You are reconciling a bank transaction against the open AR ledger.

## When to use this skill

- The user references a specific invoice and a banking transaction.
- A \`banking.transaction.received\` event is in scope and an open invoice
  exists with a matching counterparty, amount, or memo.

## Procedure

1. Pull candidate transactions within ±£0.50 of the invoice amount with
   \`list_bank_transactions\`.
2. Score each candidate with \`scripts/score.py\` (geometric mean of
   amount / counterparty / memo similarity).
3. If the best score is **≥ 0.95**, call \`draft_journal_entry\` —
   otherwise return the top three for human review.
4. Refuse to auto-post if the transaction amount exceeds the policy ceiling
   in \`references/policy.md\`.

## Anti-patterns

See \`references/idioms.md\` for cases this skill must NOT touch
(intercompany, reversals, and FX-sensitive lines).
`,
    },
    {
      slug: 'bank-statement-intake',
      name: 'Bank statement intake',
      description: 'Parse uploaded CSV/OFX bank statements, normalise counterparties and post each line as a draft transaction in the staging ledger.',
      version: '2.0.1',
      tokens: 2.4,
      activation: { last24h: 184, lastFire: '11m ago', hitRate: 0.98 },
      source: { kind: 'org', label: 'aonik-org/skills', path: 'finance/bank-statement-intake' },
      installed: '1 month ago',
      visibility: 'org',
      status: 'active',
    },
    {
      slug: 'ar-aging-summary',
      name: 'AR aging summary',
      description: 'Produce an aging summary across the AR ledger with sub-totals by tier and an action-prioritised list of customers to chase.',
      version: '1.0.0',
      tokens: 0.9,
      activation: { last24h: 88, lastFire: '38m ago', hitRate: 1.0 },
      source: { kind: 'community', label: 'agentskills/finance', path: 'ar-aging' },
      installed: '3 weeks ago',
      visibility: 'org',
      status: 'active',
    },
    {
      slug: 'dunning-cadence',
      name: 'Dunning cadence',
      description: 'Choose a dunning template and channel for an overdue invoice, taking customer tier, prior contact and the invoice age into account.',
      version: '1.2.0',
      tokens: 1.6,
      activation: { last24h: 42, lastFire: '1h ago', hitRate: 0.92 },
      source: { kind: 'org', label: 'aonik-org/skills', path: 'finance/dunning' },
      installed: '12 days ago',
      visibility: 'org',
      status: 'active',
    },
    {
      slug: 'currency-rounding-fix',
      name: 'Currency rounding fix',
      description: 'Detect and reverse off-by-cent rounding errors that appear when invoices are issued in one currency and settled in another.',
      version: '0.3.0',
      tokens: 1.1,
      activation: { last24h: 6, lastFire: '4h ago', hitRate: 0.84 },
      source: { kind: 'private', label: 'maria@aonik', path: 'currency-rounding' },
      installed: '4 days ago',
      visibility: 'private',
      status: 'beta',
    },
    {
      slug: 'period-close-prep',
      name: 'Period close prep',
      description: 'Walk the month-end close checklist, surface unposted entries and produce the GL trial balance for review by the controller.',
      version: '0.7.1',
      tokens: 3.2,
      activation: { last24h: 12, lastFire: 'yesterday', hitRate: 0.94 },
      source: { kind: 'org', label: 'aonik-org/skills', path: 'finance/period-close' },
      installed: '5 days ago',
      visibility: 'org',
      status: 'beta',
    },
  ];

  const [sel, setSel] = React.useState(0);
  const [filter, setFilter] = React.useState('all');
  const filtered = filter === 'all' ? skills : skills.filter(s => s.status === filter || s.source.kind === filter);
  const s = filtered[sel] || filtered[0];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      {/* Spec banner — what skills are, in this product's voice */}
      <div style={{
        display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 0,
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 12, overflow: 'hidden',
      }}>
        {[
          { eyebrow: '1 · Discovery',   title: 'Read name + description', body: 'On every turn the agent skims registered skills — only the YAML frontmatter is loaded.' },
          { eyebrow: '2 · Activation',  title: 'Load full SKILL.md',      body: 'When a skill matches the task, its full instructions enter the context window.' },
          { eyebrow: '3 · Execution',   title: 'Run scripts + refs',      body: 'Bundled scripts execute in the agent sandbox; references and assets are pulled on demand.' },
        ].map((step, i) => (
          <div key={i} style={{
            padding: '14px 18px',
            borderRight: i < 2 ? '1px solid var(--border-light)' : 'none',
            display: 'flex', flexDirection: 'column', gap: 4,
          }}>
            <div style={{ fontSize: 10, fontFamily: 'var(--font-mono)', color: agent.color, fontWeight: 600, letterSpacing: '0.06em' }}>{step.eyebrow}</div>
            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{step.title}</div>
            <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', lineHeight: 1.5 }}>{step.body}</div>
          </div>
        ))}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 520px', gap: 24, alignItems: 'start' }}>
        <Section
          title="Installed skills"
          subtitle="Each skill is a folder with a SKILL.md and optional bundled scripts, references and assets — following the open Agent Skills spec."
          count={skills.length}
          action={<>
            <button className="btn btn-ghost btn-sm"><Icon name="search" size={11}/> Browse registry</button>
            <button className="btn btn-outline btn-sm"><Icon name="upload" size={11}/> Install skill</button>
          </>}>
          {/* Filter chips */}
          <div style={{ display: 'flex', gap: 6, marginBottom: 12, flexWrap: 'wrap' }}>
            {[
              { id: 'all',       label: `All · ${skills.length}` },
              { id: 'active',    label: `Active · ${skills.filter(x => x.status === 'active').length}` },
              { id: 'beta',      label: `Beta · ${skills.filter(x => x.status === 'beta').length}` },
              { id: 'org',       label: 'Org' },
              { id: 'community', label: 'Community' },
              { id: 'private',   label: 'Private' },
            ].map(f => (
              <button key={f.id} onClick={() => { setFilter(f.id); setSel(0); }} style={{
                fontSize: 11, padding: '4px 10px', borderRadius: 999, cursor: 'pointer',
                background: filter === f.id ? agent.color + '14' : 'var(--surface)',
                color:      filter === f.id ? agent.color : 'var(--text-secondary)',
                border:     `1px solid ${filter === f.id ? agent.color + '55' : 'var(--border-light)'}`,
                fontWeight: filter === f.id ? 600 : 500,
              }}>{f.label}</button>
            ))}
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {filtered.map((sk, i) => {
              const active = sk.slug === s?.slug;
              const sourceTone = { org: '#055a60', community: '#7b76b6', private: '#b4741e' }[sk.source.kind];
              return (
                <div key={sk.slug} onClick={() => setSel(i)} className="hover-lift" style={{
                  padding: 14, cursor: 'pointer',
                  background: active ? agent.color + '0a' : 'var(--surface)',
                  border: `1px solid ${active ? agent.color : 'var(--border-light)'}`,
                  borderRadius: 10,
                  display: 'grid', gridTemplateColumns: '32px 1fr auto', gap: 12, alignItems: 'flex-start',
                }}>
                  <div style={{
                    width: 32, height: 32, borderRadius: 7,
                    background: agent.color + '14', color: agent.color,
                    display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                  }}><Icon name="folderOpen" size={15}/></div>
                  <div style={{ minWidth: 0 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                      <span style={{ fontSize: 13.5, fontWeight: 600, color: 'var(--text-primary)' }}>{sk.name}</span>
                      <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>v{sk.version}</span>
                      {sk.status === 'beta' && <span style={{ fontSize: 9.5, padding: '1px 5px', borderRadius: 3, background: '#b4741e1a', color: '#b4741e', fontWeight: 600, letterSpacing: '0.04em' }}>BETA</span>}
                      <span style={{ fontSize: 9.5, padding: '1px 6px', borderRadius: 3, background: sourceTone + '14', color: sourceTone, fontWeight: 600, letterSpacing: '0.04em', textTransform: 'uppercase' }}>{sk.source.kind}</span>
                    </div>
                    <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 4, lineHeight: 1.5 }}>{sk.description}</div>
                  </div>
                  <div style={{ textAlign: 'right', whiteSpace: 'nowrap' }}>
                    <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-primary)' }}>{sk.activation.last24h}</div>
                    <div style={{ fontSize: 10, color: 'var(--text-tertiary)', marginTop: 1 }}>activations · 24h</div>
                  </div>
                </div>
              );
            })}
          </div>
        </Section>

        {s && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 16, position: 'sticky', top: 0 }}>
            <Card
              title={s.name}
              eyebrow={<span style={{ fontFamily: 'var(--font-mono)' }}>{s.source.label}/{s.source.path} · v{s.version}</span>}
              action={<>
                <button className="btn btn-ghost btn-sm"><Icon name="play" size={10}/> Test</button>
                <button className="btn btn-outline btn-sm"><Icon name="edit" size={10}/> Edit</button>
              </>}>
              <div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 14 }}>
                <p style={{ fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.6, margin: 0 }}>{s.description}</p>

                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 8 }}>
                  <MiniStat l="Activations · 24h" v={s.activation.last24h}/>
                  <MiniStat l="Hit rate" v={Math.round(s.activation.hitRate * 100) + '%'} tone={s.activation.hitRate >= 0.95 ? '#1f7a5e' : '#b4741e'}/>
                  <MiniStat l="SKILL.md size" v={s.tokens.toFixed(1) + ' KB'}/>
                </div>

                {/* SKILL.md preview */}
                {s.skillMd && (
                  <div>
                    <FieldLabel>SKILL.md · loaded on activation</FieldLabel>
                    <div style={{
                      background: '#1a1d21', borderRadius: 8,
                      fontFamily: 'var(--font-mono)', fontSize: 11, lineHeight: 1.55,
                      color: '#d8dde2', maxHeight: 260, overflow: 'auto',
                      padding: '10px 12px',
                    }}>
                      {s.skillMd.split('\n').map((line, i) => {
                        let color = '#d8dde2';
                        if (line.startsWith('---'))                color = '#7b76b6';
                        else if (line.match(/^[a-z-]+:/i))         color = '#5facbd';
                        else if (line.startsWith('#'))             color = '#eb9b6b';
                        else if (line.startsWith('- '))            color = '#a8dbcb';
                        else if (line.match(/`[^`]+`/))            color = '#d8dde2';
                        return (
                          <div key={i} style={{ color, whiteSpace: 'pre-wrap' }}>
                            {line || '\u00A0'}
                          </div>
                        );
                      })}
                    </div>
                  </div>
                )}

                {/* File tree (only on the first skill — others get a stub) */}
                {s.tree && (
                  <div>
                    <FieldLabel>Bundle</FieldLabel>
                    <SkillFileTree tree={s.tree} accent={agent.color}/>
                  </div>
                )}

                {/* Bottom meta */}
                <div style={{
                  display: 'flex', flexWrap: 'wrap', gap: '6px 18px',
                  fontSize: 11, color: 'var(--text-tertiary)',
                  borderTop: '1px solid var(--border-light)', paddingTop: 10,
                }}>
                  <span>installed <b style={{ color: 'var(--text-secondary)' }}>{s.installed}</b></span>
                  <span>last fired <b style={{ color: 'var(--text-secondary)' }}>{s.activation.lastFire}</b></span>
                  <span>visibility <b style={{ color: 'var(--text-secondary)' }}>{s.visibility}</b></span>
                </div>
              </div>
            </Card>
          </div>
        )}
      </div>
    </div>
  );
}

// ─── File tree (mini explorer for a skill bundle) ──────────────────
function SkillFileTree({ tree, accent }) {
  const Row = ({ entry, depth }) => {
    const [open, setOpen] = React.useState(true);
    const isFolder = entry.type === 'folder';
    const name = entry.path.split('/').pop();
    return (
      <>
        <div
          onClick={() => isFolder && setOpen(!open)}
          style={{
            display: 'grid', gridTemplateColumns: 'auto 1fr auto', gap: 8, alignItems: 'center',
            padding: '5px 10px', borderRadius: 4,
            paddingLeft: 10 + depth * 18,
            cursor: isFolder ? 'pointer' : 'default',
            fontSize: 11.5,
            color: isFolder ? 'var(--text-primary)' : 'var(--text-secondary)',
            fontFamily: 'var(--font-mono)',
          }}>
          <span style={{ display: 'inline-flex', color: isFolder ? accent : 'var(--text-tertiary)' }}>
            <Icon name={isFolder ? (open ? 'folderOpen' : 'folder') : (name === 'SKILL.md' ? 'markdown' : 'file')} size={12}/>
          </span>
          <span style={{ fontWeight: name === 'SKILL.md' ? 600 : 400 }}>{name}</span>
          {entry.size && <span style={{ fontSize: 10, color: 'var(--text-tertiary)' }}>{entry.size}</span>}
        </div>
        {isFolder && open && entry.children?.map(c => <Row key={c.path} entry={c} depth={depth + 1}/>)}
      </>
    );
  };
  return (
    <div style={{ background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '6px 0' }}>
      {tree.map(entry => <Row key={entry.path} entry={entry} depth={0}/>)}
    </div>
  );
}

// MCP tab
function TabMcp({ agent }) {
  const servers = [
    { name: 'aonik-ledger',     url: 'mcp://internal/aonik-ledger',      status: 'connected',  tools: 12, latency: '14ms', auth: 'mTLS', native: true,  resources: 4, lastSync: '12s ago' },
    { name: 'open-banking-uk',  url: 'mcp://partner/open-banking-uk',    status: 'connected',  tools:  8, latency: '188ms', auth: 'OAuth2',                 resources: 6, lastSync: '2m ago' },
    { name: 'companies-house',  url: 'mcp://partner/companies-house',    status: 'connected',  tools:  4, latency: '412ms', auth: 'API key',                resources: 2, lastSync: '8m ago' },
    { name: 'fx-quotes',        url: 'mcp://partner/fx-quotes-v2',       status: 'connecting', tools:  6, latency: '—',     auth: 'OAuth2',                 resources: 3, lastSync: 'in progress' },
    { name: 'sanctions-screen', url: 'mcp://partner/ofac-sanctions',     status: 'error',      tools:  3, latency: '—',     auth: 'mTLS',                   resources: 1, lastSync: 'failed 18m ago', err: 'TLS handshake failed · cert chain incomplete' },
  ];
  const stTone = { connected: '#1f7a5e', connecting: '#b4741e', error: '#c44536' };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      <Section
        title="MCP Servers"
        subtitle="Model Context Protocol servers this agent connects to. Each server exposes typed tools, resources, and prompt templates over a tenant-authenticated channel."
        count={servers.length}
        action={<>
          <button className="btn btn-ghost btn-sm"><Icon name="search" size={11}/> Browse marketplace</button>
          <button className="btn btn-outline btn-sm"><Icon name="plus" size={11}/> Connect server</button>
        </>}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          {servers.map(s => (
            <div key={s.name} style={{
              background: 'var(--surface)', border: `1px solid ${s.status === 'error' ? '#c4453633' : 'var(--border-light)'}`,
              borderRadius: 12, overflow: 'hidden',
            }}>
              <div style={{
                display: 'grid', gridTemplateColumns: '40px 1fr 100px 100px 100px 100px',
                gap: 14, alignItems: 'center', padding: '14px 16px',
              }}>
                <div style={{
                  width: 36, height: 36, borderRadius: 8,
                  background: 'var(--surface-inset)', position: 'relative',
                  display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                }}>
                  <Icon name="server" size={15} color="var(--text-secondary)"/>
                  <span style={{
                    position: 'absolute', bottom: -2, right: -2,
                    width: 10, height: 10, borderRadius: 999,
                    background: stTone[s.status], border: '2px solid var(--surface)',
                    ...(s.status === 'connecting' ? { animation: 'agt-pulse 1.4s ease-out infinite' } : {}),
                  }}/>
                </div>
                <div style={{ minWidth: 0 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <span style={{ fontSize: 13.5, fontWeight: 600, color: 'var(--text-primary)' }}>{s.name}</span>
                    {s.native && <span style={{ fontSize: 9.5, padding: '1px 6px', borderRadius: 3, background: 'var(--brand-primary-10)', color: 'var(--brand-primary)', fontWeight: 600, letterSpacing: '0.04em' }}>NATIVE</span>}
                    <span style={{ fontSize: 9.5, padding: '1px 6px', borderRadius: 3, background: 'var(--surface-inset)', color: 'var(--text-tertiary)', fontWeight: 600, letterSpacing: '0.04em', fontFamily: 'var(--font-mono)' }}>{s.auth}</span>
                  </div>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 2 }}>{s.url}</div>
                </div>
                <div style={{ fontSize: 11, color: 'var(--text-secondary)', textAlign: 'left' }}>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-primary)' }}>{s.tools}</div>
                  <div style={{ fontSize: 10, color: 'var(--text-tertiary)', marginTop: 1 }}>tools</div>
                </div>
                <div style={{ fontSize: 11, color: 'var(--text-secondary)' }}>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-primary)' }}>{s.resources}</div>
                  <div style={{ fontSize: 10, color: 'var(--text-tertiary)', marginTop: 1 }}>resources</div>
                </div>
                <div style={{ fontSize: 11, color: 'var(--text-secondary)' }}>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-primary)' }}>{s.latency}</div>
                  <div style={{ fontSize: 10, color: 'var(--text-tertiary)', marginTop: 1 }}>latency</div>
                </div>
                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 6 }}>
                  <button className="btn btn-ghost btn-sm" style={{ height: 26, padding: '0 8px', fontSize: 11 }}><Icon name="refresh" size={11}/></button>
                  <button className="btn btn-outline btn-sm" style={{ height: 26, padding: '0 10px', fontSize: 11 }}>Manage</button>
                </div>
              </div>
              {s.err && (
                <div style={{ padding: '10px 16px', background: '#c4453608', borderTop: '1px solid #c4453633', fontSize: 11.5, color: '#c44536', display: 'flex', alignItems: 'center', gap: 8 }}>
                  <Icon name="warning" size={12} color="#c44536"/>
                  {s.err}
                  <div style={{ flex: 1 }}/>
                  <button className="btn btn-ghost btn-sm" style={{ height: 22, fontSize: 11, color: '#c44536' }}>Retry</button>
                  <button className="btn btn-ghost btn-sm" style={{ height: 22, fontSize: 11 }}>View logs</button>
                </div>
              )}
              <div style={{ padding: '8px 16px', background: 'var(--surface-inset)', borderTop: '1px solid var(--border-light)', display: 'flex', alignItems: 'center', gap: 12, fontSize: 11, color: 'var(--text-tertiary)' }}>
                <span>last sync · <b style={{ color: 'var(--text-secondary)' }}>{s.lastSync}</b></span>
                <span>·</span>
                <span style={{ fontFamily: 'var(--font-mono)' }}>v1.4.2</span>
              </div>
            </div>
          ))}
        </div>
      </Section>
    </div>
  );
}

// Activity tab — runs timeline + log stream
function TabActivity({ agent }) {
  const runs = [
    { id: 'run_5af2', op: 'match_and_apply',  status: 'ok',    dur: '3.14s', t: 'now',  txn: 'INV-2041', tool: 12, sub: 1 },
    { id: 'run_5ae1', op: 'apply_invoice',    status: 'held',  dur: '0.84s', t: '11m',  txn: 'INV-2038', tool: 8,  sub: 0 },
    { id: 'run_5ad8', op: 'match_and_apply',  status: 'ok',    dur: '2.94s', t: '24m',  txn: 'INV-2037', tool: 11, sub: 1 },
    { id: 'run_5ac2', op: 'summarize_ar',     status: 'ok',    dur: '1.94s', t: '1h',   txn: '—',        tool: 4,  sub: 0 },
    { id: 'run_5a9e', op: 'dunning_send',     status: 'ok',    dur: '0.42s', t: '2h',   txn: 'INV-2014', tool: 3,  sub: 1 },
    { id: 'run_5a72', op: 'reconcile_fx',     status: 'err',   dur: '4.21s', t: '3h',   txn: 'INV-2009', tool: 14, sub: 2 },
    { id: 'run_5a40', op: 'match_and_apply',  status: 'ok',    dur: '1.78s', t: '4h',   txn: 'INV-2002', tool: 9,  sub: 1 },
  ];
  const tone = { ok: '#1f7a5e', held: '#b4741e', err: '#c44536' };
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '1fr 380px', gap: 24 }}>
      <Section
        title="Recent runs"
        subtitle="Each row is a complete agent invocation. Click to open the trace."
        count={`318 / 24h`}
        action={<>
          <button className="btn btn-ghost btn-sm"><Icon name="filter" size={11}/> Status</button>
          <button className="btn btn-ghost btn-sm"><Icon name="calendar" size={11}/> 24h</button>
          <button className="btn btn-outline btn-sm">Export CSV</button>
        </>}>
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
          <div style={{
            display: 'grid', gridTemplateColumns: '50px 100px 1fr 90px 70px 60px 60px',
            gap: 14, padding: '10px 16px',
            background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)',
            fontSize: 10, fontWeight: 600, letterSpacing: '0.06em', textTransform: 'uppercase', color: 'var(--text-tertiary)',
          }}>
            <div/>
            <div>Run</div>
            <div>Operation</div>
            <div>Subject</div>
            <div style={{ textAlign: 'right' }}>Tools</div>
            <div style={{ textAlign: 'right' }}>Dur</div>
            <div style={{ textAlign: 'right' }}>Age</div>
          </div>
          {runs.map((r, i) => (
            <div key={r.id} style={{
              display: 'grid', gridTemplateColumns: '50px 100px 1fr 90px 70px 60px 60px',
              gap: 14, padding: '12px 16px', alignItems: 'center', cursor: 'pointer',
              borderBottom: i < runs.length - 1 ? '1px solid var(--border-light)' : 'none',
            }}>
              <span style={{
                fontFamily: 'var(--font-mono)', fontSize: 9.5, fontWeight: 600,
                padding: '2px 7px', borderRadius: 3, textAlign: 'center',
                background: tone[r.status] + '1a', color: tone[r.status],
                textTransform: 'uppercase',
              }}>{r.status}</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)' }}>{r.id}</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12.5, color: 'var(--text-primary)' }}>{r.op}</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-secondary)' }}>{r.txn}</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)', textAlign: 'right' }}>{r.tool}{r.sub > 0 && <span style={{ color: agent.color }}> +{r.sub}sub</span>}</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)', textAlign: 'right' }}>{r.dur}</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)', textAlign: 'right' }}>{r.t}</span>
            </div>
          ))}
        </div>
      </Section>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
        <Card title="Live log stream" eyebrow="streaming · last 30s">
          <div style={{ padding: 12, fontFamily: 'var(--font-mono)', fontSize: 10.5, lineHeight: 1.7, background: '#0e1620', color: '#d4dbe5', maxHeight: 360, overflow: 'auto' }}>
            {[
              { t: '12:04:18.214', l: 'I', m: 'agent.start  run=run_5af2 trace=tr_8c12' },
              { t: '12:04:18.301', l: 'I', m: 'tool.call    list_bank_transactions(window=72h)' },
              { t: '12:04:18.519', l: 'I', m: 'tool.return  rows=18' },
              { t: '12:04:18.520', l: 'I', m: 'tool.call    match_invoice_to_txn(invoice=INV-2041, txn=*)' },
              { t: '12:04:18.731', l: 'I', m: 'tool.return  best_score=0.97 candidate=BNK-7741' },
              { t: '12:04:18.732', l: 'I', m: 'tool.call    draft_journal_entry(...)' },
              { t: '12:04:18.820', l: 'I', m: 'sub.call     ledger.apply_journal_entry' },
              { t: '12:04:19.054', l: 'I', m: 'sub.return   posted=true ref=GL-44021' },
              { t: '12:04:19.055', l: 'I', m: 'tool.call    display_proposal_card(...)' },
              { t: '12:04:19.063', l: 'I', m: 'agent.end    status=ok dur=3.14s tools=12 sub=1' },
            ].map((row, i) => (
              <div key={i} style={{ display: 'grid', gridTemplateColumns: '90px 14px 1fr', gap: 8 }}>
                <span style={{ color: '#6b7a8c' }}>{row.t}</span>
                <span style={{ color: row.l === 'E' ? '#ff8175' : '#5b9dd6' }}>{row.l}</span>
                <span>{row.m}</span>
              </div>
            ))}
          </div>
        </Card>
      </div>
    </div>
  );
}

// Settings tab
function TabSettings({ agent }) {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '1fr 360px', gap: 24 }}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
        <Card title="General">
          <div style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 14 }}>
            <SettingRow l="Status" sub="Pause this agent globally — it won't run, but its config stays put.">
              <Pill tone="success" dot size="sm">Running</Pill>
            </SettingRow>
            <Divider/>
            <SettingRow l="Auto-apply" sub="Skip the proposal step when confidence ≥ threshold and amount &lt; ceiling.">
              <Toggle on={agent.autoApply}/>
            </SettingRow>
            <Divider/>
            <SettingRow l="Confidence threshold" sub="Below this, the agent always asks for human review.">
              <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 600 }}>0.95</span>
            </SettingRow>
            <Divider/>
            <SettingRow l="Amount ceiling" sub="Hard cap above which dual-approval is mandatory.">
              <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 600 }}>£50,000</span>
            </SettingRow>
          </div>
        </Card>
        <Card title="Routing">
          <div style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 14 }}>
            <SettingRow l="Inbox" sub="Where unresolved proposals land for human review.">
              <span style={{ fontSize: 12, fontWeight: 600 }}>Treasury inbox</span>
            </SettingRow>
            <Divider/>
            <SettingRow l="Approver group" sub="Two of these must sign off above the ceiling.">
              <span style={{ fontSize: 12, fontWeight: 600 }}>Finance · 4 members</span>
            </SettingRow>
            <Divider/>
            <SettingRow l="Notification" sub="Slack channel pinged for held items.">
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12 }}>#fin-ops-alerts</span>
            </SettingRow>
          </div>
        </Card>
        <Card title="Danger zone">
          <div style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 12 }}>
            <DangerRow t="Reset memory" d="Forget conversation memory across all users. Cannot be undone." cta="Reset"/>
            <DangerRow t="Disable agent" d="Stop running and hide from new chats. Existing traces remain." cta="Disable"/>
            <DangerRow t="Delete agent" d="Permanently remove this agent and all its skills, schedules and triggers." cta="Delete" destructive/>
          </div>
        </Card>
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
        <Card title="Versioning" eyebrow="v0.42.1 · deployed 12d">
          <div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 8 }}>
            {[
              { v: 'v0.42.1', t: '12 days ago',  by: 'maria', n: 'Tightened dunning cadence',     active: true },
              { v: 'v0.42.0', t: '18 days ago',  by: 'maria', n: 'Added FX reconciliation skill' },
              { v: 'v0.41.4', t: '1 month ago',  by: 'aaron', n: 'Bumped match threshold' },
              { v: 'v0.41.0', t: '2 months ago', by: 'maria', n: 'Initial release' },
            ].map((v, i) => (
              <div key={i} style={{
                display: 'grid', gridTemplateColumns: '70px 1fr auto',
                gap: 10, padding: '10px 12px',
                background: v.active ? agent.color + '0a' : 'var(--surface-inset)',
                border: '1px solid ' + (v.active ? agent.color + '44' : 'var(--border-light)'),
                borderRadius: 8,
              }}>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, fontWeight: 600, color: 'var(--text-primary)' }}>{v.v}</span>
                <div style={{ minWidth: 0 }}>
                  <div style={{ fontSize: 11.5, color: 'var(--text-primary)' }}>{v.n}</div>
                  <div style={{ fontSize: 10, color: 'var(--text-tertiary)', marginTop: 1 }}>{v.t} · @{v.by}</div>
                </div>
                <button className="btn btn-ghost btn-sm" style={{ height: 22, fontSize: 10.5 }}>{v.active ? 'live' : 'roll back'}</button>
              </div>
            ))}
          </div>
        </Card>
      </div>
    </div>
  );
}

// ─── Tiny support primitives ────────────────────────────────────────
function MiniStat({ l, v, tone }) {
  return (
    <div style={{ background: 'var(--surface-inset)', borderRadius: 8, padding: '8px 10px' }}>
      <div style={{ fontSize: 9.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase' }}>{l}</div>
      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 13, fontWeight: 600, color: tone || 'var(--text-primary)', marginTop: 2 }}>{v}</div>
    </div>
  );
}
function FieldLabel({ children }) {
  return <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 8 }}>{children}</div>;
}
function SettingRow({ l, sub, children }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
      <div style={{ flex: 1 }}>
        <div style={{ fontSize: 12.5, fontWeight: 500, color: 'var(--text-primary)' }}>{l}</div>
        {sub && <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{sub}</div>}
      </div>
      {children}
    </div>
  );
}
function Toggle({ on }) {
  return (
    <span style={{
      width: 30, height: 17, borderRadius: 999, padding: 2,
      background: on ? 'var(--brand-primary)' : 'var(--gray-300, #cbd1d8)',
      display: 'inline-flex', alignItems: 'center', cursor: 'pointer', flex: 'none',
    }}>
      <span style={{
        width: 13, height: 13, borderRadius: 999, background: '#fff',
        transform: on ? 'translateX(13px)' : 'translateX(0)',
        transition: 'transform 150ms',
      }}/>
    </span>
  );
}
function Divider() {
  return <div style={{ height: 1, background: 'var(--border-light)' }}/>;
}
function DangerRow({ t, d, cta, destructive }) {
  return (
    <div style={{
      display: 'grid', gridTemplateColumns: '1fr auto', gap: 14, alignItems: 'center',
      padding: 12, border: '1px solid ' + (destructive ? '#c4453633' : 'var(--border-light)'),
      borderRadius: 8,
    }}>
      <div>
        <div style={{ fontSize: 12.5, fontWeight: 500, color: destructive ? '#c44536' : 'var(--text-primary)' }}>{t}</div>
        <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{d}</div>
      </div>
      <button className="btn btn-outline btn-sm" style={destructive ? { color: '#c44536', borderColor: '#c4453666' } : null}>{cta}</button>
    </div>
  );
}

Object.assign(window, { ScreenAgentDetail });
