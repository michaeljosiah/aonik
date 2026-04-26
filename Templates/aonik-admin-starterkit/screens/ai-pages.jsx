// Frontend Tool Cards showcase + AI pages (Playground, Usage)

function ScreenToolsShowcase() {
  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader eyebrow="AI · Tool Cards"
        title="Frontend Tool Cards"
        subtitle="The six display tools agents render into chat, rails and inline surfaces"
        actions={<>
          <button className="btn btn-outline btn-sm"><Icon name="book" size={12}/> Tool spec</button>
          <button className="btn btn-primary btn-sm"><Icon name="bot" size={12}/> Try in playground</button>
        </>}/>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 18 }}>
        {/* 1. display_fx_rate_chart */}
        <ShowcaseSlot
          n="01"
          name="display_fx_rate_chart"
          kind="Visualization"
          desc="30-day line chart with a BUY/HOLD/WAIT signal and human-readable rationale. Called by the FX Agent before every cross-border payout.">
          <ToolCardFx base="GBP" target="NGN" signal="buy"
            signalReason="Rate near 30-day high and NGN inflows concentrated next week."
            rates={[
              { date: '25 Mar', rate: 1908 }, { date: '30 Mar', rate: 1920 },
              { date: '5 Apr',  rate: 1945 }, { date: '10 Apr', rate: 1930 },
              { date: '15 Apr', rate: 1965 }, { date: '18 Apr', rate: 1980 },
              { date: '22 Apr', rate: 2012 },
            ]}/>
        </ShowcaseSlot>

        {/* 2. display_budget_breakdown */}
        <ShowcaseSlot
          n="02"
          name="display_budget_breakdown"
          kind="Visualization"
          desc="Category-level progress bars with over / on-track / under status. Rendered inline when the user asks about burn rate or variance.">
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
        </ShowcaseSlot>

        {/* 3. display_spending_pie_chart */}
        <ShowcaseSlot
          n="03"
          name="display_spending_pie_chart"
          kind="Visualization"
          desc="Donut chart plus ranked legend. Answers 'where is my money going?' in one glance.">
          <ToolCardPie
            title="Spending · April 2026" totalSpent={42180} currency="GBP"
            categories={[
              { name: 'Fuel · fleet',    amount: 19200, percentage: 46 },
              { name: 'Warehousing',     amount: 10100, percentage: 24 },
              { name: 'Contractors',     amount:  8420, percentage: 20 },
              { name: 'Insurance',       amount:  3960, percentage:  9 },
              { name: 'Other',           amount:   500, percentage:  1 },
            ]}/>
        </ShowcaseSlot>

        {/* 4. display_autopilot_proposal */}
        <ShowcaseSlot
          n="04"
          name="display_autopilot_proposal"
          kind="Action · Proposal"
          desc="Agent proposes a concrete write action with severity, detailed fields and Apply / Review / Dismiss. The signature 'agents propose, systems apply' pattern.">
          <ToolCardAutopilot
            agent="Billing Agent"
            action="Apply INV-2041 to bank txn 9f2c1a"
            description="Reference, amount and counterparty all match. Below the £50K policy ceiling."
            details={[
              { label: 'Invoice',    value: 'INV-2041 · Primrose' },
              { label: 'Amount',     value: '£12,480.00' },
              { label: 'Account',    value: '1200 Accounts Receivable' },
              { label: 'Confidence', value: '0.94' },
            ]}
            severity="low"/>
        </ShowcaseSlot>

        {/* 5. confirmAction */}
        <ShowcaseSlot
          n="05"
          name="confirmAction"
          kind="Action · Confirmation"
          desc="Human-in-the-loop gate. Surfaced by policies that require explicit approval before an agent can write. Severity colours the border.">
          <ToolCardConfirm
            action="Post journal entry JE-4082"
            description="Apply £12,480 revenue from Primrose INV-2041. Balanced debit/credit."
            severity="medium"/>
        </ShowcaseSlot>

        {/* 6. display_option_selector */}
        <ShowcaseSlot
          n="06"
          name="display_option_selector"
          kind="Input · Selection"
          desc="Agent asks the user to pick from options. Single or multi-select. Returns the choice back into the conversation so the agent can continue.">
          <ToolCardOptions
            question="Which hedging strategy should I prepare?"
            options={[
              { label: '1-month forward', description: 'Lock in ₦2,012 for 30 days' },
              { label: '3-month forward', description: 'Lock in for 90 days at ₦2,045' },
              { label: 'Spot + monitor',  description: 'No hedge; alert if >2% drift' },
            ]}/>
        </ShowcaseSlot>
      </div>

      <Card title="How tool cards flow" subtitle="From agent decision to user surface" style={{ marginTop: 4 }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 14, marginTop: 4 }}>
          {[
            { n: '1', t: 'Agent calls tool', d: 'display_* tool invoked during reasoning with structured args.' },
            { n: '2', t: 'Frontend renders', d: 'Typed handler maps tool name → React component.' },
            { n: '3', t: 'User interacts',  d: 'Approves, selects, or dismisses. State persists in thread.' },
            { n: '4', t: 'Agent resumes',   d: 'Result is fed back; agent takes next step.' },
          ].map(s => (
            <div key={s.n} style={{ background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 10, padding: 14 }}>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--brand-primary)', fontWeight: 700, letterSpacing: '0.08em' }}>STEP {s.n}</div>
              <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', marginTop: 4 }}>{s.t}</div>
              <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 4, lineHeight: 1.5 }}>{s.d}</div>
            </div>
          ))}
        </div>
      </Card>
    </div>
  );
}

