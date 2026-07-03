/* API mapping: Ingredients (Spec 058 §8.1 — landed 050/051/052 endpoints)
   List / create / edit / deactivate  →  GET/POST/PUT /commerce/admin/ingredients (050)
   Cost history + reprice             →  GET /commerce/admin/ingredients/{id}/cost/history · PUT /commerce/admin/ingredients/{id}/cost (051)
   Stock + reorder point              →  GET/POST /commerce/admin/ingredients/{id}/inventory · PUT /commerce/admin/ingredients/{id}/reorder-point (052)
*/

// Commerce · Make · Spec 058 — Ingredients (8.1)
// ScreenCommerceIngredients — raw-material master data: effective-dated cost
// (current window + one scheduled future window, 051), available = on hand −
// reserved with a danger tone at/below the reorder point (052), honest
// uncosted state (cost null ⇒ "—", never a fake number), and a drawer with the
// unit-lock guard, the cost-history timeline, a reprice form (incl. the
// backdate-REJECTED example — closed windows are immutable), and stock &
// reorder editing. Data: CM_INGREDIENTS; reuses cmMoney (commerce-catalog.jsx),
// cmUnit + cmIngAvail (mock-data.js). Fixed mock clock: today = 2026-07-03.

const CM_ING_TODAY = '2026-07-03';
const CM_ING_FILTERS = [
  { id: 'all', label: 'All' },
  { id: 'below', label: 'Below reorder' },
  { id: 'uncosted', label: 'Uncosted' },
  { id: 'inactive', label: 'Inactive' },
];

const cmIngD = iso => {
  if (!iso) return '';
  const p = iso.split('-');
  return (+p[2]) + ' ' + ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'][+p[1] - 1];
};
const cmIngBelow = ing => ing.active && ing.reorderPoint != null && cmIngAvail(ing) <= ing.reorderPoint;

// Avg cost movement across the last 30 days (ISO strings compare lexically):
// every non-scheduled window that OPENED inside the 30d window vs its
// predecessor. Deterministic — rice +3.7% and tomato +14.3% ⇒ avg +9.0%.
function cmIngMove30() {
  const cutoff = '2026-06-03';
  const moves = [];
  CM_INGREDIENTS.forEach(ing => {
    const h = (ing.history || []).filter(w => !w.scheduled);
    for (let i = 1; i < h.length; i++) {
      if (h[i].from >= cutoff) moves.push(((h[i].cost - h[i - 1].cost) / h[i - 1].cost) * 100);
    }
  });
  if (!moves.length) return null;
  return { avg: moves.reduce((a, b) => a + b, 0) / moves.length, n: moves.length };
}

