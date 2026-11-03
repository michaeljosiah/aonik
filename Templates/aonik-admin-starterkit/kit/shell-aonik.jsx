// Aonik Admin — production shell with inline nav, bottom user profile, full TopBar

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
  // Customers sits here deliberately: parties are the universal counterparty
  // primitive, exactly as Orders is the universal verb. One registry for every
  // tenant; domain lenses (Billing, Storefront, Payabo) are TABS on the detail,
  // gated by the tenant's enabled modules — never separate customer views.
  { group: 'Transact', items: [
    { id: 'orders', label: 'Orders', icon: 'receipt', badge: 4, children: [
      { id: 'orders-list',     label: 'All orders',      icon: 'list' },
      { id: 'orders-new',      label: 'New order',       icon: 'plus' },
      { id: 'order-items',     label: 'Item monitor',    icon: 'activity', badge: 2 },
    ]},
    { id: 'customers', label: 'Customers', icon: 'users2' },
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
      // Customers moved to the top-level Transact layer — the registry is
      // universal; billing keeps only its own artifacts here.
      { id: 'invoices',   label: 'Invoices',    icon: 'invoice' },
      { id: 'accounts',   label: 'Customer accounts', icon: 'users2' },
      { id: 'collections',label: 'Collections', icon: 'arrows', badge: 3 },
      { id: 'ledger',     label: 'Ledger',      icon: 'book2', children: [
        { id: 'ledgers',        label: 'Ledgers',           icon: 'book' },
        { id: 'accounts-chart', label: 'Chart of accounts', icon: 'landmark' },
        { id: 'journal',        label: 'Journal entries',   icon: 'invoice' },
      ]},
    ]},
    // Commerce (Spec 042 sell-side) + Commerce · Make (Specs 050–057 maker-ops).
    // Deliberately two FLAT sibling parents (Spec 058 §7): the sidebar renders
    // exactly one child level, so a nested "Make" sub-group would be unreachable.
    // Badges come from the mock datasets: 3 active low-stock alerts (Open +
    // Acknowledged), 2 POs pending receipt.
    { id: 'commerce', label: 'Commerce', icon: 'cart', children: [
      { id: 'cm-overview',   label: 'Overview',        icon: 'dashboard' },
      { id: 'cm-catalog',    label: 'Catalog',         icon: 'box' },
      { id: 'cm-inventory',  label: 'Inventory',       icon: 'layers' },
      { id: 'cm-orders',     label: 'Orders & carts',  icon: 'receipt' },
      { id: 'cm-discounts',  label: 'Discounts',       icon: 'tag' },
    ]},
    { id: 'cm-make', label: 'Commerce · Make', icon: 'chef', children: [
      { id: 'cm-ingredients', label: 'Ingredients',       icon: 'wheat' },
      { id: 'cm-recipes',     label: 'Recipes',           icon: 'book' },
      { id: 'cm-planning',    label: 'Planning',          icon: 'calendar' },
      { id: 'cm-production',  label: 'Production',        icon: 'flame' },
      { id: 'cm-lowstock',    label: 'Low stock',         icon: 'alert', badge: 3 },
      { id: 'cm-suppliers',   label: 'Suppliers',         icon: 'truck' },
      { id: 'cm-pos',         label: 'Purchase orders',   icon: 'clipboard', badge: 2 },
      { id: 'cm-margin',      label: 'Margin',            icon: 'chart' },
    ]},
    // Commerce · Storefront (Specs 066–072) — the shop window: what the
    // customer can choose, read, and be promised. Third flat sibling per the
    // Spec 058 §7 one-child-level rule. Badge = content blocks awaiting review.
    { id: 'cm-storefront', label: 'Commerce · Storefront', icon: 'store', children: [
      { id: 'cs-options',  label: 'Personalisation',   icon: 'sliders' },
      { id: 'cs-content',  label: 'Product content',   icon: 'file', badge: 2 },
      { id: 'cs-boxplan',  label: 'Box plans',         icon: 'box' },
      { id: 'cs-delivery', label: 'Delivery',          icon: 'calendar' },
      { id: 'cs-merch',    label: 'Merchandising',     icon: 'stack' },
      { id: 'cs-config',   label: 'Storefront config', icon: 'cog' },
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

// ─── Profile popover — bottom-left user menu ──────────────────────────
// Items: Account · Notifications · Theme · Sign out. Closes on outside click,
// Esc, or selecting an item. Anchored above the user tile.
function ProfileMenuPopover({ anchorRect, onClose, onSelect }) {
  const ref = React.useRef(null);
  React.useEffect(() => {
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

  // Anchor above the user tile, aligned to its left.
  const POPOVER_HEIGHT_ESTIMATE = 220;
  const left = anchorRect ? anchorRect.left : 0;
  const top  = anchorRect ? anchorRect.top - POPOVER_HEIGHT_ESTIMATE : 0;

  const items = [
    { id: 'account',       label: 'Account',       icon: 'user',       hint: 'Plan, billing, profile' },
    { id: 'notifications', label: 'Notifications', icon: 'bell',       hint: '3 unread' },
    { id: 'theme',         label: 'Theme',         icon: 'moon',       trailing: 'System' },
  ];
  const danger = { id: 'signout', label: 'Sign out', icon: 'signout' };

  return (
    <div ref={ref} style={{
      position: 'fixed', left, top,
      width: 248, zIndex: 1000,
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 10, padding: 6,
      boxShadow: '0 18px 40px -10px rgb(0 0 0 / 0.22), 0 0 0 1px rgb(0 0 0 / 0.02)',
      animation: 'popoverIn 120ms ease-out',
    }}>
      {/* User identity header */}
      <div style={{
        padding: '10px 10px 12px',
        borderBottom: '1px solid var(--border-light)', marginBottom: 4,
        display: 'flex', alignItems: 'center', gap: 10,
      }}>
        <Avatar name="Oliver Chen" size={32} color="#7b76b6" textColor="#fff"/>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>Oliver Chen</div>
          <div style={{ fontSize: 11, color: 'var(--text-tertiary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>oliver@primrose.co</div>
        </div>
      </div>

      {items.map(it => (
        <div key={it.id}
          onClick={() => { onSelect?.(it.id); onClose?.(); }}
          style={{
            display: 'flex', alignItems: 'center', gap: 10,
            padding: '8px 10px', borderRadius: 6, cursor: 'pointer',
            fontSize: 12.5, color: 'var(--text-primary)',
          }}
          onMouseEnter={e => e.currentTarget.style.background = 'rgba(0,0,0,0.04)'}
          onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
        >
          <Icon name={it.icon} size={14} color="var(--text-secondary)"/>
          <span style={{ flex: 1 }}>{it.label}</span>
          {it.hint && <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>{it.hint}</span>}
          {it.trailing && (
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4, fontSize: 10.5, color: 'var(--text-tertiary)' }}>
              {it.trailing} <Icon name="chevron" size={11}/>
            </span>
          )}
        </div>
      ))}

      <div style={{ borderTop: '1px solid var(--border-light)', marginTop: 4, paddingTop: 4 }}>
        <div
          onClick={() => { onSelect?.(danger.id); onClose?.(); }}
          style={{
            display: 'flex', alignItems: 'center', gap: 10,
            padding: '8px 10px', borderRadius: 6, cursor: 'pointer',
            fontSize: 12.5, color: 'var(--text-primary)',
          }}
          onMouseEnter={e => e.currentTarget.style.background = 'rgba(0,0,0,0.04)'}
          onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
        >
          <Icon name={danger.icon} size={14} color="var(--text-secondary)"/>
          <span style={{ flex: 1 }}>{danger.label}</span>
        </div>
      </div>
    </div>
  );
}

// shadcn sidebar anatomy: 16rem expanded, 3rem rail and inline collapsible groups.
function AonikSidebar({ active = 'myspace', collapsed = false, showUpgrade = false, onProfileSelect, profileMenuOpen = false }) {
  const [profileOpen, setProfileOpen] = React.useState(profileMenuOpen);
  const [profileRect, setProfileRect] = React.useState(null);
  const profileTileRef = React.useRef(null);
  React.useEffect(() => {
    if (profileMenuOpen && profileTileRef.current) {
      setProfileRect(profileTileRef.current.getBoundingClientRect());
      setProfileOpen(true);
    }
  }, [profileMenuOpen]);
  const containsActive = item => item.id === active || item.children?.some(containsActive);
  const renderItem = item => {
    const selected = item.id === active;
    if (item.children?.length) return (
      <details key={item.id} open={containsActive(item) || undefined} className="kit-nav-parent">
        <summary title={item.label}><Icon name={window.AonikMenuIcons.legacyItem(item.id)} size={16}/>{!collapsed && <><span>{item.label}</span><Icon name="chevron" size={12}/></>}</summary>
        <div className="kit-nav-children">{item.children.map(renderItem)}</div>
      </details>
    );
    return <button type="button" key={item.id} className={'kit-nav-item' + (selected ? ' is-active' : '')} aria-current={selected ? 'page' : undefined} title={item.label}>
      <Icon name={window.AonikMenuIcons.legacyItem(item.id)} size={16}/>{!collapsed && <><span>{item.label}</span>{item.badge != null && <small>{item.badge}</small>}</>}
    </button>;
  };
  return (
    <aside className={'kit-sidebar' + (collapsed ? ' is-collapsed' : '')}>
      <div className="kit-sidebar-brand">{collapsed ? <AonikMark size={24}/> : <AonikWordmark size={20}/>}</div>
      {!collapsed && <div className="kit-tenant"><Avatar name="Primrose Ltd" size={28}/><div><b>Primrose Ltd</b><small>Business workspace</small></div><Icon name="chevdown" size={12}/></div>}
      {!collapsed && <input className="input" aria-label="Search design navigation" placeholder="Search or ask…" style={{height:32,fontSize:12,marginBottom:14}}/>}
      <nav className="kit-sidebar-nav" aria-label="Design kit navigation">{SIDEBAR_NAV.map(group => <section key={group.group}>{!collapsed && <div className="kit-nav-label">{group.group}</div>}{group.items.map(renderItem)}</section>)}</nav>
      {showUpgrade && !collapsed && <div className="kit-upgrade"><b>Launch plan</b><small>168 / 200 AI messages used</small><button className="btn btn-outline btn-sm">Upgrade plan</button></div>}
      <div className="kit-sidebar-footer"><button ref={profileTileRef} type="button" className="kit-user" aria-label="Open user menu" onClick={e => { setProfileRect(e.currentTarget.getBoundingClientRect()); setProfileOpen(value => !value); }}>
        <Avatar name="Oliver Chen" size={30}/>{!collapsed && <><span><b>Oliver Chen</b><small>Workspace administrator</small></span><Icon name="chevdown" size={12}/></>}
      </button>{profileOpen && <ProfileMenuPopover anchorRect={profileRect} onClose={() => setProfileOpen(false)} onSelect={id => onProfileSelect?.(id)}/>}</div>
    </aside>
  );
}

// Contextual upgrade pill for the top bar — shown on free tier or near limits.
// Routes to Account → Plan in interactive contexts.
function UpgradePill({ label = 'Upgrade to Growth', onClick }) {
  return (
    <button onClick={onClick} style={{
      display: 'inline-flex', alignItems: 'center', gap: 6,
      height: 28, padding: '0 10px',
      background: 'var(--brand-secondary, #eb5c37)', color: '#fff',
      border: 'none', borderRadius: 999, cursor: 'pointer',
      fontSize: 11.5, fontWeight: 600, fontFamily: 'var(--font-sans)',
      boxShadow: '0 1px 2px rgba(0,0,0,0.08)',
    }}>
      <Icon name="sparkles" size={11} color="#fff"/>
      {label}
    </button>
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
          <img src={globalThis.AonikAgentPortraits.resolve('aonik')} alt="AONIK · AI assistant" style={{ width: 32, height: 32, borderRadius: '50%', objectFit: 'cover' }}/>
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
        {globalThis.AonikAgentPortraits.resolve(agent) ? <img src={globalThis.AonikAgentPortraits.resolve(agent)} alt={`${agent} · AI agent`} style={{ width: 22, height: 22, borderRadius: '50%', objectFit: 'cover' }}/> : <Avatar name={agent} size={22} color="var(--brand-primary-10)" textColor="var(--brand-primary)"/>}
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
        <button className="btn btn-agent btn-sm">Apply</button>
        <button className="btn btn-outline btn-sm">Review</button>
        <button className="btn btn-ghost btn-sm">Dismiss</button>
      </div>
    </div>
  );
}

function AonikShell({ active, breadcrumbs, topActions, children, sidebarCollapsed = false, rightRail, onAskAonik, showUpgrade = false, onProfileSelect, profileMenuOpen = false }) {
  return (
    <div style={{
      display: 'flex', width: '100%', height: '100%',
      background: 'var(--background)', fontFamily: 'var(--font-sans)', color: 'var(--text-primary)',
    }}>
      <AonikSidebar
        active={active}
        collapsed={sidebarCollapsed}
        showUpgrade={showUpgrade}
        onProfileSelect={onProfileSelect}
        profileMenuOpen={profileMenuOpen}
      />
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

Object.assign(window, { AonikShell, AonikSidebar, AonikTopBar, AgentRail, ProposalCard, SIDEBAR_NAV, UpgradePill });
