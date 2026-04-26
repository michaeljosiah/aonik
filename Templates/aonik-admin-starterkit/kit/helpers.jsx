// Shared helpers — page header, filter bar, data table, tabs, breadcrumbs, status dots.
// All exported to window.

function PageHeader({ eyebrow, title, subtitle, actions }) {
  return (
    <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 24 }}>
      <div>
        {eyebrow && <div className="eyebrow">{eyebrow}</div>}
        <h1 style={{
          fontFamily: 'var(--font-brand)', fontSize: 24, fontWeight: 700,
          marginTop: eyebrow ? 6 : 0, letterSpacing: '-0.01em', color: 'var(--text-primary)',
        }}>{title}</h1>
        {subtitle && (
          <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 4 }}>{subtitle}</div>
        )}
      </div>
      {actions && <div style={{ display: 'flex', gap: 8, flex: 'none' }}>{actions}</div>}
    </div>
  );
}

// Segmented tabs — the chip style used throughout the app
function Tabs({ tabs, active, onChange = () => {}, counts = {} }) {
  return (
    <div style={{ display: 'flex', gap: 2, alignItems: 'center' }}>
      {tabs.map((t, i) => {
        const isActive = (active || tabs[0]) === t;
        return (
          <button key={t} onClick={() => onChange(t)}
            className="btn btn-ghost"
            style={{
              height: 30, padding: '0 12px', fontSize: 12, borderRadius: 6,
              background: isActive ? 'var(--brand-primary-10)' : 'transparent',
              color: isActive ? 'var(--brand-primary)' : 'var(--text-secondary)',
              fontWeight: isActive ? 600 : 400,
              display: 'inline-flex', alignItems: 'center', gap: 6,
            }}>
            {t}
            {counts[t] != null && (
              <span style={{
                fontFamily: 'var(--font-mono)', fontSize: 10,
                color: isActive ? 'var(--brand-secondary)' : 'var(--text-tertiary)',
                fontWeight: 600,
              }}>{counts[t]}</span>
            )}
          </button>
        );
      })}
    </div>
  );
}

// Filter bar — tabs + search + date
function FilterBar({ tabs, active, counts = {}, search = 'Filter…', extra }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10,
      padding: '10px 14px',
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 10,
    }}>
      {tabs && <Tabs tabs={tabs} active={active} counts={counts}/>}
      {tabs && <div style={{ width: 1, height: 20, background: 'var(--border-light)', margin: '0 4px' }}/>}
      <div style={{ flex: 1, position: 'relative', minWidth: 180 }}>
        <span style={{ position: 'absolute', left: 10, top: '50%', transform: 'translateY(-50%)', color: 'var(--text-tertiary)' }}>
          <Icon name="search" size={13}/>
        </span>
        <input className="input" placeholder={search}
          style={{ paddingLeft: 30, height: 30, fontSize: 12, background: 'var(--surface-inset)', border: 'none', width: '100%' }}/>
      </div>
      {extra}
      <button className="btn btn-ghost btn-sm"><Icon name="filter" size={12}/> Filters</button>
    </div>
  );
}

// Generic data table. cols: [{ key, label, w, align, render? }]; rows: array
function DataTable({ cols, rows, rowHighlight = () => false, inlineAfter = () => null, footer }) {
  const template = cols.map(c => c.w || '1fr').join(' ') + ' 40px';
  return (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 10, overflow: 'hidden',
    }}>
      <div style={{
        display: 'grid', gridTemplateColumns: template,
        padding: '10px 16px', gap: 14,
        background: 'var(--surface-inset)',
        fontSize: 10, fontWeight: 600, letterSpacing: '0.06em', textTransform: 'uppercase',
        color: 'var(--text-tertiary)',
        borderBottom: '1px solid var(--border-light)',
      }}>
        {cols.map(c => (
          <div key={c.key} style={{ textAlign: c.align || 'left' }}>{c.label}</div>
        ))}
        <div/>
      </div>
      {rows.map((r, i) => {
        const hl = rowHighlight(r);
        const after = inlineAfter(r);
        const last = i === rows.length - 1 && !footer;
        return (
          <React.Fragment key={r.id || i}>
            <div style={{
              display: 'grid', gridTemplateColumns: template,
              padding: '12px 16px', gap: 14,
              alignItems: 'center',
              borderBottom: last && !after ? 'none' : '1px solid var(--border-light)',
              background: hl || 'transparent',
            }}>
              {cols.map(c => (
                <div key={c.key} style={{
                  textAlign: c.align || 'left',
                  fontFamily: c.mono ? 'var(--font-mono)' : 'inherit',
                  fontSize: c.fontSize || 13,
                  color: 'var(--text-primary)',
                  fontWeight: c.weight || 400,
                  overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
                }}>{c.render ? c.render(r) : r[c.key]}</div>
              ))}
              <div style={{ display: 'flex', justifyContent: 'center' }}>
                <span className="hover-halo"><Icon name="more" size={14}/></span>
              </div>
            </div>
            {after}
          </React.Fragment>
        );
      })}
      {footer}
    </div>
  );
}

