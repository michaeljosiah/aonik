/* API mapping: Spec 058 §8.7 — Planning (landed Spec 055, plus hand-off CTAs)
   ScreenCommerceProductionSheet
     Production sheet (windowed demand)    GET  /commerce/admin/planning/production-sheet?fromUtc=&toUtc=    (055)
     Create production order from sheet    POST /commerce/admin/production-orders/from-sheet                 (056 — §8.8 hand-off; no-recipe variants skipped and surfaced)
   ScreenCommercePrepList
     Prep list (netted vs raw)             GET  /commerce/admin/planning/prep-list?fromUtc=&toUtc=&net=true  (055 — net=false returns raw requirements)
     Order shortfalls                      POST /commerce/admin/purchase-orders/from-shortfall               (053 — §8.5 hand-off; pack-rounded, minimum one pack)
*/
// Commerce · Make — Spec 058 §8.7 Planning group
//   • ScreenCommerceProductionSheet — the aggregated production sheet: windowed
//     demand per variant (paid-or-committed orders only, Draft checkouts excluded),
//     bundle demand exploded to component variants, no-recipe variants surfaced.
//   • ScreenCommercePrepList — ingredient requirements exploded from recipes and
//     netted against Available (OnHand − Reserved): the pinned 10/8/5 ⇒ 3 rice row,
//     pack-rounded suggestions, the GBP-guard honey row, no-recipe diagnostics.
// Data: CM_PROD_SHEET, cmPrepRows(), CM_INGREDIENTS, cmUnit from mock-data.js;
// cmMoney from commerce-catalog.jsx; WindowPicker from kit/components.jsx.
// No decorative middots — commas/em-dash only; colored dots = state.

// Default window = the 'This week' preset (half-open [2026-06-29, 2026-07-06) UTC,
// matching CM_PROD_SHEET.window). File-local, CM_PLN_ prefix — no global collisions.
const CM_PLN_WEEK = { from: '2026-06-29', to: '2026-07-06', label: 'This week' };

function CmPlnEmoji({ e, size = 26 }) {
  return (
    <span style={{ width: size, height: size, borderRadius: 6, background: 'var(--surface-inset)', border: '1px solid var(--border-light)', display: 'grid', placeItems: 'center', fontSize: Math.round(size * 0.52), flex: 'none' }}>{e}</span>
  );
}