function ScreenCommerceIngredients() {
  const [f, setF] = React.useState('all');
  const [sel, setSel] = React.useState(null);

  const rows = CM_INGREDIENTS;
  const belowN = rows.filter(cmIngBelow).length;
  const uncostedN = rows.filter(i => i.cost == null).length;
  const inactiveN = rows.filter(i => !i.active).length;
  const move = cmIngMove30();

  const counts = { all: rows.length, below: belowN, uncosted: uncostedN, inactive: inactiveN };
  const shown = rows.filter(i =>
    f === 'below' ? cmIngBelow(i)
    : f === 'uncosted' ? i.cost == null
    : f === 'inactive' ? !i.active
    : true);

  const kpis = [
    { l: 'Ingredients', v: rows.length, s: (rows.length - inactiveN) + ' active, ' + inactiveN + ' inactive' },
    { l: 'Below reorder point', v: belowN, s: 'available ≤ reorder point', warn: true },
    { l: 'Avg cost movement (30d)', v: move ? (move.avg > 0 ? '+' : '') + move.avg.toFixed(1) + '%' : '—', s: move ? 'across ' + move.n + ' repriced windows' : 'no repricing in window' },
    { l: 'Uncosted', v: uncostedN, s: 'no cost row — rollups show "—"', warn: true },
  ];

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <style>{`.cm-ingrow:hover{background:var(--surface-inset);cursor:pointer;}`}</style>
      <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Header */}
        <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 16 }}>
          <div>
            <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Ingredients</div>
            <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>Raw-material master data behind recipes, prep and costing. Costs are effective-dated — repricing opens a new window, it never rewrites history.</div>
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            <div style={{ position: 'relative' }}>
              <input placeholder="Search ingredients" style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 6, padding: '7px 10px 7px 28px', fontSize: 12.5, color: 'var(--text-primary)', width: 200, fontFamily: 'var(--font-sans)' }} />
              <span style={{ position: 'absolute', left: 9, top: 8 }}><Icon name="search" size={13} color="var(--text-tertiary)" /></span>
            </div>
            <button className="btn btn-primary btn-sm"><Icon name="plus" size={12} /> New ingredient</button>
          </div>
        </div>

        {/* KPIs */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
          {kpis.map(k => (
            <div key={k.l} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
              <div style={{ fontSize: 11, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>{k.l}</div>
              <div style={{ fontSize: 22, fontWeight: 700, color: k.warn && k.v > 0 ? 'var(--warning)' : 'var(--text-primary)', marginTop: 4, fontFamily: 'var(--font-mono)' }}>{k.v}</div>
              <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{k.s}</div>
            </div>
          ))}
        </div>

        {/* Filter */}
        <div style={{ display: 'inline-flex', padding: 4, gap: 2, background: 'var(--surface-inset)', borderRadius: 10, alignSelf: 'flex-start' }}>
          {CM_ING_FILTERS.map(t => {
            const on = f === t.id;
            return (
              <button key={t.id} onClick={() => setF(t.id)} style={{
                display: 'inline-flex', alignItems: 'center', gap: 7, height: 30, padding: '0 14px', borderRadius: 8, cursor: 'pointer', border: 'none',
                fontSize: 12, fontWeight: on ? 600 : 500, background: on ? 'var(--surface)' : 'transparent',
                color: on ? 'var(--text-primary)' : 'var(--text-secondary)', boxShadow: on ? '0 1px 3px rgba(20,25,30,0.10)' : 'none',
              }}>
                {t.label}
                {t.id !== 'all' && <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: on && counts[t.id] > 0 ? 'var(--warning)' : 'var(--text-tertiary)' }}>{counts[t.id]}</span>}
              </button>
            );
          })}
        </div>

        {/* Table */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 70px 160px 150px 110px 100px 96px', gap: 12, padding: '10px 14px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
            <div>Ingredient</div><div>Unit</div><div>Category</div><div style={{ textAlign: 'right' }}>Cost / unit</div><div style={{ textAlign: 'right' }}>Available</div><div style={{ textAlign: 'right' }}>Reorder pt</div><div style={{ textAlign: 'right' }}>Status</div>
          </div>
          {shown.map((ing, i) => {
            const avail = cmIngAvail(ing);
            const below = cmIngBelow(ing);
            return (
              <div key={ing.id} className="cm-ingrow" onClick={() => setSel(ing)} style={{ display: 'grid', gridTemplateColumns: '1fr 70px 160px 150px 110px 100px 96px', gap: 12, padding: '10px 14px', alignItems: 'center', borderBottom: i < shown.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5, opacity: ing.active ? 1 : 0.6 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <span style={{ width: 26, height: 26, borderRadius: 6, background: 'var(--surface-inset)', display: 'grid', placeItems: 'center', fontSize: 13, flex: 'none' }}>{ing.emoji}</span>
                  <div><div style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{ing.name}</div><div style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{ing.id}</div></div>
                </div>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-secondary)' }}>{ing.unit}</div>
                <div style={{ color: 'var(--text-secondary)', fontSize: 12 }}>{ing.cat}</div>
                <div style={{ textAlign: 'right' }}>
                  {ing.cost ? (
                    <>
                      <div style={{ fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)' }}>{cmMoney(ing.cost.current, ing.cost.ccy)}<span style={{ fontWeight: 400, fontSize: 10.5, color: 'var(--text-tertiary)' }}> / {ing.unit}</span></div>
                      {ing.cost.scheduled && <div style={{ fontSize: 10.5, color: 'var(--pending)', fontFamily: 'var(--font-mono)' }}>{cmMoney(ing.cost.scheduled.cost, ing.cost.ccy)} scheduled {cmIngD(ing.cost.scheduled.from)}</div>}
                    </>
                  ) : (
                    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 3 }}>
                      <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>—</span>
                      <Pill tone="warning" size="sm">Uncosted</Pill>
                    </div>
                  )}
                </div>
                <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 600, color: below ? 'var(--danger)' : 'var(--text-primary)' }}>{cmUnit(avail, ing.unit)}</div>
                <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>{ing.reorderPoint != null ? cmUnit(ing.reorderPoint, ing.unit) : '—'}</div>
                <div style={{ textAlign: 'right' }}><Pill tone={ing.active ? 'success' : 'muted'} dot size="sm">{ing.active ? 'Active' : 'Inactive'}</Pill></div>
              </div>
            );
          })}
        </div>

        <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Icon name="check2" size={12} color="var(--success)" /> Available = on hand − reserved (052 netting). A cost is the current effective-dated window's price — a scheduled window takes effect on its from-date (051), and an uncosted ingredient rolls up to "—", never a fake number.
        </div>
      </div>

      {sel && <CmIngDrawer ing={sel} onClose={() => setSel(null)} />}
    </div>
  );
}

