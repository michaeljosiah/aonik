// Aonik Admin — production shell with flyout nav, bottom user profile, full TopBar

// ─── Nav model ─────────────────────────────────────────────────────────
// Orders is the universal "verb" primitive — any line item can ride on it
// (bill payment, money transfer, future product). Bill Payments / Remittances
// / Billing / Personal Finance are "noun" primitives — catalog & config for
// what can be in an order. Approvals is a cross-cutting queue across types.
const SIDEBAR_NAV = [
  { group: 'Home', items: [
    { id: 'myspace', label: 'My Space', icon: 'home' },
  ]},

  // ── TRANSACT — universal verb layer (cross-cutting) ────────────────
  { group: 'Transact', items: [
    { id: 'orders', label: 'Orders', icon: 'receipt', badge: 4, children: [
      { id: 'orders-list',     label: 'All orders',      icon: 'list' },
      { id: 'orders-new',      label: 'New order',       icon: 'plus' },
      { id: 'order-items',     label: 'Item monitor',    icon: 'activity', badge: 2 },
    ]},
    { id: 'approvals', label: 'Approvals', icon: 'clipcheck', badge: 7 },
  ]},

  // ── PRODUCTS — noun layer: catalog + config per primitive ──────────
  { group: 'Products', items: [
    { id: 'bill-payments', label: 'Bill Payments', icon: 'invoice', children: [
      { id: 'billers',          label: 'Billers',         icon: 'building' },
      { id: 'biller-categories',label: 'Categories',      icon: 'tag' },
      { id: 'bill-history',     label: 'Recent activity', icon: 'list' },
    ]},
    { id: 'remittances', label: 'Remittances', icon: 'globe2', children: [
      { id: 'corridors',  label: 'Corridors',  icon: 'route' },
      { id: 'network',    label: 'Partners',   icon: 'network' },
      { id: 'pricing',    label: 'FX & Rates', icon: 'arrows' },
      { id: 'remit-history', label: 'Recent activity', icon: 'list' },
    ]},
    { id: 'billing', label: 'Billing', icon: 'book', children: [
      { id: 'invoices',   label: 'Invoices',    icon: 'invoice' },
      { id: 'customers',  label: 'Customers',   icon: 'building' },
      { id: 'accounts',   label: 'Customer accounts', icon: 'users2' },
      { id: 'collections',label: 'Collections', icon: 'arrows', badge: 3 },
      { id: 'ledger',     label: 'Ledger',      icon: 'book2', children: [
        { id: 'ledgers',        label: 'Ledgers',           icon: 'book' },
        { id: 'accounts-chart', label: 'Chart of accounts', icon: 'landmark' },
        { id: 'journal',        label: 'Journal entries',   icon: 'invoice' },
      ]},
    ]},
    { id: 'personal-finance', label: 'Personal Finance', icon: 'bank', children: [
      { id: 'wallets',  label: 'Wallets',   icon: 'bank' },
      { id: 'savings',  label: 'Savings',   icon: 'chart' },
      { id: 'transfers',label: 'Transfers', icon: 'payout' },
    ]},
  ]},

  // ── PLATFORM — shared infra ────────────────────────────────────────
  { group: 'Platform', items: [
    { id: 'compliance', label: 'Compliance', icon: 'clipcheck', badge: 2 },
    { id: 'ai', label: 'AI & Agents', icon: 'sparkles', children: [
      { id: 'playground', label: 'Playground', icon: 'bot' },
      { id: 'agents',     label: 'Agents',     icon: 'sparkles' },
      { id: 'aitasks',    label: 'Tasks',      icon: 'clipcheck', badge: 3 },
      { id: 'policies',   label: 'Policies',   icon: 'shield' },
      { id: 'usage',      label: 'Usage',      icon: 'chart' },
    ]},
    { id: 'obs', label: 'Observability', icon: 'activity', children: [
      { id: 'obs-overview', label: 'Overview',  icon: 'chart' },
      { id: 'obs-traces',   label: 'Traces',    icon: 'gitbranch' },
      { id: 'obs-logs',     label: 'Logs',      icon: 'terminal' },
      { id: 'obs-audit',    label: 'Audit Log', icon: 'verified' },
    ]},
    { id: 'settings', label: 'Settings',  icon: 'settings' },
    { id: 'tenants',  label: 'Tenants',   icon: 'building' },
    { id: 'system',   label: 'System',    icon: 'wrench' },
  ]},
];

