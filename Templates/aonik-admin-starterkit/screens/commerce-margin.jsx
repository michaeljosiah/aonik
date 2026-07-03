/* API mapping (Spec 058 §8.9 — the landed Spec 057 endpoints this screen designs against):
     Report           GET /commerce/admin/reports/margin?fromUtc=&toUtc=&currency=
     Target margin    PUT /commerce/admin/products/{id}/target-margin   (nullable — clearing removes judgement)
*/
// Commerce · Make · Spec 058 — Margin report (§8.9)
// ScreenCommerceMargin — the profit review that closes the day: WindowPicker +
// report-currency chip, five aggregate tiles (Revenue / COGS / Gross margin /
// Margin % / the dedicated unknown-COGS tile), and the per-variant table:
// discount-allocated revenue, COGS, gross margin, margin % vs target
// (ProgressCells) with inline nullable target editing.
// Honest-null rule (#164 P1, in pixels): unknown-COGS rows render "—" and stay
// OUT of the aggregate denominator — never a fake zero-cost margin. The tile
// caption shows what a zeroed COGS would have claimed instead.
// Reuses cmMoney (commerce-catalog.jsx), cmMarginRows / CM_MARGIN_STATUS
// (mock-data.js), WindowPicker / ProgressCells / Pill / Icon (kit).
// No decorative middots; dots = state only.

