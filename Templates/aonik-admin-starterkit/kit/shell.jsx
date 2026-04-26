// AONIK Admin UI — Shell (sidebar + topbar + content frame + right agent rail)
// Usage: <AppShell activeModule="dashboard" rightOpen>{children}</AppShell>

const NAV = [
  { group: 'Workspace', items: [
    { id: 'myspace',    label: 'My Space',         icon: 'home' },
    { id: 'dashboard',  label: 'Overview',         icon: 'dashboard' },
    { id: 'agents',     label: 'Agent Command',    icon: 'bot', badge: 3 },
  ]},
  { group: 'Finance', items: [
    { id: 'ledger',     label: 'Ledger',           icon: 'ledger' },
    { id: 'invoices',   label: 'Invoices',         icon: 'invoice' },
    { id: 'bank',       label: 'Bank feeds',       icon: 'bank' },
    { id: 'payouts',    label: 'Payouts',          icon: 'payout' },
  ]},
  { group: 'Operations', items: [
    { id: 'compliance', label: 'Compliance',       icon: 'shield' },
    { id: 'reports',    label: 'Reports',          icon: 'chart' },
    { id: 'team',       label: 'Team',             icon: 'users' },
  ]},
];

function Sidebar({ active = 'dashboard' }) {
  return (
    <aside style={{
      width: 240, flex: 'none',
      background: 'var(--surface-inset)',
      borderRight: '1px solid var(--border-light)',
      display: 'flex', flexDirection: 'column',
      padding: '14px 12px', gap: 4,
    }}>
      {/* brand */}
      <div style={{ padding: '8px 10px 18px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <AonikWordmark size={19}/>
        <span className="hover-halo"><Icon name="chevron" size={14}/></span>
      </div>

      {/* workspace switcher */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 10,
        padding: '8px 10px', borderRadius: 8,
        background: 'var(--surface)',
        border: '1px solid var(--border-light)',
        marginBottom: 14,
      }}>
        <Avatar name="Primrose Logistics" size={28} color="#055a60" textColor="#fff"/>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>Primrose Logistics</div>
          <div style={{ fontSize: 10, color: 'var(--text-secondary)' }}>NGN · USD · GBP</div>
        </div>
        <Icon name="chevdown" size={14} color="var(--text-tertiary)"/>
      </div>

      {/* search */}
      <div style={{ position: 'relative', marginBottom: 8 }}>
        <span style={{ position: 'absolute', left: 10, top: '50%', transform: 'translateY(-50%)', color: 'var(--text-tertiary)' }}>
          <Icon name="search" size={14}/>
        </span>
        <input className="input" placeholder="Search or ask…" style={{ paddingLeft: 32, height: 34, fontSize: 13, background: 'var(--surface)' }}/>
        <span style={{ position: 'absolute', right: 8, top: '50%', transform: 'translateY(-50%)', fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)', background: 'var(--surface-inset)', padding: '2px 6px', borderRadius: 4, border: '1px solid var(--border-light)' }}>⌘K</span>
      </div>

      {/* nav groups */}
      <div style={{ overflowY: 'auto', flex: 1, marginTop: 4 }}>
        {NAV.map(group => (
          <div key={group.group} style={{ marginBottom: 14 }}>
            <div style={{
              fontSize: 10, fontWeight: 600, letterSpacing: '0.08em', textTransform: 'uppercase',
              color: 'var(--text-tertiary)', padding: '6px 10px 4px',
            }}>{group.group}</div>
            {group.items.map(item => {
              const isActive = item.id === active;
              return (
                <div key={item.id} style={{
                  display: 'flex', alignItems: 'center', gap: 10,
                  padding: '7px 10px', borderRadius: 8, cursor: 'pointer',
                  background: isActive ? 'var(--surface)' : 'transparent',
                  border: isActive ? '1px solid var(--border-light)' : '1px solid transparent',
                  color: isActive ? 'var(--text-primary)' : 'var(--text-secondary)',
                  fontSize: 13, fontWeight: isActive ? 500 : 400,
                  boxShadow: isActive ? '0 1px 2px 0 rgb(0 0 0 / 0.04)' : 'none',
                }}>
                  <Icon name={item.icon} size={16} color={isActive ? 'var(--brand-primary)' : 'var(--text-secondary)'}/>
                  <span style={{ flex: 1 }}>{item.label}</span>
                  {item.badge && (
                    <span style={{
                      fontFamily: 'var(--font-mono)', fontSize: 10, fontWeight: 600,
                      padding: '1px 6px', borderRadius: 999,
                      background: 'var(--brand-secondary)', color: '#fff',
                    }}>{item.badge}</span>
                  )}
                </div>
              );
            })}
          </div>
        ))}
      </div>

      {/* footer: agent status */}
      <div style={{
        borderTop: '1px solid var(--border-light)',
        padding: '12px 10px 4px',
        display: 'flex', alignItems: 'center', gap: 10,
      }}>
        <div style={{ position: 'relative' }}>
          <Avatar name="Aonik AI" size={30} color="var(--brand-primary)" textColor="#fff"/>
          <span style={{
            position: 'absolute', bottom: -1, right: -1,
            width: 10, height: 10, borderRadius: 999,
            background: 'var(--success)', border: '2px solid var(--surface-inset)'
          }}/>
        </div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-primary)' }}>Orchestrator</div>
          <div style={{ fontSize: 10, color: 'var(--text-secondary)' }}>7 agents online · 3 proposals</div>
        </div>
        <span className="hover-halo"><Icon name="settings" size={14}/></span>
      </div>
    </aside>
  );
}

function TopBar({ title, breadcrumbs = [], actions }) {
  return (
    <div style={{
      height: 56, flex: 'none',
      borderBottom: '1px solid var(--border-light)',
      background: 'var(--surface)',
      display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      padding: '0 24px',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
        {breadcrumbs.map((b, i) => (
          <React.Fragment key={i}>
            <span style={{ fontSize: 13, color: i === breadcrumbs.length - 1 ? 'var(--text-primary)' : 'var(--text-secondary)', fontWeight: i === breadcrumbs.length - 1 ? 600 : 400 }}>{b}</span>
            {i < breadcrumbs.length - 1 && <Icon name="chevron" size={12} color="var(--text-tertiary)"/>}
          </React.Fragment>
        ))}
        {title && !breadcrumbs.length && (
          <span style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>{title}</span>
        )}
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
        {actions}
        <span className="hover-halo"><Icon name="bell" size={16}/></span>
        <span className="hover-halo"><Icon name="settings" size={16}/></span>
        <div style={{ width: 1, height: 24, background: 'var(--border-light)', margin: '0 4px' }}/>
        <Avatar name="Ada Okafor" size={30} color="var(--accent-violet)" textColor="#fff"/>
      </div>
    </div>
  );
}

// Right agent rail — a persistent inline AI side panel.
function AgentRail({ messages, proposals = [], thinking = false }) {
  return (
    <aside style={{
      width: 380, flex: 'none',
      borderLeft: '1px solid var(--border-light)',
      background: 'var(--surface)',
      display: 'flex', flexDirection: 'column',
    }}>
      {/* rail header */}
      <div style={{
        padding: '14px 18px',
        borderBottom: '1px solid var(--border-light)',
        display: 'flex', alignItems: 'center', gap: 10,
      }}>
        <div style={{
          width: 32, height: 32, borderRadius: 9,
          background: 'linear-gradient(135deg, var(--brand-primary) 0%, #077278 100%)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          color: '#fff',
        }}>
          <Icon name="sparkles" size={16}/>
        </div>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>Ledger Agent</div>
          <div style={{ fontSize: 11, color: 'var(--text-secondary)', display: 'flex', alignItems: 'center', gap: 6 }}>
            <span style={{ width: 6, height: 6, borderRadius: 999, background: 'var(--success)' }}/>
            reading this page · 4 tools available
          </div>
        </div>
        <span className="hover-halo"><Icon name="more" size={14}/></span>
      </div>

      {/* conversation */}
      <div className="chat-primary" style={{ flex: 1, overflowY: 'auto', padding: 18, display: 'flex', flexDirection: 'column', gap: 14 }}>
        {messages}
        {proposals.map((p, i) => <ProposalCard key={i} {...p}/>)}
        {thinking && (
          <div style={{ display: 'flex', gap: 10 }}>
            <Avatar name="L" size={24} color="var(--brand-primary)" textColor="#fff"/>
            <div style={{ padding: '8px 12px', background: 'var(--surface-inset)', borderRadius: '12px 12px 12px 4px', fontSize: 12 }}>
              <span className="shimmer">Drafting journal entry for INV-2041…</span>
            </div>
          </div>
        )}
      </div>

      {/* composer */}
      <div style={{
        borderTop: '1px solid var(--border-light)',
        padding: 14,
      }}>
        <div style={{
          background: 'var(--surface-inset)',
          border: '1px solid var(--border-light)',
          borderRadius: 10,
          padding: '10px 12px',
        }}>
          <textarea
            rows={2}
            placeholder="Ask the ledger agent…"
            style={{
              width: '100%', border: 'none', background: 'transparent', outline: 'none',
              resize: 'none', fontFamily: 'inherit', fontSize: 13, color: 'var(--text-primary)',
              padding: 0,
            }}
            defaultValue=""
          />
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 8 }}>
            <div style={{ display: 'flex', gap: 4 }}>
              <span className="hover-halo"><Icon name="at" size={14}/></span>
              <span className="hover-halo"><Icon name="upload" size={14}/></span>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ fontSize: 10, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>⌘⏎</span>
              <button className="btn btn-primary btn-sm" style={{ height: 26, padding: '0 10px' }}>
                <Icon name="send" size={12}/> Send
              </button>
            </div>
          </div>
        </div>
      </div>
    </aside>
  );
}

// ProposalCard — the signature inline "agents propose, systems apply" pattern
function ProposalCard({ agent = 'Billing', confidence = 0.94, summary, diff, reason, compact = false }) {
  return (
    <div style={{
      background: 'var(--surface)',
      border: '1px solid var(--border-light)',
      borderLeft: '3px solid var(--brand-secondary)',
      borderRadius: 10,
      padding: compact ? '12px 14px' : '14px 16px',
      display: 'flex', flexDirection: 'column', gap: 10,
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <Avatar name={agent} size={22} color="var(--brand-primary-10)" textColor="var(--brand-primary)"/>
        <span style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-primary)' }}>{agent} Agent</span>
        <span style={{ marginLeft: 'auto', fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-secondary)' }}>
          conf · {confidence.toFixed(2)}
        </span>
      </div>
      {summary && (
        <div style={{ fontSize: 13, color: 'var(--text-primary)', lineHeight: 1.5 }}>{summary}</div>
      )}
      {diff && (
        <div style={{
          background: 'var(--surface-inset)', borderRadius: 6,
          padding: '8px 10px', fontFamily: 'var(--font-mono)',
          fontSize: 11, lineHeight: 1.6, color: 'var(--text-primary)',
        }}>
          {diff.map((line, i) => (
            <div key={i} style={{
              color: line.type === 'add' ? 'var(--success)' : line.type === 'rm' ? 'var(--danger)' : 'var(--text-secondary)',
            }}>
              {line.type === 'add' ? '+ ' : line.type === 'rm' ? '- ' : '  '}{line.text}
            </div>
          ))}
        </div>
      )}
      {reason && <div style={{ fontSize: 11, color: 'var(--text-secondary)', lineHeight: 1.5 }}>{reason}</div>}
      <div style={{ display: 'flex', gap: 6, marginTop: 2 }}>
        <button className="btn btn-secondary btn-sm">Apply</button>
        <button className="btn btn-outline btn-sm">Review</button>
        <button className="btn btn-ghost btn-sm">Dismiss</button>
      </div>
    </div>
  );
}

function AppShell({ active = 'dashboard', breadcrumbs, topActions, rightRail, children }) {
  return (
    <div style={{
      display: 'flex', width: '100%', height: '100%',
      background: 'var(--background)',
      fontFamily: 'var(--font-sans)',
      color: 'var(--text-primary)',
    }}>
      <Sidebar active={active}/>
      <main style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
        <TopBar breadcrumbs={breadcrumbs} actions={topActions}/>
        <div style={{ flex: 1, overflow: 'auto' }}>
          {children}
        </div>
      </main>
      {rightRail}
    </div>
  );
}

Object.assign(window, { AppShell, Sidebar, TopBar, AgentRail, ProposalCard });