function CmPlnToggle({ on, onToggle, label }) {
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

// ═══ Production sheet — windowed demand (§8.7a) ═══════════════════════════
function ScreenCommerceProductionSheet() {
  const [win, setWin] = React.useState(CM_PLN_WEEK);
  const sheet = CM_PROD_SHEET;
  const noRecipe = sheet.rows.filter(r => !r.hasRecipe);
  const bundleExp = sheet.rows.filter(r => r.bundleExpanded);
  const kpis = [
    { l: 'Orders counted', v: sheet.orders, s: 'paid-or-committed in window' },
    { l: 'Portions total', v: sheet.portions, s: 'across ' + sheet.rows.length + ' variants' },
    { l: 'Variants demanded', v: sheet.rows.length, s: bundleExp.length + ' via bundle expansion' },
    { l: 'Without recipe', v: noRecipe.length, s: 'skipped from runs — surfaced', warn: true },
  ];
  const cols = '1.7fr 140px 140px 160px 120px';

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Header */}
        <div>
          <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Production sheet</div>
          <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>What must be made for the window — demand aggregated across orders, with build-your-own-box lines exploded to their component variants.</div>
        </div>

        {/* Window + demand rule */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <WindowPicker from={win.from} to={win.to} onChange={setWin} />
          <div style={{ display: 'flex', alignItems: 'center', gap: 7, fontSize: 12, color: 'var(--text-secondary)' }}>
            <Icon name="filter" size={13} color="var(--brand-primary)" />
            <span><b style={{ color: 'var(--text-primary)' }}>Demand rule:</b> {sheet.demandRule} Orders are counted in the half-open window {`[${win.from}, ${win.to})`} — the end date belongs to the next window.</span>
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

        {/* Demand table */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
          <div style={{ display: 'grid', gridTemplateColumns: cols, gap: 12, padding: '10px 14px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
            <div>Variant</div><div style={{ textAlign: 'right' }}>Portions demanded</div><div style={{ textAlign: 'right' }}>Orders contributing</div><div>Source</div><div>Recipe</div>
          </div>
          {sheet.rows.map(r => (
            <div key={r.variantSku} style={{ display: 'grid', gridTemplateColumns: cols, gap: 12, padding: '10px 14px', alignItems: 'center', borderBottom: '1px solid var(--border-light)', fontSize: 12.5 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <CmPlnEmoji e={r.emoji} />
                <div>
                  <div style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{r.name}</div>
                  <div style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{r.variantSku}</div>
                </div>
              </div>
              <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)' }}>{r.portions}</div>
              <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)' }}>{r.orders}</div>
              <div>{r.bundleExpanded
                ? <Pill tone="tint" size="sm">Bundle-expanded</Pill>
                : <span style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>direct</span>}</div>
              <div>{r.hasRecipe
                ? <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5, fontSize: 11.5, color: 'var(--success)' }}><Icon name="check2" size={13} color="var(--success)" /> yes</span>
                : <Pill tone="danger" size="sm">No recipe</Pill>}</div>
            </div>
          ))}
          <div style={{ display: 'grid', gridTemplateColumns: cols, gap: 12, padding: '10px 14px', alignItems: 'center', background: 'var(--surface-inset)', fontSize: 12 }}>
            <div style={{ fontWeight: 600, color: 'var(--text-secondary)' }}>Window total</div>
            <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 700, color: 'var(--text-primary)' }}>{sheet.portions}</div>
            <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)' }}>{sheet.orders} distinct</div>
            <div style={{ gridColumn: 'span 2', fontSize: 10.5, color: 'var(--text-tertiary)' }}>an order can contribute to several variants</div>
          </div>
        </div>

        {/* Create-run hand-off (§8.8) */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 16, padding: '13px 16px', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10 }}>
          <div style={{ display: 'flex', gap: 9, alignItems: 'flex-start', fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>
            <span style={{ flex: 'none', marginTop: 1 }}><Icon name="alertc" size={14} color="var(--warning)" /></span>
            <span>No-recipe variants are skipped and surfaced: <b style={{ color: 'var(--text-primary)' }}>{noRecipe.map(r => r.name).join(', ')}</b> — they stay on this sheet but the created run will not include them.</span>
          </div>
          <button className="btn btn-primary btn-sm" style={{ flex: 'none' }}><Icon name="flame" size={12} /> Create production order from this sheet</button>
        </div>
      </div>
    </div>
  );
}