// ─── Click popover submenu — opens when user clicks a parent menu item ─────
// Closes on outside click, Esc, or selecting a child. Anchored to the right of
// the parent row at its top offset.
function NavPopover({ parent, isActiveChild, anchorRect, onClose, onSelect }) {
  const ref = React.useRef(null);
  React.useEffect(() => {
    // Defer attaching the outside-click listener by one tick so the click that
    // *opened* the popover doesn't immediately close it.
    const t = setTimeout(() => {
      const handleClick = e => {
        if (ref.current && !ref.current.contains(e.target)) onClose?.();
      };
      const handleKey = e => { if (e.key === 'Escape') onClose?.(); };
      document.addEventListener('mousedown', handleClick);
      document.addEventListener('keydown', handleKey);
      ref.current.__cleanup = () => {
        document.removeEventListener('mousedown', handleClick);
        document.removeEventListener('keydown', handleKey);
      };
    }, 0);
    return () => {
      clearTimeout(t);
      if (ref.current && ref.current.__cleanup) ref.current.__cleanup();
    };
  }, [onClose]);

  // Anchor to the trigger's bounding rect using fixed positioning so the
  // popover escapes any `overflow: hidden` ancestor (artboard frame, sidebar
  // scroll container, etc.).
  const left = anchorRect ? anchorRect.right + 8 : 0;
  const top = anchorRect ? anchorRect.top : 0;

  return (
    <div ref={ref} style={{
      position: 'fixed', left, top,
      minWidth: 232, zIndex: 1000,
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 10, padding: 6,
      boxShadow: '0 18px 40px -10px rgb(0 0 0 / 0.22), 0 0 0 1px rgb(0 0 0 / 0.02)',
      animation: 'popoverIn 120ms ease-out',
    }}>
      {/* Pointer */}
      <span style={{
        position: 'absolute', left: -5, top: 14, width: 9, height: 9,
        background: 'var(--surface)', borderLeft: '1px solid var(--border-light)',
        borderBottom: '1px solid var(--border-light)', transform: 'rotate(45deg)',
      }}/>

      <div style={{
        padding: '8px 10px 10px', borderBottom: '1px solid var(--border-light)', marginBottom: 4,
        display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8,
      }}>
        <span style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <Icon name={parent.icon} size={13} color="var(--brand-primary)"/>
          <span style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{parent.label}</span>
        </span>
        {parent.badge != null && (
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, fontWeight: 600,
            padding: '1px 6px', borderRadius: 999, background: 'var(--brand-secondary)', color: '#fff' }}>{parent.badge}</span>
        )}
      </div>
      {parent.children.map(c => {
        const a = isActiveChild(c.id);
        const hasGrand = c.children && c.children.length;
        return (
          <div key={c.id}
            onClick={() => { onSelect?.(c); onClose?.(); }}
            style={{
              display: 'flex', alignItems: 'center', gap: 10,
              padding: '8px 10px', borderRadius: 6, cursor: 'pointer',
              fontSize: 12.5,
              background: a ? 'var(--brand-primary-10)' : 'transparent',
              color: a ? 'var(--brand-primary)' : 'var(--text-primary)',
              fontWeight: a ? 600 : 400,
            }}
            onMouseEnter={e => { if (!a) e.currentTarget.style.background = 'rgba(0,0,0,0.04)'; }}
            onMouseLeave={e => { if (!a) e.currentTarget.style.background = 'transparent'; }}
          >
            <Icon name={c.icon} size={14} color={a ? 'var(--brand-primary)' : 'var(--text-secondary)'}/>
            <span style={{ flex: 1 }}>{c.label}</span>
            {c.badge != null && (
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 9.5, fontWeight: 600,
                padding: '1px 5px', borderRadius: 999, background: 'var(--brand-secondary)', color: '#fff' }}>{c.badge}</span>
            )}
            {hasGrand && <Icon name="chevron" size={11} color="var(--text-tertiary)"/>}
          </div>
        );
      })}
    </div>
  );
}

