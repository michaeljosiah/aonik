// Frontend Tool cards — the six display tools from pages/ai/playground/frontendTools.ts
// Exported: ToolCardFx, ToolCardBudget, ToolCardPie, ToolCardAutopilot,
//           ToolCardConfirm, ToolCardOptions, ToolCallBadge (collapsed row).
// All consume the same shape agents emit over the wire.

// ─── Shared frame for every tool card ──────────────────────────────
function ToolFrame({ toolName, status = 'completed', children, compact = false }) {
  const statusDot = {
    streaming:  'var(--brand-primary)',
    pending:    'var(--brand-primary)',
    executing:  'var(--brand-primary)',
    completed:  'var(--success)',
    error:      'var(--danger)',
    'awaiting-approval':  'var(--brand-secondary)',
    'awaiting-selection': 'var(--brand-secondary)',
  }[status] || 'var(--gray-400)';
  const isActive = status === 'streaming' || status === 'executing' || status === 'pending';

  return (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 10, overflow: 'hidden',
    }}>
      <div style={{
        display: 'flex', alignItems: 'center', gap: 8,
        padding: '8px 12px', borderBottom: '1px solid var(--border-light)',
        background: 'var(--surface-inset)',
      }}>
        <span style={{ width: 6, height: 6, borderRadius: 999, background: statusDot, flex: 'none',
          animation: isActive ? 'pulse 1.2s ease-in-out infinite' : 'none' }}/>
        <span style={{
          fontFamily: 'var(--font-mono)', fontSize: 11, fontWeight: 500,
          color: 'var(--text-primary)', flex: 1,
        }} className={isActive ? 'shimmer' : ''}>{toolName}</span>
        <Icon name="sparkles" size={11} color="var(--text-tertiary)"/>
      </div>
      <div style={{ padding: compact ? 12 : 14 }}>{children}</div>
    </div>
  );
}

// Collapsed tool-call pill — matches ChatMessageList.tsx ToolCallCard header-only state
function ToolCallBadge({ name, status = 'completed', ms }) {
  const color = {
    streaming: 'var(--brand-primary)', pending: 'var(--brand-primary)', executing: 'var(--brand-primary)',
    completed: 'var(--success)', error: 'var(--danger)',
  }[status] || 'var(--gray-400)';
  const isActive = status === 'streaming' || status === 'executing' || status === 'pending';
  return (
    <div style={{
      display: 'inline-flex', alignItems: 'center', gap: 8,
      padding: '5px 10px', borderRadius: 6,
      background: 'var(--surface-inset)', border: '1px solid var(--border-light)',
      fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)',
    }}>
      <span style={{ width: 6, height: 6, borderRadius: 999, background: color,
        animation: isActive ? 'pulse 1.2s ease-in-out infinite' : 'none' }}/>
      <span className={isActive ? 'shimmer' : ''} style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{name}</span>
      {ms && <span style={{ color: 'var(--text-tertiary)' }}>{ms}</span>}
      {status === 'completed' && <Icon name="check" size={11} color="var(--success)"/>}
    </div>
  );
}