function ScreenCommerceMargin() {
  const [win, setWin] = React.useState({ label: 'This week', from: '2026-06-29', to: '2026-07-06' });
  const [targets, setTargets] = React.useState({});    // product id → pct | null (cleared) — PUT target-margin theatre
  const [editing, setEditing] = React.useState(null);  // product id whose inline target editor is open
  const [draft, setDraft] = React.useState('');

  const report = cmMarginRows(win);
  const t = report.totals;

  // Re-judge rows against locally edited targets (display theatre only — the
  // margin math itself never moves; targets are judgement thresholds).
  const rows = report.rows.map(r => {
    const target = (r.product in targets) ? targets[r.product] : r.targetPct;
    const status = r.marginPct == null ? 'unknown'
      : target == null ? 'notarget'
      : r.marginPct >= target ? 'above' : 'below';
    return { ...r, target, status };
  });

  const openEditor = r => { setEditing(r.product); setDraft(r.target == null ? '' : String(r.target)); };
  const closeEditor = () => { setEditing(null); setDraft(''); };
  const saveTarget = id => { const v = parseFloat(draft); if (!isNaN(v)) setTargets(prev => ({ ...prev, [id]: v })); closeEditor(); };
  const clearTarget = id => { setTargets(prev => ({ ...prev, [id]: null })); closeEditor(); };

  const tiles = [
    { l: 'Revenue', v: cmMoney(t.revenue), s: 'discount-allocated, all rows' },
    { l: 'COGS', v: cmMoney(t.cogs), s: 'known-COGS rows only' },
    { l: 'Gross margin', v: cmMoney(t.grossMargin), s: 'revenue less COGS, known rows' },
    { l: 'Margin %', v: t.marginPct.toFixed(1) + '%', s: 'over known-COGS rows only' },
    { l: 'Unknown-COGS revenue', v: cmMoney(t.unknownCogsRevenue), s: 'surfaced, excluded from the margin — zeroing it would fake ' + t.zeroedCounterfactualPct.toFixed(1) + '%', info: true },
  ];

  const cols = '1fr 64px 118px 118px 118px 216px 118px';
  const iconBtn = { width: 18, height: 18, borderRadius: 5, border: '1px solid var(--border-light)', background: 'var(--surface)', cursor: 'pointer', display: 'grid', placeItems: 'center', padding: 0, flex: 'none' };

  return (
    <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 16 }}>
        <div>
          <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Margin report</div>
          <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>The profit review that closes the day — discount-allocated revenue against recipe standard costs, judged per product against target margins. Unknown COGS is surfaced and excluded, never zeroed.</div>
        </div>
        <button className="btn btn-sm"><Icon name="download" size={12} /> Export</button>
      </div>

      {/* Window + report currency */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 16, flexWrap: 'wrap' }}>
        <WindowPicker from={win.from} to={win.to} onChange={setWin} />
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, border: '1px solid var(--border-light)', background: 'var(--surface)', borderRadius: 999, padding: '4px 12px', fontFamily: 'var(--font-mono)', fontSize: 11.5, fontWeight: 600, color: 'var(--text-primary)' }}>
            <Icon name="wallet" size={12} color="var(--text-tertiary)" /> {report.currency}
          </span>
          <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>report currency</span>
        </div>
      </div>

      {/* Aggregate tiles — the unknown-COGS tile is its own, info-toned */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 12 }}>
        {tiles.map(k => (
          <div key={k.l} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px', boxShadow: k.info ? 'inset 3px 0 0 var(--info)' : 'none' }}>
            <div style={{ fontSize: 11, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>{k.l}</div>
            <div style={{ fontSize: 22, fontWeight: 700, color: k.info ? 'var(--info)' : 'var(--text-primary)', marginTop: 4, fontFamily: 'var(--font-mono)' }}>{k.v}</div>
            <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{k.s}</div>
          </div>
        ))}
      </div>

      {/* Per-variant margin table */}
      <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
        <div style={{ display: 'grid', gridTemplateColumns: cols, gap: 12, padding: '10px 14px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
          <div>Variant</div>
          <div style={{ textAlign: 'right' }}>Qty sold</div>
          <div style={{ textAlign: 'right' }}>Revenue<div style={{ fontSize: 9, fontWeight: 500, textTransform: 'none', letterSpacing: 0 }}>discount-allocated</div></div>
          <div style={{ textAlign: 'right' }}>COGS</div>
          <div style={{ textAlign: 'right' }}>Gross margin</div>
          <div>Margin % vs target</div>
          <div>Status</div>
        </div>
        {rows.map((r, i) => (
          <div key={r.variantSku} style={{ display: 'grid', gridTemplateColumns: cols, gap: 12, padding: '11px 14px', alignItems: 'center', borderBottom: i < rows.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5, background: r.status === 'below' ? 'var(--danger-light)' : 'transparent', boxShadow: r.status === 'below' ? 'inset 3px 0 0 var(--danger)' : 'none' }}>
            {/* Variant */}
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, minWidth: 0 }}>
              <span style={{ width: 26, height: 26, borderRadius: 6, background: 'var(--surface-inset)', display: 'grid', placeItems: 'center', fontSize: 13, flex: 'none' }}>{r.emoji}</span>
              <div style={{ minWidth: 0 }}>
                <div style={{ color: 'var(--text-primary)', fontWeight: 500, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{r.name}</div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 1 }}>
                  <span style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{r.variantSku}</span>
                  {r.bundleExpanded && (
                    <span title="Includes units sold inside build-your-own boxes, split at standalone prices">
                      <Pill tone="tint" size="sm">bundle-expanded</Pill>
                    </span>
                  )}
                </div>
              </div>
            </div>
            {/* Qty sold */}
            <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }}>{r.qty}</div>
            {/* Revenue */}
            <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)' }}>{cmMoney(r.revenue)}</div>
            {/* COGS — "—" when unknown, never zero */}
            <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)' }}>
              <div style={{ fontWeight: r.cogs == null ? 400 : 600, color: r.cogs == null ? 'var(--text-tertiary)' : 'var(--text-primary)' }}>{cmMoney(r.cogs)}</div>
              {r.cogsPerUnit != null && <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>{cmMoney(r.cogsPerUnit)}/unit</div>}
            </div>
            {/* Gross margin */}
            <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: r.grossMargin == null ? 400 : 600, color: r.grossMargin == null ? 'var(--text-tertiary)' : 'var(--text-primary)' }}>{cmMoney(r.grossMargin)}</div>
            {/* Margin % vs target */}
            <div style={{ minWidth: 0 }}>
              {r.status === 'unknown' ? (
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>—</span>
                  <Pill tone="warning" dot size="sm">{r.unknownReason}</Pill>
                </div>
              ) : (
                <div>
                  {r.status === 'notarget'
                    ? <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)', fontSize: 12 }}>{r.marginPct.toFixed(1)}%</span>
                    : <ProgressCells value={r.marginPct} max={100} tone={r.status === 'above' ? 'success' : 'danger'} caption={r.marginPct.toFixed(1) + '%'} />}
                  {editing === r.product ? (
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 5, flexWrap: 'wrap' }}>
                      <input autoFocus value={draft} onChange={e => setDraft(e.target.value)} placeholder="—" style={{ width: 48, background: 'var(--surface)', border: '1px solid var(--brand-primary)', borderRadius: 6, padding: '2px 7px', fontSize: 11.5, fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)' }} />
                      <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>%</span>
                      <button onClick={() => saveTarget(r.product)} title="Save target" style={iconBtn}><Icon name="check" size={11} color="var(--brand-primary)" /></button>
                      <button onClick={closeEditor} title="Cancel" style={iconBtn}><Icon name="close" size={10} color="var(--text-tertiary)" /></button>
                      <button onClick={() => clearTarget(r.product)} style={{ border: 'none', background: 'transparent', cursor: 'pointer', padding: 0, fontSize: 10.5, color: 'var(--text-tertiary)', textDecoration: 'underline', fontFamily: 'var(--font-sans)' }}>clear target</button>
                    </div>
                  ) : (
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 5 }}>
                      <span style={{ fontSize: 10.5, color: r.target == null ? 'var(--text-tertiary)' : 'var(--text-secondary)' }}>
                        {r.target == null ? 'no target — cannot judge' : 'target ' + r.target + '%'}
                      </span>
                      <button onClick={() => openEditor(r)} title="Edit target margin" style={iconBtn}><Icon name="edit" size={10} color="var(--text-tertiary)" /></button>
                    </div>
                  )}
                </div>
              )}
            </div>
            {/* Status */}
            <div><Pill tone={CM_MARGIN_STATUS[r.status].tone} dot size="sm">{CM_MARGIN_STATUS[r.status].label}</Pill></div>
          </div>
        ))}
      </div>

      {/* Honest-aggregate footnote */}
      <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', display: 'flex', alignItems: 'center', gap: 6 }}>
        <Icon name="alertc" size={12} color="var(--info)" />
        Unknown-COGS rows keep a null margin and stay out of the aggregate denominator — the honest {t.marginPct.toFixed(1)}% here vs the {t.zeroedCounterfactualPct.toFixed(1)}% a zeroed COGS would fake. Targets are per product and nullable; clearing one shows "no target — cannot judge".
      </div>
    </div>
  );
}

Object.assign(window, { ScreenCommerceMargin });