function ShowcaseSlot({ n, name, kind, desc, children }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 10 }}>
        <span style={{
          fontFamily: 'var(--font-mono)', fontSize: 10, fontWeight: 700, letterSpacing: '0.08em',
          color: 'var(--brand-primary)', background: 'var(--brand-primary-10)',
          padding: '3px 8px', borderRadius: 4, flex: 'none',
        }}>{n}</span>
        <div style={{ flex: 1 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <code style={{ fontFamily: 'var(--font-mono)', fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{name}</code>
            <Pill tone="tint" size="sm">{kind}</Pill>
          </div>
          <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 4, lineHeight: 1.5 }}>{desc}</div>
        </div>
      </div>
      <div>{children}</div>
    </div>
  );
}

// ─── AI Playground ────────────────────────────────────────────────
//
// Mirrors the structure of src/pages/ai/AiPlaygroundPage.tsx (Langfuse-style):
//
//   ┌── Header (title + Split window · Reset · Run All) ──────────────┐
//   ├── Config bar (Agent|Task · Agent picker · Model · Voice · Tools ┤
//   │                · User Brief · Settings · Scenarios)             │
//   ├──────────────────────────────────────────────┬──────────────────┤
//   │ Stacked editable message blocks              │  Output panel    │
//   │  · System (with Regenerate w/ AI · ⛶)        │   · streamed     │
//   │  · User #1                                   │   · tool calls   │
//   │  · Assistant #2                              │   · metrics      │
//   │  · User #3                                   │   · review       │
//   │  · + Message                                 │                  │
//   │ Submit · Run history                         │                  │
//   └──────────────────────────────────────────────┴──────────────────┘
//
function ScreenPlayground() {
  const [pgMode, setPgMode] = React.useState('agent');         // agent | task
  const [splitMode, setSplitMode] = React.useState('single');  // single | compare
  const [voice, setVoice] = React.useState(false);
  const [historyOpen, setHistoryOpen] = React.useState(true);
  const [systemPrompt, setSystemPrompt] = React.useState(
`You are Aonik's Billing Agent.

Role
  Match invoices to bank transactions. Reconcile on the user's behalf.

Guardrails
  • Require explicit user confirmation for any action above £50,000.
  • Never mutate closed periods. Propose a reversing entry instead.
  • When a counterparty is flagged KYC-pending, pause and surface a
    confirmAction card.

Tools
  search_invoices, list_bank_transactions, match_invoice_to_txn,
  draft_journal_entry, confirmAction.

Response style
  Be concise. Prefer tool cards over prose. Always cite the specific
  invoice / txn ID in your reply.`
  );
  const [messages, setMessages] = React.useState([
    { id: 'm1', role: 'user',      content: "Match last week's bank transactions to open invoices from Primrose." },
    { id: 'm2', role: 'assistant', content: "I found 3 matches. One is below the £50K ceiling and can be auto-applied; the other two need your approval." },
    { id: 'm3', role: 'user',      content: '' },
  ]);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', minHeight: 0, background: 'var(--background)' }}>
      <PlaygroundHeader splitMode={splitMode} onSplitChange={setSplitMode}/>
      <PlaygroundConfigBar
        pgMode={pgMode} onPgModeChange={setPgMode}
        voice={voice} onVoiceChange={setVoice}
      />

      {/* Split content */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 540px', flex: 1, minHeight: 0, overflow: 'hidden' }}>
        {/* ─── Left: messages + run history ───────────────────────── */}
        <div style={{ display: 'flex', flexDirection: 'column', minHeight: 0, borderRight: '1px solid var(--border-light)' }}>
          <div style={{ flex: 1, overflowY: 'auto' }}>
            <PlaygroundMessageBlock
              role="system" content={systemPrompt}
              onChange={setSystemPrompt}
              agentName="Billing Agent"
              hasChanges
            />
            {messages.map((m, i) => (
              <PlaygroundMessageBlock
                key={m.id}
                role={m.role}
                content={m.content}
                index={i + 1}
                onChange={(v) => setMessages(prev => prev.map(x => x.id === m.id ? { ...x, content: v } : x))}
                onRoleChange={(r) => setMessages(prev => prev.map(x => x.id === m.id ? { ...x, role: r } : x))}
                onDelete={messages.length > 1 ? () => setMessages(prev => prev.filter(x => x.id !== m.id)) : null}
              />
            ))}
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '10px 24px', borderBottom: '1px solid var(--border-light)' }}>
              <button className="btn btn-ghost btn-sm" style={{ height: 26, fontSize: 11.5, color: 'var(--text-secondary)' }}
                onClick={() => setMessages(prev => [
                  ...prev,
                  { id: `m${Date.now()}`, role: prev[prev.length - 1]?.role === 'user' ? 'assistant' : 'user', content: '' }
                ])}>
                <Icon name="plus" size={11}/> Message <Icon name="chevron-down" size={10}/>
              </button>
            </div>
          </div>

          {/* Submit */}
          <div style={{ flex: 'none', padding: '12px 24px', borderTop: '1px solid var(--border-light)', background: 'var(--surface)' }}>
            <button className="btn btn-primary"
              style={{ width: '100%', justifyContent: 'center', height: 36, fontSize: 13, fontWeight: 600 }}>
              <Icon name="play" size={12}/> Submit
              <span style={{
                marginLeft: 8, fontFamily: 'var(--font-mono)', fontSize: 10, fontWeight: 400,
                opacity: 0.7, padding: '2px 5px', border: '1px solid rgba(255,255,255,0.25)', borderRadius: 3,
              }}>⌘ Enter</span>
            </button>
          </div>

          {/* Run history (collapsible) */}
          <PlaygroundRunHistory open={historyOpen} onToggle={() => setHistoryOpen(o => !o)}/>
        </div>

        {/* ─── Right: output panel ────────────────────────────────── */}
        <PlaygroundOutputPanel modelName="claude-sonnet-4.5" voice={voice}/>
      </div>
    </div>
  );
}

