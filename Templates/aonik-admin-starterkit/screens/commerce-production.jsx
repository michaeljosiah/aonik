/* API mapping: Spec 058 §8.8 — Production (landed Spec 056)
   ScreenCommerceProductionOrders
     List / filter runs      GET  /commerce/admin/production-orders?status=              (056)
     Create                  POST /commerce/admin/production-orders                      (056)
     Create from sheet       POST /commerce/admin/production-orders/from-sheet           (056 — §8.7 hand-off; provenance line where fromSheet)
     Release                 POST /commerce/admin/production-orders/{id}/release         (056 — all-or-nothing stock draw from the frozen snapshot)
     Start                   POST /commerce/admin/production-orders/{id}/start           (056)
     Complete                POST /commerce/admin/production-orders/{id}/complete        (056 — actuals + optional finished-goods yield)
     Cancel                  POST /commerce/admin/production-orders/{id}/cancel          (056 — released runs do not restock)
   ScreenCommerceKitchenSheet
     Kitchen sheet           GET  /commerce/admin/production-orders/{id}/kitchen-sheet   (056 — quantities replay the creation-time snapshot)
*/
// Commerce · Make — Spec 058 §8.8 Production group
//   • ScreenCommerceProductionOrders — the run lifecycle on the landed vocabulary
//     (Planned | Released | InProgress | Completed | Cancelled): stock-impact
//     preview + the all-or-nothing insufficient-stock error, actuals + yield on
//     completion, no-restock cancel rule, and the frozen-snapshot proof on
//     RUN-2026-0209 (snapshot rice 0.30/portion vs live recipe 0.25).
//   • ScreenCommerceKitchenSheet — print-intent sheet for RUN-2026-0221: per-dish
//     prep quantities (perPortion × plannedPortions) + merged all-ingredient totals,
//     all from the creation-time snapshot — matches what Release consumes.
// Data: CM_PROD_ORDERS, CM_RUN_STATUS, CM_RECIPES, cmUnit from mock-data.js.
// No decorative middots — commas/em-dash only; colored dots = state.

// File-local helpers, cmRun/CmRun prefix — no global collisions.
const cmRunPortions = run => run.lines.reduce((a, l) => a + l.plannedPortions, 0);
const cmRunQty = (perPortion, portions) => Math.round(perPortion * portions * 10000) / 10000;
// Live per-portion quantity from the CURRENT recipe (051/050 data) — compared
// against the frozen snapshot to prove the 056 rule: edits never touch a run.
const cmRunLivePer = (variantSku, ingId) => {
  const rcp = (typeof CM_RECIPES !== 'undefined' ? CM_RECIPES : (window.CM_RECIPES || [])).find(x => x.variantSku === variantSku);
  if (!rcp) return null;
  const c = rcp.components.find(x => x.ing === ingId);
  return c ? c.qty / rcp.yield : null;
};

function CmRunEmoji({ e, size = 26 }) {
  return (
    <span style={{ width: size, height: size, borderRadius: 6, background: 'var(--surface-inset)', border: '1px solid var(--border-light)', display: 'grid', placeItems: 'center', fontSize: Math.round(size * 0.52), flex: 'none' }}>{e}</span>
  );
}

function CmRunToggle({ on, onToggle, label }) {
  return (
    <button onClick={onToggle} style={{
      display: 'inline-flex', alignItems: 'center', gap: 8, padding: '5px 11px', borderRadius: 999, cursor: 'pointer',
      fontSize: 11.5, fontWeight: on ? 600 : 500, fontFamily: 'var(--font-sans)',
      border: '1px solid ' + (on ? 'var(--brand-primary)' : 'var(--border-light)'),
      background: on ? 'var(--brand-primary-10)' : 'var(--surface)',
      color: on ? 'var(--brand-primary)' : 'var(--text-secondary)',
    }}>
      <span style={{ width: 22, height: 12, borderRadius: 999, background: on ? 'var(--brand-primary)' : 'var(--border-light)', position: 'relative', flex: 'none', transition: 'background 0.15s' }}>
        <span style={{ position: 'absolute', top: 2, left: on ? 12 : 2, width: 8, height: 8, borderRadius: 999, background: 'var(--surface)', transition: 'left 0.15s' }} />
      </span>
      {label}
    </button>
  );
}