function CmIngDrawer({ ing, onClose }) {
  const avail = cmIngAvail(ing);
  const below = cmIngBelow(ing);
  const ccy = ing.cost ? ing.cost.ccy : 'NGN';
  const hist = ing.history || [];
  const current = hist.filter(w => !w.scheduled).slice(-1)[0] || null;
  const lastClosed = hist.filter(w => !w.scheduled && w.to != null).slice(-1)[0] || null;
  const wins = hist.slice().reverse();   // newest first: scheduled, current, then closed
  const label = { fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 6 };
  const inputStyle = { width: '100%', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '8px 11px', fontSize: 13, fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' };
  return (
    <>
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(20,25,30,0.28)', zIndex: 35 }} />
      <div style={{ position: 'absolute', top: 0, right: 0, bottom: 0, width: 480, background: 'var(--surface)', borderLeft: '1px solid var(--border-light)', boxShadow: '-12px 0 32px -8px rgba(0,0,0,0.18)', zIndex: 36, display: 'flex', flexDirection: 'column' }}>
        <div style={{ padding: '18px 22px 16px', borderBottom: '1px solid var(--border-light)', display: 'flex', alignItems: 'center', gap: 12 }}>
          <span style={{ width: 40, height: 40, borderRadius: 9, background: 'var(--surface-inset)', display: 'grid', placeItems: 'center', fontSize: 19, flex: 'none' }}>{ing.emoji}</span>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ fontSize: 15, fontWeight: 700, color: 'var(--text-primary)' }}>{ing.name}</span>
              <Pill tone={ing.active ? 'success' : 'muted'} dot size="sm">{ing.active ? 'Active' : 'Inactive'}</Pill>
              {!ing.cost && <Pill tone="warning" size="sm">Uncosted</Pill>}
            </div>
            <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}><span style={{ fontFamily: 'var(--font-mono)' }}>{ing.id}</span><span style={{ marginLeft: 10 }}>{ing.cat}</span></div>
          </div>
          <button onClick={onClose} style={{ width: 26, height: 26, borderRadius: 6, border: '1px solid var(--border-light)', background: 'var(--surface)', cursor: 'pointer', display: 'grid', placeItems: 'center' }}><Icon name="close" size={13} color="var(--text-secondary)" /></button>
        </div>

        <div style={{ flex: 1, overflow: 'auto', padding: 22, display: 'flex', flexDirection: 'column', gap: 18 }}>
          {/* Identity */}
          <div>
            <div style={label}>Identity</div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
              {[['Ingredient ID', ing.id, true], ['Base unit', ing.unit, true], ['Category', ing.cat, false], ['Costing currency', ccy, true]].map(([l, v, mono]) => (
                <div key={l} style={{ background: 'var(--surface-inset)', borderRadius: 9, padding: '9px 12px' }}>
                  <div style={{ fontSize: 9.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase' }}>{l}</div>
                  <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)', marginTop: 3, fontFamily: mono ? 'var(--font-mono)' : 'var(--font-sans)' }}>{v}</div>
                </div>
              ))}
            </div>
            {ing.unitLocked && (
              <div style={{ display: 'flex', gap: 9, padding: '10px 13px', borderRadius: 10, background: 'var(--warning-light)', borderLeft: '3px solid var(--warning)', marginTop: 8 }}>
                <Icon name="lock" size={14} color="var(--warning)" style={{ flex: 'none', marginTop: 1 }} />
                <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}><b style={{ color: 'var(--text-primary)' }}>Unit locked</b> — this ingredient is referenced by recipes/costs; v1 has no unit conversion, so the base unit ({ing.unit}) is immutable (050/051 guard).</div>
              </div>
            )}
            {ing.note && (
              <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', marginTop: 8, lineHeight: 1.5 }}>{ing.note}</div>
            )}
          </div>

          {/* Stock & reorder (052) */}
          <div>
            <div style={label}>Stock &amp; reorder (052)</div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 8 }}>
              {[['On hand', cmUnit(ing.onHand, ing.unit), 'var(--text-primary)'], ['Reserved', cmUnit(ing.reserved, ing.unit), 'var(--text-secondary)'], ['Available', cmUnit(avail, ing.unit), below ? 'var(--danger)' : 'var(--success)']].map(([l, v, c]) => (
                <div key={l} style={{ background: 'var(--surface-inset)', borderRadius: 9, padding: '10px 12px' }}>
                  <div style={{ fontSize: 9.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase' }}>{l}</div>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 15, fontWeight: 700, color: c, marginTop: 3 }}>{v}</div>
                </div>
              ))}
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 8, marginTop: 8 }}>
              {[['Set on-hand', ing.onHand], ['Reorder point', ing.reorderPoint], ['Reorder qty', ing.reorderQty]].map(([l, v]) => (
                <div key={l}>
                  <div style={{ fontSize: 9.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 4 }}>{l} ({ing.unit})</div>
                  <input defaultValue={v != null ? v : ''} placeholder="—" style={inputStyle} />
                </div>
              ))}
            </div>
            <div style={{ fontSize: 11, color: 'var(--text-tertiary)', marginTop: 7, lineHeight: 1.5 }}>A low-stock alert opens when available (on hand − reserved) falls to the reorder point; the reorder qty seeds the from-shortfall purchase order. Reserved is held by production and is not editable here.</div>
          </div>

          {/* Cost history (051) */}
          <div>
            <div style={label}>Cost history (051)</div>
            {wins.length === 0 ? (
              <div style={{ display: 'flex', gap: 9, padding: '11px 13px', borderRadius: 10, background: 'var(--warning-light)', borderLeft: '3px solid var(--warning)' }}>
                <Icon name="alertc" size={14} color="var(--warning)" style={{ flex: 'none', marginTop: 1 }} />
                <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}><b style={{ color: 'var(--text-primary)' }}>No cost rows — {ing.name} is uncosted.</b> Every recipe using it rolls up to "—" (unknown, never zero) until a first cost window exists. Apply a cost below to resolve it.</div>
              </div>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                {wins.map((w, i) => {
                  const isSched = !!w.scheduled;
                  const isCur = !isSched && w === current;
                  return (
                    <div key={i} style={{
                      display: 'flex', alignItems: 'center', gap: 10, padding: '9px 12px', borderRadius: 9,
                      border: isSched ? '1px dashed var(--pending)' : isCur ? '1px solid var(--brand-primary)' : '1px solid var(--border-light)',
                      background: isSched ? 'var(--pending-light)' : isCur ? 'var(--brand-primary-10)' : 'var(--surface)',
                      opacity: isSched || isCur ? 1 : 0.65,
                    }}>
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-primary)' }}>
                          {isSched ? 'from ' + cmIngD(w.from) : '[' + cmIngD(w.from) + ' → ' + (w.to ? cmIngD(w.to) : 'open') + ')'}
                        </div>
                        <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 1 }}>{w.source}</div>
                      </div>
                      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 13, fontWeight: 700, color: 'var(--text-primary)', flex: 'none' }}>{cmMoney(w.cost, ccy)}<span style={{ fontWeight: 400, fontSize: 10, color: 'var(--text-tertiary)' }}> / {ing.unit}</span></div>
                      {isSched && <Pill tone="pending" dot size="sm">Scheduled</Pill>}
                      {isCur && <Pill tone="success" dot size="sm">Current</Pill>}
                    </div>
                  );
                })}
                <div style={{ fontSize: 11, color: 'var(--text-tertiary)', lineHeight: 1.5 }}>Windows are half-open [from → to) with exactly one open window. Closed windows never change — a reprice splits the open window at its effective date.</div>
              </div>
            )}
          </div>

          {/* Reprice (051) */}
          <div>
            <div style={label}>Reprice</div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr auto', gap: 8, alignItems: 'end' }}>
              <div>
                <div style={{ fontSize: 9.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 4 }}>New cost / {ing.unit} ({ccy})</div>
                <input defaultValue={ing.cost ? ing.cost.current : ''} placeholder="0" style={inputStyle} />
              </div>
              <div>
                <div style={{ fontSize: 9.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 4 }}>Effective from</div>
                <input defaultValue={CM_ING_TODAY} style={inputStyle} />
              </div>
              <button className="btn btn-primary btn-sm" style={{ height: 34 }}><Icon name="check" size={12} /> Apply</button>
            </div>
            <div style={{ fontSize: 11, color: 'var(--text-tertiary)', marginTop: 7, lineHeight: 1.5 }}>Today closes the current window now; a future date schedules the change and the current price stays in force until then{ing.cost && ing.cost.scheduled ? ' — like the ' + cmMoney(ing.cost.scheduled.cost, ccy) + ' window scheduled for ' + cmIngD(ing.cost.scheduled.from) + '.' : '.'}</div>
            {lastClosed && (
              <div style={{ display: 'flex', gap: 9, padding: '10px 13px', borderRadius: 10, background: 'var(--danger-light)', borderLeft: '3px solid var(--danger)', marginTop: 8 }}>
                <Icon name="ban" size={14} color="var(--danger)" style={{ flex: 'none', marginTop: 1 }} />
                <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>
                  <div style={{ fontSize: 9.5, fontWeight: 700, color: 'var(--danger)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 2 }}>Rejected example — backdated reprice</div>
                  Effective {cmIngD(lastClosed.from)} is inside an elapsed window [{cmIngD(lastClosed.from)} → {cmIngD(lastClosed.to)}) — history is immutable. Choose today or later.
                </div>
              </div>
            )}
          </div>
        </div>

        <div style={{ flex: 'none', padding: '14px 22px', borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <button className="btn btn-ghost btn-sm" style={{ color: ing.active ? 'var(--danger)' : 'var(--text-secondary)' }}><Icon name={ing.active ? 'ban' : 'refresh'} size={12} /> {ing.active ? 'Deactivate' : 'Reactivate'}</button>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-outline btn-sm" onClick={onClose}>Cancel</button>
            <button className="btn btn-primary btn-sm"><Icon name="check" size={12} /> Save changes</button>
          </div>
        </div>
      </div>
    </>
  );
}

Object.assign(window, { ScreenCommerceIngredients });