// ── Header ──────────────────────────────────────────────────────────
function PlaygroundHeader({ splitMode, onSplitChange }) {
  return (
    <div style={{ flex: 'none', padding: '18px 24px 14px', borderBottom: '1px solid var(--border-light)', background: 'var(--surface)' }}>
      <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 24 }}>
        <div>
          <h1 style={{ margin: 0, fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>
            AI Playground
          </h1>
          <p style={{ margin: '3px 0 0', fontSize: 12.5, color: 'var(--text-secondary)' }}>
            Test agents, AI tasks, prompts, and models interactively.
          </p>
        </div>
        <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
          <button className="btn btn-ghost btn-sm" onClick={() => onSplitChange(splitMode === 'single' ? 'compare' : 'single')}>
            <Icon name="columns" size={12}/> {splitMode === 'single' ? 'Split window' : 'Single'}
          </button>
          <button className="btn btn-ghost btn-sm">
            <Icon name="refresh" size={12}/> Reset playground
          </button>
          <button className="btn btn-primary btn-sm" style={{ height: 32, paddingRight: 6 }}>
            <Icon name="play" size={12}/> Run All
            <span style={{
              marginLeft: 6, fontFamily: 'var(--font-mono)', fontSize: 10, fontWeight: 400,
              opacity: 0.65, padding: '2px 5px', border: '1px solid rgba(255,255,255,0.25)', borderRadius: 3,
            }}>⌘ Enter</span>
          </button>
        </div>
      </div>
    </div>
  );
}