// ─── display_fx_rate_chart ─────────────────────────────────────────
function ToolCardFx({ base = 'GBP', target = 'NGN', rates = [], signal = 'buy', signalReason }) {
  const min = Math.min(...rates.map(r => r.rate));
  const max = Math.max(...rates.map(r => r.rate));
  const range = max - min || 1;
  const w = 280, h = 80;
  const pts = rates.map((r, i) => {
    const x = (i / Math.max(rates.length - 1, 1)) * w;
    const y = h - ((r.rate - min) / range) * (h - 10) - 5;
    return `${x},${y}`;
  }).join(' ');

  const signalColor = { buy: 'var(--success)', hold: 'var(--warning)', wait: 'var(--danger)' }[signal];
  const signalLabel = { buy: 'BUY', hold: 'HOLD', wait: 'WAIT' }[signal];
  const current = rates[rates.length - 1]?.rate;
  const prev = rates[0]?.rate;
  const delta = current && prev ? ((current - prev) / prev) * 100 : 0;

  return (
    <ToolFrame toolName="display_fx_rate_chart">
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: 10 }}>
        <div>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)', letterSpacing: '0.05em' }}>
            {base}/{target}
          </div>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: 22, fontWeight: 600, color: 'var(--text-primary)', marginTop: 2 }}>
            {current?.toLocaleString(undefined, { maximumFractionDigits: 2 })}
          </div>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: delta >= 0 ? 'var(--success)' : 'var(--danger)', marginTop: 1 }}>
            {delta >= 0 ? '▲' : '▼'} {Math.abs(delta).toFixed(2)}% · 30d
          </div>
        </div>
        <div style={{
          padding: '4px 10px', borderRadius: 6,
          background: signalColor + '22', color: signalColor,
          fontFamily: 'var(--font-mono)', fontSize: 11, fontWeight: 700, letterSpacing: '0.06em',
          display: 'inline-flex', alignItems: 'center', gap: 4, height: 'fit-content',
        }}>
          <span style={{ width: 6, height: 6, borderRadius: 999, background: signalColor }}/>
          {signalLabel}
        </div>
      </div>
      <svg width="100%" height={h} viewBox={`0 0 ${w} ${h}`} preserveAspectRatio="none" style={{ display: 'block' }}>
        <defs>
          <linearGradient id="fx-grad" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={signalColor} stopOpacity="0.25"/>
            <stop offset="100%" stopColor={signalColor} stopOpacity="0"/>
          </linearGradient>
        </defs>
        <polyline points={`0,${h} ${pts} ${w},${h}`} fill="url(#fx-grad)"/>
        <polyline points={pts} fill="none" stroke={signalColor} strokeWidth="1.5" strokeLinecap="round"/>
      </svg>
      {signalReason && (
        <div style={{ marginTop: 10, fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>{signalReason}</div>
      )}
    </ToolFrame>
  );
}

// ─── display_budget_breakdown ──────────────────────────────────────
function ToolCardBudget({ period, totalBudget, totalSpent, currency = 'GBP', categories = [] }) {
  const pct = Math.round((totalSpent / totalBudget) * 100);
  const fmt = n => new Intl.NumberFormat('en-US', { style: 'currency', currency, maximumFractionDigits: 0 }).format(n);
  return (
    <ToolFrame toolName="display_budget_breakdown">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', marginBottom: 6 }}>
        <div>
          <div style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{period}</div>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: 20, fontWeight: 600, color: 'var(--text-primary)', marginTop: 2 }}>
            {fmt(totalSpent)}<span style={{ color: 'var(--text-tertiary)', fontSize: 12, fontWeight: 400 }}> / {fmt(totalBudget)}</span>
          </div>
        </div>
        <div style={{
          fontFamily: 'var(--font-mono)', fontSize: 11, fontWeight: 600,
          color: pct > 100 ? 'var(--danger)' : pct > 90 ? 'var(--warning)' : 'var(--success)',
        }}>{pct}%</div>
      </div>
      <div style={{ height: 4, background: 'var(--surface-inset)', borderRadius: 999, overflow: 'hidden', marginBottom: 12 }}>
        <div style={{
          width: Math.min(pct, 100) + '%', height: '100%',
          background: pct > 100 ? 'var(--danger)' : pct > 90 ? 'var(--warning)' : 'var(--success)',
        }}/>
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        {categories.map((c, i) => {
          const p = Math.round((c.spent / c.budgeted) * 100);
          const tone = c.status === 'over' ? 'var(--danger)' : c.status === 'on_track' ? 'var(--warning)' : 'var(--success)';
          return (
            <div key={i} style={{ display: 'grid', gridTemplateColumns: '1fr auto auto', gap: 10, alignItems: 'center' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, minWidth: 0 }}>
                <span style={{ width: 6, height: 6, borderRadius: 999, background: tone, flex: 'none' }}/>
                <span style={{ fontSize: 12, color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{c.name}</span>
              </div>
              <div style={{ width: 64, height: 3, background: 'var(--surface-inset)', borderRadius: 999, overflow: 'hidden' }}>
                <div style={{ width: Math.min(p, 100) + '%', height: '100%', background: tone }}/>
              </div>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)', minWidth: 72, textAlign: 'right' }}>
                {fmt(c.spent)}
              </div>
            </div>
          );
        })}
      </div>
    </ToolFrame>
  );
}