// ═══ Prep list — required vs available vs shortfall (§8.7b) ═══════════════
function ScreenCommercePrepList() {
  const [win, setWin] = React.useState(CM_PLN_WEEK);
  const [net, setNet] = React.useState(true);
  const rows = cmPrepRows(win);
  const shortRows = rows.filter(r => r.shortfall > 0);
  const estSum = shortRows.reduce((a, r) => a + (r.suggested ? r.suggested.est : 0), 0);
  const noRecipe = CM_PROD_SHEET.rows.filter(r => !r.hasRecipe);
  const kpis = [
    { l: 'Ingredients required', v: rows.length, s: 'exploded from recipes' },
    { l: 'With shortfall', v: net ? shortRows.length : '—', s: net ? 'after netting vs available' : 'netting off', warn: true },
    { l: 'Suggested order cost', v: net ? cmMoney(estSum) : '—', s: net ? 'pack-rounded, minimum one pack' : 'netting off' },
    { l: 'Excluded variants', v: noRecipe.length, s: 'no recipe — see diagnostics', warn: true },
  ];
  const cols = net ? '1.4fr 110px 170px 110px 1.3fr 1.3fr' : '1.6fr 140px 1.6fr';

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Header */}
        <div>
          <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Prep list</div>
          <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>Ingredient requirements for the window's demand, exploded from recipes and netted against available stock — on hand − reserved.</div>
        </div>

        {/* Window + net toggle */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
          <WindowPicker from={win.from} to={win.to} onChange={setWin} />
          <CmPlnToggle on={net} onToggle={() => setNet(v => !v)} label="Net against stock" />
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

        {!net && (
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '11px 14px', borderRadius: 10, background: 'var(--surface-inset)', border: '1px solid var(--border-light)', fontSize: 12, color: 'var(--text-secondary)' }}>
            <Icon name="alertc" size={14} color="var(--brand-primary)" />
            <span><b style={{ color: 'var(--text-primary)' }}>Raw requirements</b> — netting is off: available, shortfall and order suggestions are hidden. Quantities are the recipe explosion alone.</span>
          </div>
        )}

        {/* Prep table */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
          <div style={{ display: 'grid', gridTemplateColumns: cols, gap: 12, padding: '10px 14px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
            <div>Ingredient</div>
            <div style={{ textAlign: 'right' }}>Required</div>
            {net && <div style={{ textAlign: 'right' }}>Available</div>}
            {net && <div style={{ textAlign: 'right' }}>Shortfall</div>}
            {net && <div>Suggested order</div>}
            <div>Cheapest supplier</div>
          </div>
          {rows.map((r, i) => {
            const ing = (CM_INGREDIENTS.find(x => x.id === r.ing) || {});
            const belowReorder = ing.reorderPoint != null && r.available < ing.reorderPoint;
            return (
              <div key={r.ing} style={{ display: 'grid', gridTemplateColumns: cols, gap: 12, padding: '10px 14px', alignItems: 'center', borderBottom: i < rows.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <CmPlnEmoji e={r.emoji} />
                  <div>
                    <div style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{r.name}</div>
                    <div style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>base unit {r.unit}</div>
                  </div>
                </div>
                <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }}>{cmUnit(r.required, r.unit)}</div>
                {net && (
                  <div style={{ textAlign: 'right' }}>
                    <div style={{ fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)' }}>{cmUnit(r.available, r.unit)}</div>
                    <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>on hand {r.onHand} − reserved {r.reserved}</div>
                    {belowReorder && r.shortfall === 0 && (
                      <div style={{ fontSize: 10, color: 'var(--warning)', fontWeight: 600 }}>below reorder point — active alert</div>
                    )}
                  </div>
                )}
                {net && (
                  <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: r.shortfall > 0 ? 700 : 400, color: r.shortfall > 0 ? 'var(--danger)' : 'var(--text-tertiary)' }}>
                    {cmUnit(r.shortfall, r.unit)}
                  </div>
                )}
                {net && (
                  <div>
                    {r.suggested ? (
                      <>
                        <div style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{r.suggested.label}</div>
                        <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>est {cmMoney(r.suggested.est, r.suggested.ccy)}</div>
                      </>
                    ) : <span style={{ color: 'var(--text-tertiary)' }}>—</span>}
                  </div>
                )}
                <div>
                  {r.cheapest ? (
                    <>
                      <div style={{ color: 'var(--text-primary)' }}>{r.cheapest.supplierName}</div>
                      <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{cmMoney(r.cheapest.unitPrice)}/{r.unit}, {r.cheapest.packLabel}</div>
                    </>
                  ) : (
                    <div>
                      <Pill tone="warning" size="sm">GBP only</Pill>
                      <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 3, lineHeight: 1.45 }}>{r.cheapestNote}</div>
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        </div>

        {/* No-recipe diagnostics (055 never-silent rule) */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: 16 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 12 }}>
            <Icon name="alertc" size={14} color="var(--warning)" />
            <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>Excluded from this prep list</span>
            <span style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>demanded this window but contributing no ingredient requirements — surfaced, never silent</span>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {noRecipe.map(v => (
              <div key={v.variantSku} style={{ display: 'flex', alignItems: 'center', gap: 10, border: '1px solid var(--border-light)', borderRadius: 10, padding: '9px 12px' }}>
                <CmPlnEmoji e={v.emoji} size={24} />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <span style={{ fontSize: 12.5, fontWeight: 500, color: 'var(--text-primary)' }}>{v.name}</span>
                  <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)', marginLeft: 8 }}>{v.variantSku}</span>
                </div>
                <span style={{ fontSize: 11.5, color: 'var(--text-secondary)', fontFamily: 'var(--font-mono)' }}>{v.portions} portions demanded</span>
                <Pill tone="danger" size="sm">No recipe</Pill>
              </div>
            ))}
          </div>
        </div>

        {/* Order-shortfalls hand-off (§8.5) */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 16, padding: '13px 16px', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10 }}>
          <div style={{ display: 'flex', gap: 9, alignItems: 'flex-start', fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>
            <span style={{ flex: 'none', marginTop: 1 }}><Icon name="clipboard" size={14} color="var(--brand-primary)" /></span>
            <span>Creates a purchase order from shortfall — {shortRows.map(r => `${r.name} ${cmUnit(r.shortfall, r.unit)} (${r.suggested ? r.suggested.label : 'no pack'})`).join(', ')}, est <b style={{ color: 'var(--text-primary)' }}>{cmMoney(estSum)}</b>, pack-rounded to the cheapest same-currency supplier.</span>
          </div>
          <button className="btn btn-primary btn-sm" style={{ flex: 'none' }}><Icon name="clipboard" size={12} /> Order shortfalls</button>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { ScreenCommerceProductionSheet, ScreenCommercePrepList });