function TableFooter({ showing, total, page = 1, pages = 1 }) {
  return (
    <div style={{
      padding: '10px 16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center',
      background: 'var(--surface-inset)', borderTop: '1px solid var(--border-light)',
      fontSize: 11, color: 'var(--text-secondary)',
    }}>
      <div>Showing <b style={{ color: 'var(--text-primary)' }}>{showing}</b> of {total}</div>
      <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
        <button className="btn btn-ghost btn-sm" style={{ height: 24, padding: '0 6px' }}>←</button>
        <span style={{ fontFamily: 'var(--font-mono)' }}>{page} / {pages}</span>
        <button className="btn btn-ghost btn-sm" style={{ height: 24, padding: '0 6px' }}>→</button>
      </div>
    </div>
  );
}

// Status dot + text
function Status({ tone = 'success', label }) {
  const colors = {
    success: 'var(--success)',
    warning: 'var(--warning)',
    danger: 'var(--danger)',
    pending: 'var(--brand-secondary)',
    info: 'var(--brand-primary)',
    muted: 'var(--gray-400)',
  };
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 7, fontSize: 12, color: 'var(--text-secondary)' }}>
      <span style={{ width: 7, height: 7, borderRadius: 999, background: colors[tone] || colors.muted }}/>
      {label}
    </span>
  );
}

// Section label
function SectionLabel({ children, actions }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
      <div style={{
        fontSize: 10, fontWeight: 600, letterSpacing: '0.08em', textTransform: 'uppercase',
        color: 'var(--text-tertiary)',
      }}>{children}</div>
      {actions}
    </div>
  );
}

// Inline proposal row (reuses pattern from ledger.jsx)
function InlineProposal({ agent, summary, confidence, indent = 126 }) {
  return (
    <div style={{
      padding: `0 16px 14px ${indent}px`,
      borderBottom: '1px solid var(--border-light)',
      background: '#eb5c3708',
    }}>
      <div style={{
        background: 'var(--surface)',
        border: '1px solid var(--border-light)',
        borderLeft: '3px solid var(--brand-secondary)',
        borderRadius: 8, padding: '10px 14px',
        display: 'flex', alignItems: 'center', gap: 12,
      }}>
        <Avatar name={agent} size={22} color="var(--brand-primary-10)" textColor="var(--brand-primary)"/>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 12, color: 'var(--text-primary)' }}>
            <b>{agent} Agent</b> proposes: {summary}
          </div>
          <div style={{ fontSize: 10, color: 'var(--text-secondary)', marginTop: 2, fontFamily: 'var(--font-mono)' }}>
            confidence · {confidence.toFixed(2)} · based on {agent === 'Billing' ? 'reference, amount, counterparty match' : 'prior pattern match + policy fit'}
          </div>
        </div>
        <button className="btn btn-secondary btn-sm">Apply</button>
        <button className="btn btn-outline btn-sm">Review</button>
        <button className="btn btn-ghost btn-sm"><Icon name="close" size={12}/></button>
      </div>
    </div>
  );
}

// Agent avatars with colored tints
const AGENT_COLORS = ['#055a60', '#eb5c37', '#3ab795', '#7b76b6', '#0097a9', '#5facbd'];
function agentColor(name) {
  let h = 0; for (let i = 0; i < name.length; i++) h = (h * 31 + name.charCodeAt(i)) >>> 0;
  return AGENT_COLORS[h % AGENT_COLORS.length];
}

Object.assign(window, { PageHeader, Tabs, FilterBar, DataTable, TableFooter, Status, SectionLabel, InlineProposal, agentColor });