// ─── display_spending_pie_chart ────────────────────────────────────
function ToolCardPie({ title, totalSpent, currency = 'USD', categories = [] }) {
  const total = categories.reduce((s, c) => s + c.amount, 0);
  const colors = ['#055a60', '#eb5c37', '#7b76b6', '#3ab795', '#e8a838', '#5facbd'];
  const fmt = n => new Intl.NumberFormat('en-US', { style: 'currency', currency, maximumFractionDigits: 0 }).format(n);

  // Build donut with conic-gradient
  let acc = 0;
  const stops = categories.map((c, i) => {
    const start = (acc / total) * 100;
    acc += c.amount;
    const end = (acc / total) * 100;
    return `${colors[i % colors.length]} ${start}% ${end}%`;
  }).join(', ');

  return (
    <ToolFrame toolName="display_spending_pie_chart">
      {title && <div style={{ fontSize: 12, color: 'var(--text-primary)', fontWeight: 600, marginBottom: 10 }}>{title}</div>}
      <div style={{ display: 'flex', gap: 16, alignItems: 'center' }}>
        <div style={{
          width: 96, height: 96, flex: 'none', borderRadius: 999, position: 'relative',
          background: `conic-gradient(${stops})`,
        }}>
          <div style={{
            position: 'absolute', inset: 20, background: 'var(--surface)', borderRadius: 999,
            display: 'flex', alignItems: 'center', justifyContent: 'center', flexDirection: 'column', textAlign: 'center',
          }}>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>
              {fmt(totalSpent)}
            </div>
            <div style={{ fontSize: 9, color: 'var(--text-tertiary)', letterSpacing: '0.06em' }}>TOTAL</div>
          </div>
        </div>
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 5 }}>
          {categories.map((c, i) => (
            <div key={i} style={{ display: 'grid', gridTemplateColumns: 'auto 1fr auto', gap: 8, alignItems: 'center' }}>
              <span style={{ width: 8, height: 8, borderRadius: 2, background: colors[i % colors.length] }}/>
              <span style={{ fontSize: 11.5, color: 'var(--text-primary)' }}>{c.name}</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)' }}>
                {c.percentage != null ? c.percentage.toFixed(0) + '%' : fmt(c.amount)}
              </span>
            </div>
          ))}
        </div>
      </div>
    </ToolFrame>
  );
}

// ─── display_autopilot_proposal ────────────────────────────────────
function ToolCardAutopilot({ agent, action, description, details = [], severity = 'medium', onApprove, onReject }) {
  const severityColor = { low: 'var(--success)', medium: 'var(--brand-secondary)', high: 'var(--danger)' }[severity];
  return (
    <div style={{
      background: 'var(--surface)',
      border: '1px solid var(--border-light)',
      borderLeft: '3px solid ' + severityColor,
      borderRadius: 10, padding: 14, display: 'flex', flexDirection: 'column', gap: 10,
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <div style={{
          width: 22, height: 22, borderRadius: 6,
          background: 'var(--brand-primary-10)', color: 'var(--brand-primary)',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center', flex: 'none',
        }}>
          <Icon name="sparkles" size={11}/>
        </div>
        <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-primary)' }}>{agent}</div>
        <div style={{ flex: 1 }}/>
        <span style={{
          fontFamily: 'var(--font-mono)', fontSize: 10, fontWeight: 600,
          padding: '1px 7px', borderRadius: 999, letterSpacing: '0.05em',
          background: severityColor + '22', color: severityColor,
        }}>{severity.toUpperCase()}</span>
      </div>
      <div>
        <div style={{ fontSize: 13.5, fontWeight: 600, color: 'var(--text-primary)', lineHeight: 1.4 }}>{action}</div>
        <div style={{ fontSize: 12, color: 'var(--text-secondary)', lineHeight: 1.5, marginTop: 4 }}>{description}</div>
      </div>
      {details.length > 0 && (
        <div style={{
          background: 'var(--surface-inset)', borderRadius: 6,
          padding: '8px 10px', display: 'flex', flexDirection: 'column', gap: 4,
        }}>
          {details.map((d, i) => (
            <div key={i} style={{ display: 'flex', justifyContent: 'space-between', gap: 12, fontSize: 11.5 }}>
              <span style={{ color: 'var(--text-tertiary)' }}>{d.label}</span>
              <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-primary)', fontWeight: 500 }}>{d.value}</span>
            </div>
          ))}
        </div>
      )}
      <div style={{ display: 'flex', gap: 6 }}>
        <button className="btn btn-secondary btn-sm" onClick={onApprove}>Apply</button>
        <button className="btn btn-outline btn-sm">Review</button>
        <button className="btn btn-ghost btn-sm" onClick={onReject}>Dismiss</button>
      </div>
    </div>
  );
}