// ═══ Production orders — lifecycle + release preview (§8.8a) ═══════════════
function ScreenCommerceProductionOrders() {
  const [status, setStatus] = React.useState('all');
  const [sel, setSel] = React.useState(null);
  const runs = CM_PROD_ORDERS;
  const shown = status === 'all' ? runs : runs.filter(r => r.status === status);
  const active = runs.filter(r => r.status === 'released' || r.status === 'inprogress').length;
  const blocked = runs.filter(r => r.releaseBlocked).length;
  const kpis = [
    { l: 'Runs', v: runs.length, s: 'all states' },
    { l: 'Active', v: active, s: 'released or in progress' },
    { l: 'Portions planned', v: runs.reduce((a, r) => a + cmRunPortions(r), 0), s: 'across all runs' },
    { l: 'Release blocked', v: blocked, s: 'insufficient stock — nothing applied', warn: true },
  ];
  const filters = [{ id: 'all', label: 'All' }].concat(Object.keys(CM_RUN_STATUS).map(k => ({ id: k, label: CM_RUN_STATUS[k].label })));
  const cols = '150px 110px 1fr 130px 120px 120px';

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <style>{`.cm-runrow:hover{background:var(--surface-inset);cursor:pointer;}`}</style>
      <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Header */}
        <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 16 }}>
          <div>
            <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Production orders</div>
            <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>Runs that turn ingredients into dishes. Release consumes ingredient stock all-or-nothing from the recipe snapshot frozen at creation; completion records actuals and can yield finished goods.</div>
          </div>
          <button className="btn btn-primary btn-sm" style={{ flex: 'none' }}><Icon name="plus" size={12} /> New production order</button>
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

        {/* Status filter — landed lifecycle only */}
        <div style={{ display: 'inline-flex', padding: 4, gap: 2, background: 'var(--surface-inset)', borderRadius: 10, alignSelf: 'flex-start' }}>
          {filters.map(s => {
            const on = status === s.id;
            return <button key={s.id} onClick={() => setStatus(s.id)} style={{ height: 30, padding: '0 14px', borderRadius: 8, cursor: 'pointer', border: 'none', fontSize: 12, fontWeight: on ? 600 : 500, background: on ? 'var(--surface)' : 'transparent', color: on ? 'var(--text-primary)' : 'var(--text-secondary)', boxShadow: on ? 'var(--shadow-sm)' : 'none' }}>{s.label}</button>;
          })}
        </div>

        {/* Runs table */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
          <div style={{ display: 'grid', gridTemplateColumns: cols, gap: 12, padding: '10px 14px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
            <div>Run</div><div>Planned for</div><div>Lines &amp; portions</div><div>Status</div><div style={{ textAlign: 'right' }}>Released</div><div style={{ textAlign: 'right' }}>Completed</div>
          </div>
          {shown.map((r, i) => (
            <div key={r.id} className="cm-runrow" onClick={() => setSel(r)} style={{ display: 'grid', gridTemplateColumns: cols, gap: 12, padding: '11px 14px', alignItems: 'center', borderBottom: i < shown.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5, opacity: r.status === 'cancelled' ? 0.65 : 1 }}>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, fontWeight: 500, color: 'var(--text-primary)' }}>{r.ref}</div>
              <div style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{r.plannedFor}</div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <span style={{ display: 'inline-flex', gap: 3 }}>{r.lines.map(l => <CmRunEmoji key={l.variantSku} e={l.emoji} size={22} />)}</span>
                <span style={{ fontSize: 11.5, color: 'var(--text-secondary)' }}>{r.lines.length} {r.lines.length === 1 ? 'line' : 'lines'}, <b style={{ color: 'var(--text-primary)', fontFamily: 'var(--font-mono)', fontWeight: 600 }}>{cmRunPortions(r)}</b> portions</span>
                {r.fromSheet && <Pill tone="tint" size="sm">From sheet</Pill>}
              </div>
              <div><Pill tone={CM_RUN_STATUS[r.status].tone} dot size="sm">{CM_RUN_STATUS[r.status].label}</Pill></div>
              <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)' }}>{r.releasedAt || '—'}</div>
              <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)' }}>{r.completedAt || '—'}</div>
            </div>
          ))}
        </div>

        <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Icon name="lock" size={12} color="var(--text-tertiary)" /> Every run consumes the recipe snapshot frozen at its creation — later recipe edits never change a run.
        </div>
      </div>
      {sel && <CmRunDrawer key={sel.id} run={sel} onClose={() => setSel(null)} />}
    </div>
  );
}