// ── Config bar (compact horizontal row of pickers + popover triggers) ──
function PlaygroundConfigBar({ pgMode, onPgModeChange, voice, onVoiceChange }) {
  const Pip = ({ children }) => (
    <span style={{
      marginLeft: 6, padding: '1px 6px', borderRadius: 999, fontSize: 9.5, fontFamily: 'var(--font-mono)',
      background: 'var(--brand-primary)', color: '#fff', fontWeight: 600,
    }}>{children}</span>
  );

  const ConfigChip = ({ icon, children, count, dot }) => (
    <button style={{
      display: 'inline-flex', alignItems: 'center', gap: 6, height: 30,
      padding: '0 10px', border: '1px solid var(--border-light)', borderRadius: 6,
      background: 'transparent', color: 'var(--text-primary)', fontSize: 12, cursor: 'pointer',
    }}>
      {icon && <Icon name={icon} size={12}/>}
      {children}
      {count != null && <Pip>{count}</Pip>}
      {dot && <span style={{ width: 6, height: 6, borderRadius: 999, background: 'var(--brand-primary)' }}/>}
      <Icon name="chevron-down" size={10}/>
    </button>
  );

  return (
    <div style={{
      flex: 'none', display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap',
      padding: '10px 24px', borderBottom: '1px solid var(--border-light)', background: 'var(--surface)',
    }}>
      {/* Agent | AI Task toggle */}
      <div style={{ display: 'inline-flex', borderRadius: 6, border: '1px solid var(--border-light)', overflow: 'hidden', height: 30 }}>
        {[['agent', 'bot', 'Agent'], ['task', 'list-checks', 'AI Task']].map(([k, ic, lab]) => {
          const active = pgMode === k;
          return (
            <button key={k} onClick={() => onPgModeChange(k)}
              style={{
                display: 'inline-flex', alignItems: 'center', gap: 5, padding: '0 12px',
                fontSize: 12, fontWeight: active ? 600 : 500, cursor: 'pointer', border: 'none',
                background: active ? 'var(--brand-primary)' : 'transparent',
                color: active ? '#fff' : 'var(--text-secondary)',
              }}>
              <Icon name={ic} size={12}/> {lab}
            </button>
          );
        })}
      </div>

      {/* Agent or Task picker */}
      {pgMode === 'agent' ? (
        <ConfigChip icon="bot">Billing Agent</ConfigChip>
      ) : (
        <ConfigChip icon="list-checks">Reconciliation Task</ConfigChip>
      )}

      {/* Model */}
      <ConfigChip icon="cpu">claude-sonnet-4.5</ConfigChip>

      <Divider/>

      {/* Voice mode */}
      <div style={{
        display: 'inline-flex', alignItems: 'center', gap: 8, height: 30, padding: '0 10px',
        border: '1px solid var(--border-light)', borderRadius: 6,
      }}>
        <Icon name="volume" size={12} color="var(--text-secondary)"/>
        <span style={{ fontSize: 11.5, color: 'var(--text-secondary)' }}>Voice</span>
        <span onClick={() => onVoiceChange(!voice)}
          style={{
            width: 24, height: 13, borderRadius: 999, position: 'relative', cursor: 'pointer',
            background: voice ? 'var(--brand-primary)' : 'var(--gray-200)', transition: 'background 0.15s',
          }}>
          <span style={{
            position: 'absolute', top: 1, left: voice ? 12 : 1, width: 11, height: 11, borderRadius: 999, background: '#fff',
            transition: 'left 0.15s',
          }}/>
        </span>
      </div>

      {/* Tools (agent mode) */}
      {pgMode === 'agent' && <ConfigChip icon="wrench" count={5}>Tools</ConfigChip>}

      {/* Variables (task mode) */}
      {pgMode === 'task' && <ConfigChip icon="variable" count={3}>Variables</ConfigChip>}

      {/* User Brief */}
      {pgMode === 'agent' && <ConfigChip icon="user" dot>User Brief</ConfigChip>}

      {/* Agent context */}
      {pgMode === 'agent' && <ConfigChip icon="layers">Context</ConfigChip>}

      {/* Settings */}
      <ConfigChip icon="sliders">Settings</ConfigChip>

      <Divider/>

      {/* Scenarios */}
      <ConfigChip icon="bookmark">Scenarios</ConfigChip>
    </div>
  );
}

function Divider() {
  return <span style={{ width: 1, height: 18, background: 'var(--border-light)' }}/>;
}