// ─── confirmAction ─────────────────────────────────────────────────
function ToolCardConfirm({ action, description, severity = 'medium', onApprove, onReject, resolved }) {
  const sevColor = { low: 'var(--success)', medium: 'var(--warning)', high: 'var(--danger)' }[severity];
  return (
    <ToolFrame toolName="confirmAction" status={resolved ? 'completed' : 'awaiting-approval'}>
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 10 }}>
        <div style={{
          width: 28, height: 28, borderRadius: 8,
          background: sevColor + '22', color: sevColor,
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center', flex: 'none',
        }}>
          <Icon name="warn" size={14}/>
        </div>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{action}</div>
          <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 3, lineHeight: 1.5 }}>{description}</div>
        </div>
        <span style={{
          fontFamily: 'var(--font-mono)', fontSize: 10, fontWeight: 600,
          padding: '2px 7px', borderRadius: 4, letterSpacing: '0.05em',
          background: sevColor + '22', color: sevColor,
        }}>{severity.toUpperCase()}</span>
      </div>
      {!resolved && (
        <div style={{ display: 'flex', gap: 6, marginTop: 12, paddingTop: 10, borderTop: '1px solid var(--border-light)' }}>
          <button className="btn btn-primary btn-sm" onClick={onApprove}><Icon name="check" size={11}/> Approve</button>
          <button className="btn btn-outline btn-sm" onClick={onReject}><Icon name="close" size={11}/> Reject</button>
        </div>
      )}
      {resolved === 'approved' && (
        <div style={{ marginTop: 10, paddingTop: 10, borderTop: '1px solid var(--border-light)', fontSize: 11, color: 'var(--success)', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Icon name="check" size={12}/> Approved · executing…
        </div>
      )}
    </ToolFrame>
  );
}

// ─── display_option_selector ───────────────────────────────────────
function ToolCardOptions({ question, options = [], multiSelect = false, selected = [], onSelect }) {
  const [sel, setSel] = React.useState(new Set(selected));
  const toggle = label => {
    const next = new Set(sel);
    if (multiSelect) { next.has(label) ? next.delete(label) : next.add(label); }
    else { next.clear(); next.add(label); }
    setSel(next);
  };
  const confirm = () => onSelect && onSelect(Array.from(sel));

  return (
    <ToolFrame toolName="display_option_selector" status={sel.size ? 'completed' : 'awaiting-selection'}>
      <div style={{ fontSize: 13, color: 'var(--text-primary)', fontWeight: 500, marginBottom: 10 }}>{question}</div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        {options.map((o, i) => {
          const isSel = sel.has(o.label);
          return (
            <button key={i} onClick={() => toggle(o.label)}
              className="btn btn-outline"
              style={{
                justifyContent: 'flex-start', textAlign: 'left',
                padding: '10px 12px', height: 'auto', borderRadius: 8,
                background: isSel ? 'var(--brand-primary-10)' : 'var(--surface)',
                borderColor: isSel ? 'var(--brand-primary)' : 'var(--border-light)',
                display: 'flex', alignItems: 'flex-start', gap: 10, width: '100%',
              }}>
              <span style={{
                width: multiSelect ? 14 : 14, height: 14, flex: 'none', marginTop: 2,
                borderRadius: multiSelect ? 3 : 999,
                border: `1.5px solid ${isSel ? 'var(--brand-primary)' : 'var(--border-medium)'}`,
                background: isSel ? 'var(--brand-primary)' : 'transparent',
                display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
              }}>
                {isSel && <Icon name="check" size={10} color="#fff"/>}
              </span>
              <div style={{ flex: 1 }}>
                <div style={{ fontSize: 12.5, color: 'var(--text-primary)', fontWeight: 500 }}>{o.label}</div>
                {o.description && <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 2, lineHeight: 1.4 }}>{o.description}</div>}
              </div>
            </button>
          );
        })}
      </div>
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 10 }}>
        <button className="btn btn-primary btn-sm" disabled={!sel.size} onClick={confirm}>
          Confirm{multiSelect && sel.size ? ` (${sel.size})` : ''}
        </button>
      </div>
    </ToolFrame>
  );
}

Object.assign(window, {
  ToolFrame, ToolCallBadge,
  ToolCardFx, ToolCardBudget, ToolCardPie, ToolCardAutopilot,
  ToolCardConfirm, ToolCardOptions,
});