function CmRunDrawer({ run: r, onClose }) {
  const [yieldFG, setYieldFG] = React.useState(true);
  const canCancel = r.status === 'planned' || r.status === 'released' || r.status === 'inprogress';
  const meta = [
    'created ' + r.createdAt + ' by ' + r.createdBy,
    r.releasedAt && 'released ' + r.releasedAt,
    r.startedAt && 'started ' + r.startedAt,
    r.completedAt && 'completed ' + r.completedAt,
    r.cancelledAt && 'cancelled ' + r.cancelledAt,
  ].filter(Boolean).join(' — ');

  return (
    <>
      {/* scrim rgba matches every sibling drawer (commerce-inventory/orders) — tokens.css has no scrim var */}
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(20,25,30,0.28)', zIndex: 35 }} />
      <div style={{ position: 'absolute', top: 0, right: 0, bottom: 0, width: 560, background: 'var(--surface)', borderLeft: '1px solid var(--border-light)', boxShadow: 'var(--shadow-lg)', zIndex: 36, display: 'flex', flexDirection: 'column' }}>
        {/* Header */}
        <div style={{ padding: '18px 22px 16px', borderBottom: '1px solid var(--border-light)', display: 'flex', alignItems: 'flex-start', gap: 12 }}>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 16, fontWeight: 700, color: 'var(--text-primary)' }}>{r.ref}</span>
              <Pill tone={CM_RUN_STATUS[r.status].tone} dot size="sm">{CM_RUN_STATUS[r.status].label}</Pill>
            </div>
            <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', marginTop: 3 }}>planned for <b style={{ color: 'var(--text-secondary)' }}>{r.plannedFor}</b> — {meta}</div>
          </div>
          <button onClick={onClose} style={{ width: 26, height: 26, borderRadius: 6, border: '1px solid var(--border-light)', background: 'var(--surface)', cursor: 'pointer', display: 'grid', placeItems: 'center' }}><Icon name="close" size={13} color="var(--text-secondary)" /></button>
        </div>

        {/* Body */}
        <div style={{ flex: 1, overflow: 'auto', padding: 22, display: 'flex', flexDirection: 'column', gap: 16 }}>
          {r.fromSheet && (
            <div style={{ display: 'flex', alignItems: 'flex-start', gap: 7, fontSize: 11.5, color: 'var(--text-tertiary)', lineHeight: 1.5 }}>
              <span style={{ flex: 'none', marginTop: 1 }}><Icon name="clipcheck" size={13} color="var(--text-tertiary)" /></span>
              <span><b style={{ color: 'var(--text-secondary)' }}>From production sheet</b> — {r.note}</span>
            </div>
          )}
          {!r.fromSheet && r.note && r.status !== 'cancelled' && (
            <div style={{ display: 'flex', alignItems: 'flex-start', gap: 7, fontSize: 11.5, color: 'var(--text-tertiary)', lineHeight: 1.5 }}>
              <span style={{ flex: 'none', marginTop: 1 }}><Icon name="alertc" size={13} color="var(--text-tertiary)" /></span>
              <span>{r.note}</span>
            </div>
          )}

          {/* Lines — planned vs produced + frozen snapshot */}
          <div>
            <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 8 }}>Lines</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              {r.lines.map(l => (
                <div key={l.variantSku} style={{ border: '1px solid var(--border-light)', borderRadius: 10, padding: '11px 13px' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                    <CmRunEmoji e={l.emoji} size={30} />
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{l.name}</div>
                      <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{l.variantSku}</div>
                    </div>
                    <div style={{ textAlign: 'right' }}>
                      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12.5, color: 'var(--text-secondary)' }}>{l.plannedPortions} planned</div>
                      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12.5, fontWeight: 700, color: l.producedPortions != null ? 'var(--text-primary)' : 'var(--text-tertiary)' }}>
                        {l.producedPortions != null ? l.producedPortions + ' produced' : '— produced'}
                      </div>
                    </div>
                  </div>
                  <div style={{ marginTop: 9, paddingTop: 8, borderTop: '1px dashed var(--border-light)' }}>
                    <div style={{ fontSize: 9.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 4 }}>Snapshot per portion — frozen at creation</div>
                    {l.snapshot.map(sn => {
                      const live = cmRunLivePer(l.variantSku, sn.ing);
                      const differs = live != null && Math.abs(live - sn.perPortion) > 1e-9;
                      return (
                        <div key={sn.ing} style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 11.5, padding: '3px 0' }}>
                          <span style={{ color: 'var(--text-secondary)' }}>{sn.name}</span>
                          <span style={{ flex: 1, borderBottom: '1px dashed var(--border-light)', minWidth: 20 }} />
                          <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }}>{cmUnit(sn.perPortion, sn.unit)}<span style={{ color: 'var(--text-tertiary)' }}> /portion</span></span>
                          {differs && <Pill tone="warning" size="sm">live {cmUnit(live, sn.unit)}</Pill>}
                        </div>
                      );
                    })}
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 7, fontSize: 11.5, color: 'var(--text-tertiary)' }}>
            <Icon name="lock" size={12} color="var(--text-tertiary)" /> Recipe snapshot frozen at creation — recipe edits after creation don't change this run.
          </div>

          {r.snapshotNote && (
            <div style={{ display: 'flex', gap: 9, padding: '11px 13px', borderRadius: 10, background: 'var(--warning-light)', borderLeft: '3px solid var(--warning)' }}>
              <span style={{ flex: 'none', marginTop: 1 }}><Icon name="lock" size={14} color="var(--warning)" /></span>
              <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>
                <b style={{ color: 'var(--text-primary)' }}>Frozen-snapshot proof.</b> {r.snapshotNote}
              </div>
            </div>
          )}

          {r.yieldedFinishedGoods && (
            <div style={{ display: 'flex', gap: 9, padding: '11px 13px', borderRadius: 10, background: 'var(--success-light)', borderLeft: '3px solid var(--success)' }}>
              <span style={{ flex: 'none', marginTop: 1 }}><Icon name="package" size={14} color="var(--success)" /></span>
              <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>
                <b style={{ color: 'var(--text-primary)' }}>Yielded finished goods.</b> {r.yieldNote} {r.yielded.map(y => `${y.qty} × ${y.name}`).join(', ')}.
              </div>
            </div>
          )}

          {r.status === 'cancelled' && (
            <div style={{ display: 'flex', gap: 9, padding: '11px 13px', borderRadius: 10, background: 'var(--danger-light)', borderLeft: '3px solid var(--danger)' }}>
              <span style={{ flex: 'none', marginTop: 1 }}><Icon name="ban" size={14} color="var(--danger)" /></span>
              <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>
                <b style={{ color: 'var(--danger)' }}>Cancelled by {r.cancelledBy}.</b> {r.cancelReason} {r.note}
              </div>
            </div>
          )}

          {/* Planned: stock-impact preview + all-or-nothing block (056) */}
          {r.status === 'planned' && r.releasePreview && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              <div style={{ border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
                <div style={{ padding: '10px 13px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)' }}>
                  <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-primary)' }}>Stock-impact preview — what Release draws down</div>
                  <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 1 }}>all-or-nothing: either every line applies, or nothing does</div>
                </div>
                {r.releasePreview.map((p, i) => (
                  <div key={p.ing} style={{ display: 'grid', gridTemplateColumns: '1fr 96px 96px 36px', gap: 10, padding: '8px 13px', alignItems: 'center', borderBottom: i < r.releasePreview.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <CmRunEmoji e={p.emoji} size={22} />
                      <span style={{ color: 'var(--text-primary)' }}>{p.name}</span>
                    </div>
                    <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }}>{cmUnit(p.required, p.unit)}</div>
                    <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: p.ok ? 400 : 700, color: p.ok ? 'var(--text-secondary)' : 'var(--danger)' }}>{cmUnit(p.available, p.unit)}</div>
                    <div style={{ textAlign: 'right' }}>
                      <Icon name={p.ok ? 'check2' : 'alertc'} size={14} color={p.ok ? 'var(--success)' : 'var(--danger)'} />
                    </div>
                  </div>
                ))}
              </div>
              {r.releaseBlocked && (
                <div style={{ display: 'flex', gap: 9, padding: '11px 13px', borderRadius: 10, background: 'var(--danger-light)', borderLeft: '3px solid var(--danger)' }}>
                  <span style={{ flex: 'none', marginTop: 1 }}><Icon name="ban" size={14} color="var(--danger)" /></span>
                  <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>
                    <b style={{ color: 'var(--danger)' }}>Release blocked.</b> {r.releaseBlocked.message} Nothing was consumed.
                  </div>
                </div>
              )}
            </div>
          )}

          {/* InProgress: complete with actuals + yield toggle (056) */}
          {r.status === 'inprogress' && (
            <div style={{ border: '1px solid var(--border-light)', borderRadius: 10, padding: 14, display: 'flex', flexDirection: 'column', gap: 12 }}>
              <div>
                <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>Complete this run — record actuals</div>
                <div style={{ fontSize: 11, color: 'var(--text-tertiary)', marginTop: 1 }}>inputs default to planned portions</div>
              </div>
              {r.lines.map(l => (
                <div key={l.variantSku} style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <CmRunEmoji e={l.emoji} size={24} />
                  <div style={{ flex: 1, fontSize: 12.5, color: 'var(--text-primary)' }}>{l.name}</div>
                  <input defaultValue={l.plannedPortions} style={{ width: 76, textAlign: 'right', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '7px 10px', fontSize: 13, fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)' }} />
                  <span style={{ fontSize: 11, color: 'var(--text-tertiary)', width: 92 }}>of {l.plannedPortions} planned</span>
                </div>
              ))}
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10, flexWrap: 'wrap' }}>
                <CmRunToggle on={yieldFG} onToggle={() => setYieldFG(v => !v)} label="Yield finished goods" />
                <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>produced portions land in sellable stock, variant on-hand</span>
              </div>
            </div>
          )}
        </div>

        {/* Footer — actions by state */}
        <div style={{ flex: 'none', padding: '14px 22px', borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10 }}>
          {canCancel ? (
            <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 11, color: 'var(--text-tertiary)' }}>
              <Icon name="alertc" size={12} color="var(--text-tertiary)" /> Cancel: released runs do not restock
            </div>
          ) : <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{r.status === 'completed' ? 'Completed ' + r.completedAt : ''}</span>}
          <div style={{ display: 'flex', gap: 8 }}>
            {(r.status === 'planned' || r.status === 'completed') && <button className="btn btn-outline btn-sm"><Icon name="file" size={12} /> Kitchen sheet</button>}
            {canCancel && <button className="btn btn-ghost btn-sm" style={{ color: 'var(--danger)' }}><Icon name="ban" size={12} /> Cancel run</button>}
            {r.status === 'planned' && <button className="btn btn-primary btn-sm"><Icon name="arrowdown" size={12} /> Release</button>}
            {r.status === 'released' && <button className="btn btn-primary btn-sm"><Icon name="play" size={12} /> Start</button>}
            {r.status === 'inprogress' && <button className="btn btn-primary btn-sm"><Icon name="check" size={12} /> Complete run</button>}
            {(r.status === 'completed' || r.status === 'cancelled') && <button className="btn btn-outline btn-sm" onClick={onClose}>Close</button>}
          </div>
        </div>
      </div>
    </>
  );
}