// ── Single message block (system / user / assistant) ────────────────
function PlaygroundMessageBlock({ role, content, index, onChange, onRoleChange, onDelete, agentName, hasChanges }) {
  const isSystem = role === 'system';
  const tokens = Math.round((content || '').length / 4);
  const [wizardOpen, setWizardOpen] = React.useState(false);

  return (
    <div style={{
      display: 'flex', gap: 14, padding: '14px 24px',
      borderBottom: '1px solid var(--border-light)',
    }}>
      {/* Role column */}
      <div style={{ width: 80, flex: 'none', paddingTop: 6 }}>
        {isSystem ? (
          <span style={{ fontSize: 11.5, fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'capitalize' }}>
            System
          </span>
        ) : (
          <select
            value={role}
            onChange={e => onRoleChange?.(e.target.value)}
            className="input"
            style={{ height: 26, fontSize: 11.5, padding: '0 6px', width: '100%' }}>
            <option value="user">User</option>
            <option value="assistant">Assistant</option>
          </select>
        )}
        {index != null && (
          <div style={{ marginTop: 4, fontSize: 10, fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>
            #{index}
          </div>
        )}
      </div>

      {/* Content column */}
      <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', gap: 8 }}>
        {isSystem && (
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: 4 }}>
            <button className="btn btn-ghost btn-sm" onClick={() => setWizardOpen(o => !o)}
              style={{ height: 26, fontSize: 11.5, color: 'var(--brand-primary)' }}>
              <Icon name="sparkles" size={12}/> Regenerate with AI
            </button>
            <button title="Edit fullscreen" className="hover-halo" style={{ padding: 5 }}>
              <Icon name="maximize" size={12} color="var(--text-tertiary)"/>
            </button>
          </div>
        )}

        <textarea
          value={content}
          onChange={e => onChange?.(e.target.value)}
          rows={isSystem ? 10 : 2}
          placeholder={`Enter ${role} message…`}
          spellCheck={false}
          style={{
            width: '100%', boxSizing: 'border-box',
            padding: '10px 12px',
            border: '1px solid var(--border-light)', borderRadius: 6,
            background: 'var(--surface)', color: 'var(--text-primary)',
            fontFamily: 'var(--font-mono)', fontSize: 12, lineHeight: 1.55,
            resize: 'vertical', outline: 'none',
          }}/>

        {isSystem && wizardOpen && (
          <div style={{
            display: 'flex', flexDirection: 'column', gap: 8,
            padding: 12, borderRadius: 6,
            border: '1px solid var(--border-light)', background: 'var(--surface-inset)',
          }}>
            <textarea rows={2}
              placeholder="Describe how you'd like the prompt changed (e.g. 'Make it more concise…')"
              style={{
                width: '100%', boxSizing: 'border-box', padding: '8px 10px',
                border: '1px solid var(--border-light)', borderRadius: 6,
                background: 'var(--surface)', fontSize: 12, fontFamily: 'inherit',
                resize: 'vertical', outline: 'none', color: 'var(--text-primary)',
              }}/>
            <div style={{ display: 'flex', gap: 6 }}>
              <button className="btn btn-primary btn-sm"><Icon name="sparkles" size={11}/> Generate prompt</button>
              <button className="btn btn-ghost btn-sm" onClick={() => setWizardOpen(false)}>
                <Icon name="x" size={11}/> Discard
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Right meta column */}
      <div style={{ flex: 'none', display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 5, paddingTop: 4 }}>
        <span style={{
          fontSize: 10, fontFamily: 'var(--font-mono)', fontVariantNumeric: 'tabular-nums',
          padding: '2px 7px', borderRadius: 999, background: 'var(--surface-inset)',
          color: 'var(--text-tertiary)', fontWeight: 500,
        }}>{tokens}</span>

        {isSystem && agentName && hasChanges && (
          <>
            <button title="Reset to agent default" className="hover-halo" style={{ padding: 4 }}>
              <Icon name="refresh" size={11} color="var(--text-tertiary)"/>
            </button>
            <button title="Save to agent config" className="hover-halo" style={{ padding: 4 }}>
              <Icon name="check" size={11} color="var(--brand-primary)"/>
            </button>
          </>
        )}

        {onDelete && (
          <button onClick={onDelete} title="Delete" className="hover-halo" style={{ padding: 4 }}>
            <Icon name="trash" size={11} color="var(--text-tertiary)"/>
          </button>
        )}
      </div>
    </div>
  );
}

// ── Output panel (right side, full-height) ──────────────────────────
function PlaygroundOutputPanel({ modelName, voice }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', minHeight: 0, background: 'var(--surface)' }}>
      {/* Header strip */}
      <div style={{
        flex: 'none', display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        padding: '8px 20px', borderBottom: '1px solid var(--border-light)',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ fontSize: 11.5, fontWeight: 600, color: 'var(--text-secondary)' }}>Output</span>
          {modelName && (
            <span style={{
              fontSize: 10, fontFamily: 'var(--font-mono)', padding: '2px 6px', borderRadius: 4,
              background: 'var(--surface-inset)', color: 'var(--text-tertiary)', fontWeight: 500,
            }}>{modelName}</span>
          )}
          {voice && (
            <span style={{
              fontSize: 10, fontFamily: 'var(--font-mono)', padding: '2px 6px', borderRadius: 4,
              background: 'var(--surface-inset)', color: 'var(--text-tertiary)', fontWeight: 500,
            }}>Voice idle</span>
          )}
        </div>
        <div style={{ display: 'flex', gap: 4 }}>
          <button className="btn btn-ghost btn-sm" style={{ height: 24, fontSize: 11 }}>
            <Icon name="sparkles" size={11}/> Review
          </button>
          <button className="btn btn-ghost btn-sm" style={{ height: 24, fontSize: 11 }}>
            <Icon name="arrow-down" size={11}/> Add to messages
          </button>
        </div>
      </div>

      {/* Streamed body */}
      <div style={{ flex: 1, overflowY: 'auto', padding: '16px 20px', display: 'flex', flexDirection: 'column', gap: 12 }}>
        {/* Tool-call trace */}
        <div style={{
          display: 'flex', flexDirection: 'column', gap: 6,
          padding: 10, borderRadius: 6, background: 'var(--surface-inset)', border: '1px solid var(--border-light)',
        }}>
          <div style={{ fontSize: 10, letterSpacing: '0.06em', color: 'var(--text-tertiary)', fontWeight: 600, textTransform: 'uppercase' }}>
            Tool calls · 3
          </div>
          <ToolCallRow name="search_invoices" args={{ counterparty: 'Primrose', status: 'open' }} ms="142ms" status="ok"/>
          <ToolCallRow name="list_bank_transactions" args={{ from: '2026-04-15', to: '2026-04-22' }} ms="318ms" status="ok"/>
          <ToolCallRow name="match_invoice_to_txn" args={{ invoice: 'INV-2041', txn: '9f2c1a' }} ms="211ms" status="ok"/>
        </div>

        {/* Assistant streamed text */}
        <div style={{ fontSize: 13, lineHeight: 1.6, color: 'var(--text-primary)' }}>
          I found <b>3 matches</b>. One is below the £50K policy ceiling and can be auto-applied.
          The other two need your approval before I can post them to the ledger.
        </div>

        {/* Embedded tool card from the agent */}
        <ToolCardAutopilot
          agent="Billing Agent"
          action="Apply INV-2041 to bank txn 9f2c1a"
          description="Reference, amount and counterparty all match. Below the £50K policy ceiling."
          details={[
            { label: 'Invoice',    value: 'INV-2041 · Primrose' },
            { label: 'Amount',     value: '£12,480.00' },
            { label: 'Confidence', value: '0.94' },
          ]}
          severity="low"/>

        <ToolCardConfirm
          action="Apply INV-2038 to bank txn 8b1a42 · £62,400"
          description="Above the £50K policy ceiling. Requires explicit approval."
          severity="high"/>

        <div style={{ fontSize: 13, lineHeight: 1.6, color: 'var(--text-primary)' }}>
          The third match (<code style={{ fontFamily: 'var(--font-mono)', fontSize: 12 }}>INV-2042</code>) has
          a counterparty mismatch — I'll flag it for review rather than auto-apply.
        </div>
      </div>

      {/* Metrics footer */}
      <div style={{
        flex: 'none', display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        padding: '10px 20px', borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)',
        fontSize: 11, fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)',
        fontVariantNumeric: 'tabular-nums',
      }}>
        <div style={{ display: 'flex', gap: 18 }}>
          <span><span style={{ color: 'var(--text-tertiary)' }}>tokens</span> 2,314</span>
          <span><span style={{ color: 'var(--text-tertiary)' }}>in</span> 1,820 · <span style={{ color: 'var(--text-tertiary)' }}>out</span> 494</span>
          <span><span style={{ color: 'var(--text-tertiary)' }}>cost</span> $0.0184</span>
          <span><span style={{ color: 'var(--text-tertiary)' }}>lat</span> 3.1s</span>
        </div>
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5, color: 'var(--success)' }}>
          <span style={{ width: 6, height: 6, borderRadius: 999, background: 'var(--success)' }}/> Stream complete
        </span>
      </div>
    </div>
  );
}

