// AI Chat — slide-out drawer + full-page mock matching the codebase
// Mirrors AiChatPanel.tsx (slide-out) and AiChatMock.tsx (full page)

const AGENTS = [
  { id: 'orch',       name: 'AONIK Orchestrator', desc: 'Routes to all domain agents', initial: 'A', color: '#055a60' },
  { id: 'billing',    name: 'Billing Agent',      desc: 'Invoices · matching · dunning', initial: 'B', color: '#eb5c37' },
  { id: 'ledger',     name: 'Ledger Agent',       desc: 'Journals · close · reconciliation', initial: 'L', color: '#3ab795' },
  { id: 'fx',         name: 'FX Agent',           desc: 'Cross-border · hedging · rates', initial: 'F', color: '#7b76b6' },
  { id: 'compliance', name: 'Compliance Agent',   desc: 'KYC/KYB · sanctions · audits', initial: 'C', color: '#0097a9' },
  { id: 'insights',   name: 'Insights Agent',     desc: 'Budgets · forecasts · variance', initial: 'I', color: '#5facbd' },
];

// ───────────── Agent picker dropdown ─────────────
function AgentPicker({ value, onChange, anchor = 'bottom-left', dark = false }) {
  const [open, setOpen] = React.useState(false);
  const ref = React.useRef(null);
  const sel = AGENTS.find(a => a.id === value) || AGENTS[0];

  React.useEffect(() => {
    if (!open) return;
    const onDoc = (e) => { if (ref.current && !ref.current.contains(e.target)) setOpen(false); };
    document.addEventListener('mousedown', onDoc);
    return () => document.removeEventListener('mousedown', onDoc);
  }, [open]);

  const triggerStyle = dark ? {
    background: 'rgba(255,255,255,0.12)', color: '#fff', border: '1px solid rgba(255,255,255,0.2)',
  } : {
    background: 'var(--surface)', color: 'var(--text-primary)', border: '1px solid var(--border-light)',
  };

  return (
    <div ref={ref} style={{ position: 'relative' }}>
      <button onClick={() => setOpen(o => !o)} style={{
        ...triggerStyle, display: 'inline-flex', alignItems: 'center', gap: 8,
        padding: '5px 10px 5px 6px', borderRadius: 8, fontSize: 12.5,
        cursor: 'pointer', fontFamily: 'inherit', fontWeight: 500,
      }}>
        <span style={{
          width: 22, height: 22, borderRadius: '50%', background: sel.color, color: '#fff',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          fontSize: 11, fontWeight: 700,
        }}>{sel.initial}</span>
        <span style={{ maxWidth: 180, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{sel.name}</span>
        <Icon name="chevron" size={12} color={dark ? '#fff' : 'var(--text-secondary)'}/>
      </button>
      {open && (
        <div style={{
          position: 'absolute', zIndex: 50, marginTop: 6,
          [anchor.includes('right') ? 'right' : 'left']: 0,
          width: 300, background: 'var(--surface)', border: '1px solid var(--border-light)',
          borderRadius: 10, padding: 6,
          boxShadow: '0 12px 32px -8px rgb(0 0 0 / 0.18), 0 0 0 1px rgb(0 0 0 / 0.02)',
        }}>
          <div style={{ padding: '6px 10px 8px', borderBottom: '1px solid var(--border-light)', marginBottom: 4,
            fontSize: 10, letterSpacing: '0.08em', textTransform: 'uppercase',
            color: 'var(--text-tertiary)', fontWeight: 600 }}>Switch agent</div>
          {AGENTS.map(a => {
            const active = a.id === value;
            return (
              <div key={a.id} onClick={() => { onChange?.(a.id); setOpen(false); }}
                style={{
                  display: 'flex', alignItems: 'center', gap: 10,
                  padding: '8px 10px', borderRadius: 6, cursor: 'pointer',
                  background: active ? 'var(--brand-primary-10)' : 'transparent',
                }}>
                <span style={{
                  width: 28, height: 28, borderRadius: '50%', background: a.color, color: '#fff',
                  display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                  fontSize: 12, fontWeight: 700, flex: 'none',
                }}>{a.initial}</span>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: 12.5, fontWeight: active ? 600 : 500, color: 'var(--text-primary)' }}>{a.name}</div>
                  <div style={{ fontSize: 11, color: 'var(--text-secondary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{a.desc}</div>
                </div>
                {active && <Icon name="check" size={13} color="var(--brand-primary)"/>}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

// ───────────── Slide-out drawer (AI Chat panel) ─────────────
function ScreenAiChatSlideout() {
  const [agent, setAgent] = React.useState('orch');
  const sel = AGENTS.find(a => a.id === agent);

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      {/* Backdrop: faux page underneath */}
      <FauxPagePlayground/>

      {/* Slide-out panel pinned to right edge */}
      <aside style={{
        position: 'absolute', top: 0, right: 0, bottom: 0,
        width: 480, background: 'var(--surface)',
        borderLeft: '1px solid var(--border-light)',
        boxShadow: '-12px 0 32px -8px rgb(0 0 0 / 0.18)',
        display: 'flex', flexDirection: 'column',
      }}>
        {/* Header (teal, matches AiChatPanel.tsx) */}
        <div style={{
          height: 50, padding: '0 14px',
          background: 'var(--brand-primary)', color: '#fff',
          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
          flex: 'none',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <div style={{ width: 28, height: 28, background: 'rgba(255,255,255,0.15)', borderRadius: 4,
              display: 'grid', placeItems: 'center', fontSize: 12, fontWeight: 700 }}>A</div>
            <span style={{ fontSize: 14, fontWeight: 600 }}>AONIK AI</span>
            <span style={{ height: 18, width: 1, background: 'rgba(255,255,255,0.25)', margin: '0 4px' }}/>
            <AgentPicker value={agent} onChange={setAgent} dark/>
          </div>
          <div style={{ display: 'flex', gap: 4 }}>
            <span style={{ width: 28, height: 28, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', borderRadius: 4, cursor: 'pointer' }}>
              <Icon name="arrowright" size={14} color="#fff"/>
            </span>
            <span style={{ width: 28, height: 28, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', borderRadius: 4, cursor: 'pointer' }}>
              <Icon name="x" size={14} color="#fff"/>
            </span>
          </div>
        </div>

        {/* Conversation */}
        <div style={{ flex: 1, overflow: 'auto', background: 'var(--surface-inset)', padding: 14, display: 'flex', flexDirection: 'column', gap: 10 }}>
          <DrawerMsg who="user">Where is April spend going? Anything to flag?</DrawerMsg>
          <DrawerMsg who="agent" agent={sel}>
            Looking at the categories now — fuel is trending high. Pulling the breakdown.
          </DrawerMsg>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6, paddingLeft: 38 }}>
            <ToolCallBadge name="get_budget" ms="84ms"/>
            <ToolCallBadge name="aggregate_spending_by_category" ms="142ms"/>
          </div>
          <DrawerMsg who="agent" agent={sel}>
            Fuel is at <b>107%</b> of plan with 8 days left. Warehousing on track. Want me to draft a budget alert?
          </DrawerMsg>
          <div style={{ paddingLeft: 38 }}>
            <ToolCardPie
              title="April spend · GBP" totalSpent={42180} currency="GBP"
              categories={[
                { name: 'Fuel · fleet', amount: 19200, percentage: 46 },
                { name: 'Warehousing',   amount: 10100, percentage: 24 },
                { name: 'Contractors',   amount:  8420, percentage: 20 },
                { name: 'Insurance',     amount:  3960, percentage:  9 },
                { name: 'Other',         amount:   500, percentage:  1 },
              ]}/>
          </div>
          <div style={{ paddingLeft: 38 }}>
            <ToolCardConfirm
              action="Schedule fuel alert at £18K"
              description="Notify Maria when fuel category reaches 95% of next month's budget."
              severity="low"/>
          </div>
        </div>

        {/* Composer */}
        <div style={{ padding: 12, borderTop: '1px solid var(--border-light)', background: 'var(--surface)', flex: 'none' }}>
          <div style={{ background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '10px 12px' }}>
            <textarea rows={2} placeholder="Ask me anything…"
              style={{ width: '100%', border: 'none', background: 'transparent', outline: 'none', resize: 'none', fontSize: 13, fontFamily: 'inherit', color: 'var(--text-primary)', padding: 0 }}/>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 4 }}>
              <div style={{ display: 'flex', gap: 4 }}>
                <span style={{ width: 26, height: 26, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', borderRadius: 4, cursor: 'pointer' }}>
                  <Icon name="link" size={13} color="var(--text-tertiary)"/>
                </span>
                <span style={{ width: 26, height: 26, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', borderRadius: 4, cursor: 'pointer' }}>
                  <Icon name="globe" size={13} color="var(--text-tertiary)"/>
                </span>
              </div>
              <button className="btn btn-primary btn-sm"><Icon name="send" size={12}/> Send</button>
            </div>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 8, fontSize: 11, color: 'var(--text-tertiary)' }}>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}>
              <span style={{ width: 6, height: 6, borderRadius: '50%', background: 'var(--success, #1f7a5e)' }}/>
              AG-UI connected
            </span>
            <span>Agent: {sel.name}</span>
          </div>
        </div>
      </aside>
    </div>
  );
}

function DrawerMsg({ who, agent, children }) {
  const isUser = who === 'user';
  return (
    <div style={{ display: 'flex', gap: 10, flexDirection: isUser ? 'row-reverse' : 'row', alignItems: 'flex-start' }}>
      {isUser
        ? <Avatar name="Oliver" size={28} color="#7b76b6" textColor="#fff"/>
        : <span style={{ width: 28, height: 28, borderRadius: '50%', background: agent?.color || 'var(--brand-primary)', color: '#fff',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontSize: 12, fontWeight: 700, flex: 'none' }}>{agent?.initial || 'A'}</span>}
      <div style={{
        maxWidth: 360, padding: '10px 14px', fontSize: 13, lineHeight: 1.5,
        background: isUser ? 'var(--brand-primary)' : 'var(--surface)',
        color: isUser ? '#fff' : 'var(--text-primary)',
        border: isUser ? 'none' : '1px solid var(--border-light)',
        borderRadius: isUser ? '12px 12px 4px 12px' : '12px 12px 12px 4px',
      }}>{children}</div>
    </div>
  );
}

// Faux page that the drawer covers — gives the slide-out spatial context
function FauxPagePlayground() {
  return (
    <div style={{ position: 'absolute', inset: 0, padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 16, opacity: 0.55, pointerEvents: 'none' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end' }}>
        <div>
          <div style={{ fontSize: 11, letterSpacing: '0.08em', textTransform: 'uppercase', color: 'var(--text-tertiary)' }}>My Space</div>
          <div style={{ fontSize: 24, fontWeight: 600, color: 'var(--text-primary)', marginTop: 4 }}>Good afternoon, Oliver.</div>
          <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>Here's what's open across your finance ops today.</div>
        </div>
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
        {['Cash', 'Receivables', 'Payables', 'Net runway'].map((k, i) => (
          <div key={k} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: 16, height: 96 }}>
            <div style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{k}</div>
          </div>
        ))}
      </div>
      <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, height: 280 }}/>
    </div>
  );
}

// ───────────── Full-page chat ─────────────
function ScreenAiChatFullPage() {
  const [agent, setAgent] = React.useState('orch');
  const [activeThread, setActiveThread] = React.useState('t-1');
  const [showEmpty, setShowEmpty] = React.useState(false);
  const sel = AGENTS.find(a => a.id === agent);

  const threads = [
    { id: 't-1', title: 'April spend variance review',     when: 'Today',     active: true },
    { id: 't-2', title: 'Match Wise inbound to INV-2058',   when: 'Today' },
    { id: 't-3', title: 'Hedging strategy for NGN exposure', when: 'Yesterday' },
    { id: 't-4', title: 'Q1 close checklist',                when: '2 days ago' },
    { id: 't-5', title: 'New customer KYB — Northstar',      when: '3 days ago' },
    { id: 't-6', title: 'Fuel category overrun — fleet',     when: '5 days ago' },
    { id: 't-7', title: 'Reconcile Zenith Bank feed',        when: '1 week ago' },
    { id: 't-8', title: 'Draft budget alert thresholds',     when: '1 week ago' },
  ];

  return (
    <div style={{ display: 'grid', gridTemplateColumns: '280px 1fr', height: '100%' }}>
      {/* Threads sidebar */}
      <aside style={{
        borderRight: '1px solid var(--border-light)',
        background: 'var(--surface-inset)',
        display: 'flex', flexDirection: 'column', minHeight: 0,
      }}>
        <div style={{ height: 50, padding: '0 16px', display: 'flex', alignItems: 'center', borderBottom: '1px solid var(--border-light)' }}>
          <div style={{ width: 26, height: 26, borderRadius: 4, background: 'var(--surface)', border: '1px solid var(--border-light)',
            display: 'grid', placeItems: 'center', fontSize: 11, fontWeight: 700, color: 'var(--text-primary)' }}>A</div>
          <div style={{ marginLeft: 10, fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>AONIK AI</div>
          <div style={{ marginLeft: 'auto', display: 'flex', gap: 4 }}>
            <span className="hover-halo"><Icon name="more" size={14} color="var(--text-tertiary)"/></span>
          </div>
        </div>

        <div style={{ padding: 14 }}>
          <button onClick={() => setShowEmpty(true)} style={{
            width: '100%', height: 38, display: 'flex', alignItems: 'center', gap: 8,
            padding: '0 12px', borderRadius: 6, fontSize: 13,
            background: 'var(--surface)', border: '1px solid var(--border-light)',
            color: 'var(--text-primary)', cursor: 'pointer',
          }}>
            <Icon name="plus" size={14} color="var(--text-secondary)"/> New chat
          </button>

          <div style={{ position: 'relative', marginTop: 12 }}>
            <span style={{ position: 'absolute', left: 10, top: 11 }}><Icon name="search" size={13} color="var(--text-tertiary)"/></span>
            <input className="input" placeholder="Search chats" style={{ paddingLeft: 30, height: 36, fontSize: 12.5, width: '100%' }}/>
          </div>

          <div style={{ marginTop: 18 }}>
            <div style={{ fontSize: 10, letterSpacing: '0.1em', fontWeight: 600, color: 'var(--text-tertiary)', textTransform: 'uppercase', marginBottom: 6, padding: '0 4px' }}>Chats</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
              {threads.map(t => {
                const active = !showEmpty && activeThread === t.id;
                return (
                  <div key={t.id}
                    onClick={() => { setActiveThread(t.id); setShowEmpty(false); }}
                    style={{
                      display: 'flex', alignItems: 'center', gap: 8,
                      padding: '8px 10px', borderRadius: 6, cursor: 'pointer',
                      background: active ? 'var(--brand-primary-10)' : 'transparent',
                    }}>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{
                        fontSize: 12.5, fontWeight: active ? 600 : 500,
                        color: active ? 'var(--text-primary)' : 'var(--text-secondary)',
                        overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
                      }}>{t.title}</div>
                      <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 1 }}>{t.when}</div>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </div>
      </aside>

      {/* Chat surface */}
      <section style={{ display: 'flex', flexDirection: 'column', minWidth: 0, minHeight: 0, background: 'var(--surface)' }}>
        {/* Topbar */}
        <div style={{ height: 50, padding: '0 16px', display: 'flex', alignItems: 'center', borderBottom: '1px solid var(--border-light)' }}>
          <span className="hover-halo" style={{ marginRight: 6 }}><Icon name="plus" size={14} color="var(--text-secondary)"/></span>
          <AgentPicker value={agent} onChange={setAgent}/>
          <div style={{ flex: 1, textAlign: 'center', fontSize: 15, fontWeight: 600, color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', padding: '0 16px' }}>
            {showEmpty ? 'New conversation' : threads.find(t => t.id === activeThread)?.title}
          </div>
          <div style={{ display: 'flex', gap: 4 }}>
            <span className="hover-halo"><Icon name="star" size={13} color="var(--text-secondary)"/></span>
            <span className="hover-halo"><Icon name="download" size={13} color="var(--text-secondary)"/></span>
            <span className="hover-halo"><Icon name="more" size={14} color="var(--text-secondary)"/></span>
          </div>
        </div>

        {/* Body */}
        {showEmpty
          ? <FullPageEmpty agent={sel}/>
          : <FullPageThread agent={sel}/>}

        {/* Composer */}
        {!showEmpty && (
          <div style={{ borderTop: '1px solid var(--border-light)', background: 'var(--surface)', padding: '14px 0' }}>
            <div style={{ maxWidth: 880, margin: '0 auto', padding: '0 24px' }}>
              <FullChatComposer/>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 10, fontSize: 11, color: 'var(--text-tertiary)' }}>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}>
                  <span style={{ width: 6, height: 6, borderRadius: '50%', background: 'var(--success, #1f7a5e)' }}/>
                  Connected via AG-UI protocol
                </span>
                <span>Agent: {sel.name}</span>
              </div>
            </div>
          </div>
        )}
      </section>
    </div>
  );
}

function FullPageEmpty({ agent }) {
  const prompts = [
    { icon: 'sparkles', text: 'Summarize the latest platform activity and flag anything that needs attention.' },
    { icon: 'book',     text: 'Find the most useful dashboards, agents, and docs for my current workspace.' },
    { icon: 'zap',      text: 'Help me plan the next steps for billing, collections, and reconciliation.' },
  ];
  return (
    <div style={{ flex: 1, overflow: 'auto', display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 48 }}>
      <div style={{ width: '100%', maxWidth: 820, display: 'flex', flexDirection: 'column', gap: 28, alignItems: 'center' }}>
        <div style={{ textAlign: 'center' }}>
          <div style={{ fontSize: 28, fontWeight: 600, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>
            Good afternoon, Oliver.
          </div>
          <div style={{ fontSize: 14, color: 'var(--text-secondary)', marginTop: 6, maxWidth: 540, marginLeft: 'auto', marginRight: 'auto' }}>
            Ask anything about your workspace, agents, data products, or platform operations.
          </div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 14, width: '100%', maxWidth: 720 }}>
          {prompts.map((p, i) => (
            <div key={i} style={{
              background: 'var(--surface-inset)', borderRadius: 10, padding: 14,
              border: '1px solid var(--border-light)',
              minHeight: 160, display: 'flex', flexDirection: 'column', gap: 14,
              cursor: 'pointer',
            }}>
              <div style={{
                width: 40, height: 40, borderRadius: '50%',
                background: 'var(--surface)', border: '1px solid var(--border-light)',
                display: 'grid', placeItems: 'center',
              }}>
                <Icon name={p.icon} size={16} color="var(--text-secondary)"/>
              </div>
              <div style={{ fontSize: 14, color: 'var(--text-primary)', lineHeight: 1.45 }}>{p.text}</div>
            </div>
          ))}
        </div>

        <div style={{ width: '100%', maxWidth: 720 }}>
          <FullChatComposer center/>
        </div>

        <div style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 12, color: 'var(--text-secondary)', cursor: 'pointer' }}>
          <Icon name="search" size={12}/> Browse prompts
        </div>
      </div>
    </div>
  );
}

function FullPageThread({ agent }) {
  return (
    <div style={{ flex: 1, overflow: 'auto', padding: '24px 32px', display: 'flex', justifyContent: 'center', minHeight: 0, background: 'var(--background)' }}>
      <div style={{ width: '100%', maxWidth: 880, display: 'flex', flexDirection: 'column', gap: 16 }}>
        <DrawerMsg who="user">Walk me through April spend variance — anything I should approve or push back on?</DrawerMsg>

        <DrawerMsg who="agent" agent={agent}>
          Pulling the budget vs actuals now. Three categories are off plan.
        </DrawerMsg>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6, paddingLeft: 38 }}>
          <ToolCallBadge name="get_budget"                  ms="84ms"/>
          <ToolCallBadge name="aggregate_spending_by_category" ms="142ms"/>
          <ToolCallBadge name="forecast_month_end"           ms="318ms"/>
        </div>

        <DrawerMsg who="agent" agent={agent}>
          Fuel will land ~12% over plan — tracking pace is unsustainable for the rest of the month. Warehousing on track. Contractors trending under by 40%. Want a fuel cap or just an alert?
        </DrawerMsg>

        <div style={{ paddingLeft: 38, display: 'flex', flexDirection: 'column', gap: 12 }}>
          <ToolCardBudget
            period="April 2026"
            totalBudget={60000} totalSpent={42180} currency="GBP"
            categories={[
              { name: 'Fuel · fleet',    budgeted: 18000, spent: 19200, status: 'over' },
              { name: 'Contractors',     budgeted: 14000, spent:  8420, status: 'under' },
              { name: 'Warehousing',     budgeted: 12000, spent: 10100, status: 'on_track' },
              { name: 'Insurance',       budgeted:  8000, spent:  3960, status: 'under' },
              { name: 'Software',        budgeted:  4000, spent:   500, status: 'under' },
            ]}/>
          <ToolCardOptions
            question="Which response should I prepare?"
            options={[
              { label: 'Hard fuel cap at £18K', description: 'Block PO submissions above the line until next cycle' },
              { label: 'Soft alert at 95%',     description: 'Notify Maria — no automatic restriction' },
              { label: 'Reforecast & approve',  description: 'Bump April fuel budget by £4K and document rationale' },
            ]}/>
        </div>

        <DrawerMsg who="user">Soft alert at 95%. Schedule it for next month too.</DrawerMsg>

        <DrawerMsg who="agent" agent={agent}>
          Done. I'll set up the alert and add it to your monthly cadence. Confirm before I write?
        </DrawerMsg>
        <div style={{ paddingLeft: 38 }}>
          <ToolCardConfirm
            action="Schedule fuel alert at 95% threshold"
            description="Notify Maria when fuel spend reaches 95% of monthly budget. Active May 2026 onward."
            severity="low"/>
        </div>
      </div>
    </div>
  );
}

function FullChatComposer({ center }) {
  return (
    <div style={{
      background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 14,
      padding: '12px 14px',
      boxShadow: center ? '0 4px 18px -4px rgb(0 0 0 / 0.06)' : 'none',
    }}>
      <textarea rows={center ? 3 : 2} placeholder="Ask anything…"
        style={{ width: '100%', border: 'none', background: 'transparent', outline: 'none', resize: 'none', fontSize: 14, fontFamily: 'inherit', color: 'var(--text-primary)', padding: 0 }}/>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 4 }}>
        <div style={{ display: 'flex', gap: 4 }}>
          <button className="btn btn-ghost btn-sm"><Icon name="link" size={12}/> Attach</button>
          <button className="btn btn-ghost btn-sm"><Icon name="layers" size={12}/> Tools</button>
          <button className="btn btn-ghost btn-sm"><Icon name="globe" size={12}/> Voice</button>
        </div>
        <button className="btn btn-primary btn-sm"><Icon name="send" size={12}/> Send</button>
      </div>
    </div>
  );
}

Object.assign(window, { ScreenAiChatSlideout, ScreenAiChatFullPage, AgentPicker });