function AonikSidebar({ active = 'myspace', collapsed = false }) {
  const [openId, setOpenId] = React.useState(null);
  const [openRect, setOpenRect] = React.useState(null);

  const findActive = () => {
    for (const g of SIDEBAR_NAV) for (const i of g.items) {
      if (i.id === active) return { parent: null, child: null };
      if (i.children) for (const c of i.children) if (c.id === active) return { parent: i.id, child: c.id };
    }
    return {};
  };
  const loc = findActive();
  const w = collapsed ? 62 : 240;
  const isActiveChild = id => loc.child === id;

  return (
    <aside style={{
      width: w, flex: 'none', position: 'relative',
      background: 'var(--surface-inset)',
      borderRight: '1px solid var(--border-light)',
      display: 'flex', flexDirection: 'column',
      padding: collapsed ? '14px 8px' : '14px 12px', gap: 4,
      transition: 'width 180ms ease',
    }}>
      {/* brand */}
      <div style={{ padding: '8px 10px 14px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        {collapsed ? <AonikMark size={22}/> : <AonikWordmark size={19}/>}
        {!collapsed && <span className="hover-halo"><Icon name="chevron" size={14}/></span>}
      </div>

      {/* workspace switcher */}
      {!collapsed && (
        <div style={{
          display: 'flex', alignItems: 'center', gap: 10,
          padding: '7px 10px', borderRadius: 8,
          background: 'var(--surface)', border: '1px solid var(--border-light)',
          marginBottom: 12,
        }}>
          <Avatar name="Primrose Logistics" size={24} color="#055a60" textColor="#fff"/>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>Primrose Logistics</div>
            <div style={{ fontSize: 10, color: 'var(--text-secondary)' }}>Prod · NGN · USD · GBP</div>
          </div>
          <Icon name="chevdown" size={12} color="var(--text-tertiary)"/>
        </div>
      )}

      {/* search */}
      {!collapsed && (
        <div style={{ position: 'relative', marginBottom: 8 }}>
          <span style={{ position: 'absolute', left: 10, top: '50%', transform: 'translateY(-50%)', color: 'var(--text-tertiary)' }}>
            <Icon name="search" size={14}/>
          </span>
          <input className="input" placeholder="Search or ask…" style={{ paddingLeft: 32, height: 32, fontSize: 13, background: 'var(--surface)' }}/>
          <span style={{ position: 'absolute', right: 8, top: '50%', transform: 'translateY(-50%)', fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)', background: 'var(--surface-inset)', padding: '2px 6px', borderRadius: 4, border: '1px solid var(--border-light)' }}>⌘K</span>
        </div>
      )}

      {/* nav */}
      <div style={{ overflowY: 'auto', flex: 1, marginTop: 4 }}>
        {SIDEBAR_NAV.map(group => (
          <div key={group.group} style={{ marginBottom: 10 }}>
            {!collapsed && (
              <div style={{ fontSize: 10, fontWeight: 600, letterSpacing: '0.08em', textTransform: 'uppercase',
                color: 'var(--text-tertiary)', padding: '6px 10px 4px' }}>{group.group}</div>
            )}
            {group.items.map(item => {
              const hasChildren = item.children && item.children.length;
              const isActive = item.id === active || loc.parent === item.id;
              const isOpen = openId === item.id;
              return (
                <div key={item.id} style={{ position: 'relative' }}>
                  <div
                    onClick={e => {
                      if (hasChildren) {
                        if (isOpen) {
                          setOpenId(null);
                        } else {
                          const rect = e.currentTarget.getBoundingClientRect();
                          setOpenId(item.id);
                          setOpenRect(rect);
                        }
                      }
                    }}
                    style={{
                      display: 'flex', alignItems: 'center', gap: collapsed ? 0 : 10,
                      padding: collapsed ? '9px 0' : '7px 10px',
                      justifyContent: collapsed ? 'center' : 'flex-start',
                      borderRadius: 8, cursor: 'pointer',
                      background: (isActive || isOpen) ? 'var(--surface)' : 'transparent',
                      border: (isActive || isOpen) ? '1px solid var(--border-light)' : '1px solid transparent',
                      color: isActive ? 'var(--text-primary)' : 'var(--text-secondary)',
                      fontSize: 13, fontWeight: isActive ? 500 : 400,
                      boxShadow: (isActive || isOpen) ? '0 1px 2px 0 rgb(0 0 0 / 0.04)' : 'none',
                      transition: 'background 120ms ease, border-color 120ms ease',
                    }}
                    onMouseEnter={e => { if (!isActive && !isOpen) e.currentTarget.style.background = 'rgba(0,0,0,0.03)'; }}
                    onMouseLeave={e => { if (!isActive && !isOpen) e.currentTarget.style.background = 'transparent'; }}
                  >
                    <Icon name={item.icon} size={16} color={isActive ? 'var(--brand-primary)' : 'var(--text-secondary)'}/>
                    {!collapsed && <span style={{ flex: 1 }}>{item.label}</span>}
                    {!collapsed && item.badge != null && (
                      <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, fontWeight: 600,
                        padding: '1px 6px', borderRadius: 999, background: 'var(--brand-secondary)', color: '#fff' }}>{item.badge}</span>
                    )}
                    {!collapsed && hasChildren && (
                      <Icon name="chevron" size={11} color={isOpen ? 'var(--brand-primary)' : 'var(--text-tertiary)'}/>
                    )}
                    {/* Collapsed-mode indicator dot if there are children */}
                    {collapsed && hasChildren && (
                      <span style={{ position: 'absolute', bottom: 4, right: 6, width: 4, height: 4, borderRadius: 999,
                        background: isActive ? 'var(--brand-primary)' : 'var(--text-tertiary)' }}/>
                    )}
                  </div>
                  {hasChildren && isOpen && (
                    <NavPopover
                      parent={item}
                      isActiveChild={isActiveChild}
                      anchorRect={openRect}
                      onClose={() => setOpenId(null)}
                      onSelect={() => {}}
                    />
                  )}
                </div>
              );
            })}
          </div>
        ))}
      </div>

      {/* bottom user profile — matches source */}
      <div style={{ borderTop: '1px solid var(--border-light)', paddingTop: 10, marginTop: 4 }}>
        {collapsed ? (
          <div style={{ display: 'flex', justifyContent: 'center', padding: '4px 0' }}>
            <Avatar name="Oliver Chen" size={30} color="#7b76b6" textColor="#fff"/>
          </div>
        ) : (
          <div style={{
            display: 'flex', alignItems: 'center', gap: 10,
            padding: '8px 8px', borderRadius: 8, cursor: 'pointer',
            background: 'var(--surface)', border: '1px solid var(--border-light)',
          }}>
            <div style={{ position: 'relative' }}>
              <Avatar name="Oliver Chen" size={32} color="#7b76b6" textColor="#fff"/>
              <span style={{ position: 'absolute', bottom: -1, right: -1, width: 10, height: 10, borderRadius: 999, background: 'var(--success)', border: '2px solid var(--surface)' }}/>
            </div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>Oliver Chen</div>
              <div style={{ fontSize: 10, color: 'var(--text-tertiary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>oliver@primrose.co</div>
            </div>
            <Icon name="chevdown" size={12} color="var(--text-tertiary)"/>
          </div>
        )}
      </div>
    </aside>
  );
}

// ─── TopBar with source-matching right-side icons ──────────────────
function AonikTopBar({ breadcrumbs = [], actions, onAskAonik }) {
  return (
    <div style={{
      height: 56, flex: 'none',
      borderBottom: '1px solid var(--border-light)',
      background: 'var(--surface)',
      display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      padding: '0 20px', gap: 16,
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
        {breadcrumbs.map((b, i) => (
          <React.Fragment key={i}>
            <span style={{ fontSize: 13,
              color: i === breadcrumbs.length - 1 ? 'var(--text-primary)' : 'var(--text-secondary)',
              fontWeight: i === breadcrumbs.length - 1 ? 600 : 400 }}>{b}</span>
            {i < breadcrumbs.length - 1 && <Icon name="chevron" size={12} color="var(--text-tertiary)"/>}
          </React.Fragment>
        ))}
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
        {actions}
        <button className="btn btn-outline btn-sm" onClick={onAskAonik} style={{ height: 30, padding: '0 10px', gap: 6 }}>
          <Icon name="sparkles" size={12} color="var(--brand-primary)"/>
          Ask Aonik
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)', marginLeft: 4, padding: '1px 5px', borderRadius: 3, background: 'var(--surface-inset)', border: '1px solid var(--border-light)' }}>⌘/</span>
        </button>
        <div style={{ width: 1, height: 20, background: 'var(--border-light)', margin: '0 6px' }}/>
        <span className="hover-halo" title="Help">
          <Icon name="help" size={16} color="var(--text-secondary)"/>
        </span>
        <span className="hover-halo" title="Toggle fullscreen">
          <Icon name="fullscreen" size={16} color="var(--text-secondary)"/>
        </span>
        <span className="hover-halo" title="Inbox" style={{ position: 'relative' }}>
          <Icon name="inbox" size={16} color="var(--text-secondary)"/>
          <span style={{ position: 'absolute', top: 4, right: 4, width: 6, height: 6, borderRadius: 999, background: 'var(--brand-secondary)', border: '1.5px solid var(--surface)' }}/>
        </span>
        <span className="hover-halo" title="Notifications" style={{ position: 'relative' }}>
          <Icon name="bell" size={16} color="var(--text-secondary)"/>
          <span style={{ position: 'absolute', top: 2, right: 2, minWidth: 14, height: 14, padding: '0 3px', borderRadius: 999, background: 'var(--danger)', color: '#fff', fontFamily: 'var(--font-mono)', fontSize: 9, fontWeight: 700, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', border: '1.5px solid var(--surface)' }}>3</span>
        </span>
        <span className="hover-halo" title="Settings">
          <Icon name="settings" size={16} color="var(--text-secondary)"/>
        </span>
      </div>
    </div>
  );
}

// Right agent rail (unchanged shape — accepts children for cards)
function AgentRail({ messages, proposals = [], thinking = false, children }) {
  return (
    <aside style={{
      width: 380, flex: 'none',
      borderLeft: '1px solid var(--border-light)',
      background: 'var(--surface)',
      display: 'flex', flexDirection: 'column',
    }}>
      <div style={{
        padding: '14px 18px', borderBottom: '1px solid var(--border-light)',
        display: 'flex', alignItems: 'center', gap: 10,
      }}>
        <div style={{
          width: 32, height: 32, borderRadius: 9,
          background: 'linear-gradient(135deg, var(--brand-primary) 0%, #077278 100%)',
          display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#fff',
        }}>
          <Icon name="sparkles" size={16}/>
        </div>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>Ask Aonik</div>
          <div style={{ fontSize: 11, color: 'var(--text-secondary)', display: 'flex', alignItems: 'center', gap: 6 }}>
            <span style={{ width: 6, height: 6, borderRadius: 999, background: 'var(--success)' }}/>
            reading this page · 4 tools available
          </div>
        </div>
        <span className="hover-halo"><Icon name="more" size={14}/></span>
      </div>

      <div style={{ flex: 1, overflowY: 'auto', padding: 18, display: 'flex', flexDirection: 'column', gap: 14 }}>
        {messages}
        {children}
        {proposals.map((p, i) => <ProposalCard key={i} {...p}/>)}
        {thinking && (
          <div style={{ display: 'flex', gap: 10 }}>
            <Avatar name="L" size={24} color="var(--brand-primary)" textColor="#fff"/>
            <div style={{ padding: '8px 12px', background: 'var(--surface-inset)', borderRadius: '12px 12px 12px 4px', fontSize: 12 }}>
              <span className="shimmer">Working on it…</span>
            </div>
          </div>
        )}
      </div>

      <div style={{ borderTop: '1px solid var(--border-light)', padding: 14 }}>
        <div style={{
          background: 'var(--surface-inset)', border: '1px solid var(--border-light)',
          borderRadius: 10, padding: '10px 12px',
        }}>
          <textarea rows={2} placeholder="Ask Aonik anything about this page…"
            style={{ width: '100%', border: 'none', background: 'transparent', outline: 'none',
              resize: 'none', fontFamily: 'inherit', fontSize: 13, color: 'var(--text-primary)', padding: 0 }}
            defaultValue=""/>
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

function ProposalCard({ agent = 'Billing', confidence = 0.94, summary, diff, reason, compact = false }) {
  return (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderLeft: '3px solid var(--brand-secondary)', borderRadius: 10,
      padding: compact ? '12px 14px' : '14px 16px',
      display: 'flex', flexDirection: 'column', gap: 10,
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <Avatar name={agent} size={22} color="var(--brand-primary-10)" textColor="var(--brand-primary)"/>
        <span style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-primary)' }}>{agent} Agent</span>
        <span style={{ marginLeft: 'auto', fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-secondary)' }}>conf · {confidence.toFixed(2)}</span>
      </div>
      {summary && <div style={{ fontSize: 13, color: 'var(--text-primary)', lineHeight: 1.5 }}>{summary}</div>}
      {diff && (
        <div style={{ background: 'var(--surface-inset)', borderRadius: 6, padding: '8px 10px', fontFamily: 'var(--font-mono)', fontSize: 11, lineHeight: 1.6, color: 'var(--text-primary)' }}>
          {diff.map((line, i) => (
            <div key={i} style={{ color: line.type === 'add' ? 'var(--success)' : line.type === 'rm' ? 'var(--danger)' : 'var(--text-secondary)' }}>
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

function AonikShell({ active, breadcrumbs, topActions, children, sidebarCollapsed = false, rightRail, onAskAonik }) {
  return (
    <div style={{
      display: 'flex', width: '100%', height: '100%',
      background: 'var(--background)', fontFamily: 'var(--font-sans)', color: 'var(--text-primary)',
    }}>
      <AonikSidebar active={active} collapsed={sidebarCollapsed}/>
      <main style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
        <AonikTopBar breadcrumbs={breadcrumbs} actions={topActions} onAskAonik={onAskAonik}/>
        <div style={{ flex: 1, overflow: 'auto' }}>
          {children}
        </div>
      </main>
      {rightRail}
    </div>
  );
}

Object.assign(window, { AonikShell, AonikSidebar, AonikTopBar, AgentRail, ProposalCard, SIDEBAR_NAV });