// ═══ Kitchen sheet — print-intent, snapshot-frozen (§8.8b) ═════════════════
function ScreenCommerceKitchenSheet() {
  const run = (CM_PROD_ORDERS.find(x => x.id === 'run_0221')) || CM_PROD_ORDERS[0];
  const totalPortions = cmRunPortions(run);
  // Merged all-ingredient totals across dishes (first-seen order preserved).
  const totals = [];
  run.lines.forEach(l => l.snapshot.forEach(sn => {
    const q = cmRunQty(sn.perPortion, l.plannedPortions);
    const t = totals.find(x => x.ing === sn.ing);
    if (t) t.qty = Math.round((t.qty + q) * 10000) / 10000;
    else totals.push({ ing: sn.ing, name: sn.name, unit: sn.unit, qty: q });
  }));

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Chrome header — kept minimal; the sheet below is the artefact */}
        <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 16 }}>
          <div>
            <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Kitchen sheet</div>
            <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>Print-intent prep sheet for a single run — every quantity replays the recipe snapshot frozen at creation.</div>
          </div>
          <button className="btn btn-outline btn-sm" style={{ flex: 'none' }}><Icon name="file" size={12} /> Print</button>
        </div>

        {/* The sheet */}
        <div style={{ width: '100%', maxWidth: 800, margin: '0 auto', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, boxShadow: 'var(--shadow-md)', padding: '46px 54px', display: 'flex', flexDirection: 'column', gap: 26 }}>
          {/* Sheet header */}
          <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 20 }}>
            <div>
              <div style={{ fontSize: 10, fontWeight: 700, letterSpacing: '0.14em', textTransform: 'uppercase', color: 'var(--text-tertiary)' }}>Kitchen sheet</div>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 25, fontWeight: 700, color: 'var(--text-primary)', marginTop: 4, letterSpacing: '-0.01em' }}>{run.ref}</div>
              <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 4 }}>Planned for <b style={{ color: 'var(--text-primary)' }}>{run.plannedFor}</b> — created {run.createdAt} by {run.createdBy}</div>
            </div>
            <div style={{ textAlign: 'right', flex: 'none' }}>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 36, fontWeight: 700, color: 'var(--text-primary)', lineHeight: 1 }}>{totalPortions}</div>
              <div style={{ fontSize: 10.5, fontWeight: 600, letterSpacing: '0.08em', textTransform: 'uppercase', color: 'var(--text-tertiary)', marginTop: 4 }}>total portions</div>
            </div>
          </div>

          <div style={{ height: 2, background: 'var(--text-primary)' }} />

          {/* Per-dish prep blocks */}
          {run.lines.map(l => (
            <div key={l.variantSku}>
              <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 16, marginBottom: 8 }}>
                <div style={{ fontSize: 19, fontWeight: 700, color: 'var(--text-primary)' }}>
                  <span style={{ marginRight: 9 }}>{l.emoji}</span>{l.name}
                </div>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 15, fontWeight: 600, color: 'var(--text-primary)', flex: 'none' }}>{l.plannedPortions} portions</div>
              </div>
              {l.snapshot.map(sn => (
                <div key={sn.ing} style={{ display: 'flex', alignItems: 'baseline', gap: 12, padding: '9px 2px', borderBottom: '1px dashed var(--border-light)' }}>
                  <span style={{ fontSize: 15, color: 'var(--text-primary)' }}>{sn.name}</span>
                  <span style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>{cmUnit(sn.perPortion, sn.unit)} per portion</span>
                  <span style={{ flex: 1 }} />
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: 16, fontWeight: 700, color: 'var(--text-primary)' }}>{cmUnit(cmRunQty(sn.perPortion, l.plannedPortions), sn.unit)}</span>
                </div>
              ))}
            </div>
          ))}

          {/* Merged all-ingredient totals */}
          <div>
            <div style={{ display: 'flex', alignItems: 'baseline', gap: 10, marginBottom: 8 }}>
              <div style={{ fontSize: 13, fontWeight: 700, letterSpacing: '0.08em', textTransform: 'uppercase', color: 'var(--text-primary)' }}>All ingredients</div>
              <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>merged across dishes</div>
            </div>
            <div style={{ borderTop: '2px solid var(--text-primary)', display: 'grid', gridTemplateColumns: '1fr 1fr', columnGap: 44 }}>
              {totals.map(t => (
                <div key={t.ing} style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 12, padding: '9px 2px', borderBottom: '1px solid var(--border-light)' }}>
                  <span style={{ fontSize: 15, color: 'var(--text-primary)' }}>{t.name}</span>
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: 16, fontWeight: 700, color: 'var(--text-primary)' }}>{cmUnit(t.qty, t.unit)}</span>
                </div>
              ))}
            </div>
          </div>

          {/* Footer note — the 056 guarantee, stated on the sheet */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, paddingTop: 14, borderTop: '1px solid var(--border-light)', fontSize: 12, color: 'var(--text-secondary)' }}>
            <Icon name="lock" size={13} color="var(--text-tertiary)" />
            Quantities from the recipe snapshot frozen at creation — matches what Release consumes.
          </div>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { ScreenCommerceProductionOrders, ScreenCommerceKitchenSheet });