function ToolCallRow({ name, args, ms, status }) {
  const dot = status === 'ok' ? 'var(--success)' : status === 'err' ? 'var(--danger)' : 'var(--warning)';
  return (
    <div style={{
      display: 'grid', gridTemplateColumns: '12px 1fr auto', gap: 8, alignItems: 'baseline',
      fontFamily: 'var(--font-mono)', fontSize: 11.5,
    }}>
      <span style={{ width: 6, height: 6, borderRadius: 999, background: dot, marginTop: 5 }}/>
      <div style={{ minWidth: 0 }}>
        <span style={{ color: 'var(--text-primary)', fontWeight: 600 }}>{name}</span>
        <span style={{
          marginLeft: 6, color: 'var(--text-tertiary)', fontSize: 11,
          overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
        }}>
          {Object.entries(args || {}).map(([k, v], i) => (
            <span key={k}>
              {i > 0 && ', '}<span>{k}</span>=<span style={{ color: 'var(--text-secondary)' }}>{JSON.stringify(v)}</span>
            </span>
          ))}
        </span>
      </div>
      <span style={{ color: 'var(--text-tertiary)', fontSize: 11 }}>{ms}</span>
    </div>
  );
}

// ── Run history (collapsible footer) ────────────────────────────────
function PlaygroundRunHistory({ open, onToggle }) {
  const runs = [
    { id: 'run_9f2c1a', prompt: "Match last week's bank transactions to open invoices from Primrose", status: 'ok',   tools: 5, tokens: '2,314', ms: '3.1s', when: '3m ago' },
    { id: 'run_8b1a42', prompt: 'Draft journal for Q3 depreciation on fleet assets',                  status: 'ok',   tools: 3, tokens: '1,840', ms: '2.4s', when: '11m ago' },
    { id: 'run_4d7e09', prompt: 'Apply INV-2038 £62,400 to bank txn 8b1a42',                          status: 'held', tools: 2, tokens:   '912', ms: '0.8s', when: '18m ago' },
    { id: 'run_2a3f18', prompt: 'Reconcile FX buffer account for October',                            status: 'err',  tools: 4, tokens: '3,128', ms: '4.2s', when: '1h ago' },
  ];
  const dotColor = { ok: 'var(--success)', held: 'var(--warning)', err: 'var(--danger)' };

  return (
    <div style={{ flex: 'none', borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)' }}>
      <button onClick={onToggle}
        style={{
          width: '100%', display: 'flex', alignItems: 'center', justifyContent: 'space-between',
          padding: '8px 24px', background: 'none', border: 'none', cursor: 'pointer',
          fontSize: 11.5, fontWeight: 600, color: 'var(--text-secondary)',
        }}>
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
          <Icon name={open ? 'chevron-down' : 'chevron-right'} size={11}/>
          Run history
          <span style={{
            fontSize: 10, fontFamily: 'var(--font-mono)', padding: '1px 6px',
            background: 'var(--surface)', color: 'var(--text-tertiary)', borderRadius: 999,
          }}>{runs.length}</span>
        </span>
        <span style={{ fontSize: 11, fontWeight: 400, color: 'var(--text-tertiary)' }}>
          <Icon name="trash" size={10}/> Clear
        </span>
      </button>

      {open && (
        <div style={{ maxHeight: 160, overflowY: 'auto', borderTop: '1px solid var(--border-light)' }}>
          {runs.map(r => (
            <div key={r.id} style={{
              display: 'grid', gridTemplateColumns: '90px 1fr auto auto auto auto',
              gap: 12, alignItems: 'center',
              padding: '7px 24px', borderBottom: '1px solid var(--border-light)',
              fontSize: 11.5,
            }}>
              <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }}>{r.id}</span>
              <span style={{ color: 'var(--text-secondary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                {r.prompt}
              </span>
              <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5, fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>
                <span style={{ width: 6, height: 6, borderRadius: 999, background: dotColor[r.status] }}/>
                {r.status}
              </span>
              <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)', fontSize: 11, fontVariantNumeric: 'tabular-nums' }}>{r.tokens}t</span>
              <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)', fontSize: 11, fontVariantNumeric: 'tabular-nums' }}>{r.ms}</span>
              <span style={{ color: 'var(--text-tertiary)', fontSize: 11 }}>{r.when}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ─── AI Usage ─────────────────────────────────────────────────────
function ScreenUsage() {
  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader eyebrow="AI · Analytics" title="Usage"
        subtitle="Token consumption, costs and tool-call volume across all agents"
        actions={<>
          <button className="btn btn-outline btn-sm"><Icon name="calendar" size={12}/> Last 30 days</button>
          <button className="btn btn-outline btn-sm"><Icon name="download" size={12}/> Export CSV</button>
        </>}/>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 14 }}>
        <KPI label="Tokens · input"  value="42.1M" delta="+18%" deltaTone="up"   spark="0,22 15,20 30,18 45,16 60,14 75,12 90,10 100,8"  sparkColor="#055a60"/>
        <KPI label="Tokens · output" value="8.4M"  delta="+12%" deltaTone="up"   spark="0,20 15,18 30,16 45,14 60,12 75,11 90,10 100,9"  sparkColor="#3ab795"/>
        <KPI label="Tool calls"      value="14,820" delta="+24%" deltaTone="up"   spark="0,18 15,16 30,14 45,13 60,11 75,9 90,7 100,5"   sparkColor="#7b76b6"/>
        <KPI label="Monthly cost"    value="$2,184" delta="+9%"  deltaTone="up"   spark="0,24 15,22 30,20 45,18 60,15 75,13 90,10 100,8" sparkColor="#eb5c37"/>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1.4fr 1fr', gap: 20 }}>
        <Card title="Token usage" subtitle="Input vs output · last 30 days">
          <svg viewBox="0 0 600 200" style={{ width: '100%', height: 220 }}>
            {[0, 50, 100, 150, 200].map(y => <line key={y} x1="0" y1={y} x2="600" y2={y} stroke="var(--border-light)" strokeDasharray="2 4"/>)}
            {Array.from({ length: 30 }, (_, i) => {
              const x = (i / 29) * 600;
              const hIn = 40 + Math.random() * 80;
              const hOut = 15 + Math.random() * 25;
              return (
                <g key={i}>
                  <rect x={x - 6} y={200 - hIn} width={5} height={hIn} fill="var(--brand-primary)" opacity="0.8"/>
                  <rect x={x} y={200 - hOut} width={5} height={hOut} fill="var(--brand-secondary)" opacity="0.85"/>
                </g>
              );
            })}
          </svg>
          <div style={{ display: 'flex', gap: 20, fontSize: 11, marginTop: 6 }}>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><span style={{ width: 10, height: 10, background: 'var(--brand-primary)' }}/> Input</span>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><span style={{ width: 10, height: 10, background: 'var(--brand-secondary)' }}/> Output</span>
          </div>
        </Card>

        <Card title="By agent" subtitle="Share of monthly spend">
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginTop: 4 }}>
            {[
              { n: 'Billing Agent',    p: 42, $: '$918',  c: '#eb5c37' },
              { n: 'Ledger Agent',     p: 24, $: '$524',  c: '#055a60' },
              { n: 'FX Agent',         p: 14, $: '$305',  c: '#3ab795' },
              { n: 'Compliance Agent', p: 11, $: '$240',  c: '#7b76b6' },
              { n: 'Close Agent',      p:  6, $: '$131',  c: '#0097a9' },
              { n: 'Dunning Agent',    p:  3, $:  '$66',  c: '#5facbd' },
            ].map(a => (
              <div key={a.n}>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, marginBottom: 3 }}>
                  <span style={{ color: 'var(--text-primary)' }}>{a.n}</span>
                  <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)' }}>{a.$} · {a.p}%</span>
                </div>
                <div style={{ height: 4, background: 'var(--surface-inset)', borderRadius: 999, overflow: 'hidden' }}>
                  <div style={{ width: a.p + '%', height: '100%', background: a.c }}/>
                </div>
              </div>
            ))}
          </div>
        </Card>
      </div>

      <Card title="Top tool calls" subtitle="Most invoked tools in the last 30 days">
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
          <thead>
            <tr style={{ color: 'var(--text-tertiary)', fontSize: 11, letterSpacing: '0.04em', textAlign: 'left' }}>
              <th style={{ padding: '10px 8px', fontWeight: 500 }}>TOOL</th>
              <th style={{ padding: '10px 8px', fontWeight: 500 }}>AGENT</th>
              <th style={{ padding: '10px 8px', fontWeight: 500, textAlign: 'right' }}>CALLS</th>
              <th style={{ padding: '10px 8px', fontWeight: 500, textAlign: 'right' }}>AVG MS</th>
              <th style={{ padding: '10px 8px', fontWeight: 500, textAlign: 'right' }}>ERRORS</th>
            </tr>
          </thead>
          <tbody>
            {[
              { t: 'search_invoices',        a: 'Billing',    c: '4,218', ms: '142', e: '0.2%' },
              { t: 'list_bank_transactions', a: 'Billing',    c: '3,884', ms: '318', e: '0.4%' },
              { t: 'match_invoice_to_txn',   a: 'Billing',    c: '2,910', ms: '211', e: '0.1%' },
              { t: 'draft_journal_entry',    a: 'Ledger',     c: '1,420', ms: '182', e: '0.8%' },
              { t: 'display_fx_rate_chart',  a: 'FX',         c:   '840', ms:  '88', e: '0.0%' },
              { t: 'confirmAction',          a: 'All',        c:   '612', ms:   '—', e: '0.0%' },
              { t: 'display_budget_breakdown', a: 'Insights', c:   '436', ms:  '92', e: '0.0%' },
            ].map((r, i) => (
              <tr key={i} style={{ borderTop: '1px solid var(--border-light)' }}>
                <td style={{ padding: '10px 8px', fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-primary)' }}>{r.t}</td>
                <td style={{ padding: '10px 8px', color: 'var(--text-secondary)' }}>{r.a}</td>
                <td style={{ padding: '10px 8px', fontFamily: 'var(--font-mono)', textAlign: 'right', color: 'var(--text-primary)', fontWeight: 500 }}>{r.c}</td>
                <td style={{ padding: '10px 8px', fontFamily: 'var(--font-mono)', textAlign: 'right', color: 'var(--text-secondary)' }}>{r.ms}</td>
                <td style={{ padding: '10px 8px', fontFamily: 'var(--font-mono)', textAlign: 'right', color: r.e === '0.0%' ? 'var(--success)' : 'var(--warning)' }}>{r.e}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>
    </div>
  );
}

Object.assign(window, { ScreenToolsShowcase, ScreenPlayground, ScreenUsage });
