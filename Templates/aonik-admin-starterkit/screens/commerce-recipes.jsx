/* API mapping: Recipes (Spec 058 §8.2 — landed 050/051 endpoints)
   Get / set recipe   →  GET/PUT /commerce/admin/variants/{id}/recipe (050)
   Explosion preview  →  GET /commerce/admin/variants/{id}/recipe/explosion?portions=N (050)
   Standard cost      →  GET /commerce/admin/variants/{id}/standard-cost?currency=NGN (051)
*/

// Commerce · Make · Spec 058 — Recipes (8.2)
// ScreenCommerceRecipes — the recipe-per-variant master data behind every prep
// list and kitchen sheet (Spec 050 BOM + 051 rollup): CM_RECIPES joined to
// cmAllProducts(), per-portion standard cost with an honest "—" (+ the uncosted
// ingredient named) when a component has no cost, a DIAGNOSTICS tab surfacing
// demanded variants with NO recipe (the family's never-silent rule), and a
// drawer with the component editor, validation-rejected examples, and the
// 40-portion explosion preview (jollof @ 40 ⇒ 10 kg rice, 5 kg tomato,
// 2 kg onion — the 050 worked example verbatim).
// Reuses cmMoney (commerce-catalog.jsx), cmUnit + cmAllProducts (mock-data.js).

const cmRcpD = iso => {
  if (!iso) return '';
  const p = iso.split('-');
  return (+p[2]) + ' ' + ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'][+p[1] - 1];
};
const cmRcpIng = id => CM_INGREDIENTS.find(x => x.id === id) || { id, name: id, emoji: '❓', unit: '', active: true, cost: null };
const cmRcpProd = sku => {
  const all = cmAllProducts();
  for (const p of all) {
    const v = (p.variants || []).find(x => x.sku === sku);
    if (v) return { p, v };
  }
  return { p: null, v: null };
};
const cmRcpYield = r => r.yield + ' ' + r.unit + (r.yield === 1 ? '' : 's');
// Per-portion cost of one component at the ingredient's CURRENT effective-dated
// cost: qty × cost ÷ yield — null (never zero) when the ingredient is uncosted.
const cmRcpCompCost = (c, r) => {
  const ing = cmRcpIng(c.ing);
  return ing.cost ? (ing.cost.current * c.qty) / r.yield : null;
};

function ScreenCommerceRecipes() {
  const [tab, setTab] = React.useState('recipes');
  const [sel, setSel] = React.useState(null);

  const rows = React.useMemo(() => CM_RECIPES.map(r => {
    const { p, v } = cmRcpProd(r.variantSku);
    return { ...r, color: p ? p.color : null, opt: v ? v.opt : null };
  }), []);
  const uncostedRcps = rows.filter(r => r.perPortionCost == null);
  const noRecipe = CM_PROD_SHEET.rows.filter(r => !r.hasRecipe);
  const diagCount = noRecipe.length + uncostedRcps.length;

  const kpis = [
    { l: 'Recipes', v: rows.length, s: 'one per sellable variant' },
    { l: 'Costed', v: rows.length - uncostedRcps.length, s: 'per-portion standard cost known' },
    { l: 'Incomplete rollup', v: uncostedRcps.length, s: 'a component has no cost — shows "—"', warn: true },
    { l: 'Demanded, no recipe', v: noRecipe.length, s: 'excluded from prep & costing', warn: true },
  ];

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <style>{`.cm-rcprow:hover{background:var(--surface-inset);cursor:pointer;}`}</style>
      <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Header */}
        <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 16 }}>
          <div>
            <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Recipes</div>
            <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>The bill of materials behind prep lists and kitchen sheets. A recipe draws ingredient stock when production releases; a bundle draws sellable-variant stock at checkout — they never mix.</div>
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            <div style={{ position: 'relative' }}>
              <input placeholder="Search recipes or products" style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 6, padding: '7px 10px 7px 28px', fontSize: 12.5, color: 'var(--text-primary)', width: 210, fontFamily: 'var(--font-sans)' }} />
              <span style={{ position: 'absolute', left: 9, top: 8 }}><Icon name="search" size={13} color="var(--text-tertiary)" /></span>
            </div>
            <button className="btn btn-primary btn-sm"><Icon name="plus" size={12} /> New recipe</button>
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

        {/* Tabs */}
        <div style={{ display: 'inline-flex', padding: 4, gap: 2, background: 'var(--surface-inset)', borderRadius: 10, alignSelf: 'flex-start' }}>
          {[{ id: 'recipes', label: 'Recipes', n: rows.length }, { id: 'diag', label: 'Diagnostics', n: diagCount }].map(t => {
            const on = tab === t.id;
            return (
              <button key={t.id} onClick={() => setTab(t.id)} style={{
                display: 'inline-flex', alignItems: 'center', gap: 7, height: 30, padding: '0 14px', borderRadius: 8, cursor: 'pointer', border: 'none',
                fontSize: 12, fontWeight: on ? 600 : 500, background: on ? 'var(--surface)' : 'transparent',
                color: on ? 'var(--text-primary)' : 'var(--text-secondary)', boxShadow: on ? '0 1px 3px rgba(20,25,30,0.10)' : 'none',
              }}>
                {t.label}
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: t.id === 'diag' && t.n > 0 ? 'var(--warning)' : 'var(--text-tertiary)' }}>{t.n}</span>
              </button>
            );
          })}
        </div>

        {/* Recipes table */}
        {tab === 'recipes' && (
          <>
            <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 100px 110px 170px 90px', gap: 12, padding: '10px 14px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
                <div>Product</div><div>Recipe</div><div style={{ textAlign: 'right' }}>Yield</div><div style={{ textAlign: 'right' }}>Components</div><div style={{ textAlign: 'right' }}>Cost / portion</div><div style={{ textAlign: 'right' }}>Updated</div>
              </div>
              {rows.map((r, i) => (
                <div key={r.id} className="cm-rcprow" onClick={() => setSel(r)} style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 100px 110px 170px 90px', gap: 12, padding: '10px 14px', alignItems: 'center', borderBottom: i < rows.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10, minWidth: 0 }}>
                    <span style={{ width: 26, height: 26, borderRadius: 6, background: r.color ? r.color + '22' : 'var(--surface-inset)', display: 'grid', placeItems: 'center', fontSize: 13, flex: 'none' }}>{r.emoji}</span>
                    <div style={{ minWidth: 0 }}>
                      <div style={{ color: 'var(--text-primary)', fontWeight: 500, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{r.product}</div>
                      <div style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{r.variantSku}</div>
                    </div>
                  </div>
                  <div style={{ color: 'var(--text-secondary)', fontSize: 12, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{r.name}</div>
                  <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)', fontSize: 12 }}>{cmRcpYield(r)}</div>
                  <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)' }}>{r.components.length}</div>
                  <div style={{ textAlign: 'right' }}>
                    {r.perPortionCost != null ? (
                      <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)' }}>{cmMoney(r.perPortionCost, r.ccy)}</span>
                    ) : (
                      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 3 }}>
                        <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>—</span>
                        <Pill tone="warning" size="sm">{(r.uncosted || []).map(id => cmRcpIng(id).name).join(', ')} uncosted</Pill>
                      </div>
                    )}
                  </div>
                  <div style={{ textAlign: 'right', fontSize: 11.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{cmRcpD(r.updatedAt)}</div>
                </div>
              ))}
            </div>
            <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', display: 'flex', alignItems: 'center', gap: 6 }}>
              <Icon name="check2" size={12} color="var(--success)" /> Standard cost rolls up components at the current effective-dated ingredient cost (051). An incomplete rollup renders "—", never zero — and the margin report excludes it rather than faking it.
            </div>
          </>
        )}

        {/* Diagnostics — the never-silent rule */}
        {tab === 'diag' && (
          <>
            <div style={{ display: 'flex', alignItems: 'flex-start', gap: 10, padding: '11px 14px', borderRadius: 10, background: 'var(--info-light)', borderLeft: '3px solid var(--info)' }}>
              <Icon name="alertc" size={14} color="var(--info)" style={{ flex: 'none', marginTop: 1 }} />
              <div style={{ fontSize: 12, color: 'var(--text-secondary)', lineHeight: 1.5 }}>
                <b style={{ color: 'var(--text-primary)' }}>Never silent.</b> A demanded variant without a recipe is excluded from the prep list and from costing — and surfaced here, on the production sheet (055) and in the margin report (057). Nothing is skipped quietly.
              </div>
            </div>

            <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 110px 80px 1fr 130px', gap: 12, padding: '10px 14px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
                <div>Variant</div><div style={{ textAlign: 'right' }}>Demand</div><div style={{ textAlign: 'right' }}>Orders</div><div>Diagnostic</div><div />
              </div>
              {noRecipe.map((d, i) => (
                <div key={d.variantSku} style={{ display: 'grid', gridTemplateColumns: '1fr 110px 80px 1fr 130px', gap: 12, padding: '11px 14px', alignItems: 'center', borderBottom: i < noRecipe.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                    <span style={{ width: 26, height: 26, borderRadius: 6, background: 'var(--surface-inset)', display: 'grid', placeItems: 'center', fontSize: 13, flex: 'none' }}>{d.emoji}</span>
                    <div><div style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{d.name}</div><div style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{d.variantSku}</div></div>
                  </div>
                  <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }}>{d.portions} portions</div>
                  <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)' }}>{d.orders}</div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 7, color: 'var(--danger)', fontSize: 12 }}>
                    <Icon name="warn" size={13} color="var(--danger)" style={{ flex: 'none' }} />
                    no recipe — excluded from prep &amp; costing
                  </div>
                  <div style={{ textAlign: 'right' }}><button className="btn btn-outline btn-sm"><Icon name="plus" size={11} /> Attach recipe</button></div>
                </div>
              ))}
            </div>
            <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>Demand window {CM_PROD_SHEET.window.label.toLowerCase()} [{CM_PROD_SHEET.window.from} → {CM_PROD_SHEET.window.to}) — the production sheet's paid-or-committed predicate (055); Draft checkouts are excluded.</div>

            {uncostedRcps.length > 0 && (
              <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
                <div style={{ padding: '10px 14px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>Recipe attached, rollup incomplete</div>
                {uncostedRcps.map((r, i) => (
                  <div key={r.id} style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 130px', gap: 12, padding: '11px 14px', alignItems: 'center', borderBottom: i < uncostedRcps.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                      <span style={{ width: 26, height: 26, borderRadius: 6, background: r.color ? r.color + '22' : 'var(--surface-inset)', display: 'grid', placeItems: 'center', fontSize: 13, flex: 'none' }}>{r.emoji}</span>
                      <div><div style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{r.product}</div><div style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{r.variantSku}</div></div>
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 7, color: 'var(--warning)', fontSize: 12 }}>
                      <Icon name="alertc" size={13} color="var(--warning)" style={{ flex: 'none' }} />
                      standard cost "—" — {(r.uncosted || []).map(id => cmRcpIng(id).name).join(', ')} has no cost row (051)
                    </div>
                    <div style={{ textAlign: 'right' }}><button className="btn btn-outline btn-sm">Cost ingredient</button></div>
                  </div>
                ))}
              </div>
            )}
          </>
        )}
      </div>

      {sel && <CmRcpDrawer r={sel} onClose={() => setSel(null)} />}
    </div>
  );
}

function CmRcpDrawer({ r, onClose }) {
  const [portions, setPortions] = React.useState(40);
  const uncostedNames = (r.uncosted || []).map(id => cmRcpIng(id).name).join(', ');
  const label = { fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 8 };
  const qtyInput = { width: 64, background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 7, padding: '5px 8px', fontSize: 12, fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)', textAlign: 'right' };
  return (
    <>
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(20,25,30,0.28)', zIndex: 35 }} />
      <div style={{ position: 'absolute', top: 0, right: 0, bottom: 0, width: 560, background: 'var(--surface)', borderLeft: '1px solid var(--border-light)', boxShadow: '-12px 0 32px -8px rgba(0,0,0,0.18)', zIndex: 36, display: 'flex', flexDirection: 'column' }}>
        <div style={{ padding: '18px 22px 16px', borderBottom: '1px solid var(--border-light)', display: 'flex', alignItems: 'center', gap: 12 }}>
          <span style={{ width: 40, height: 40, borderRadius: 9, background: r.color ? r.color + '22' : 'var(--surface-inset)', display: 'grid', placeItems: 'center', fontSize: 19, flex: 'none' }}>{r.emoji}</span>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ fontSize: 15, fontWeight: 700, color: 'var(--text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{r.name}</span>
              {r.perPortionCost != null
                ? <Pill tone="success" size="sm">{cmMoney(r.perPortionCost, r.ccy)} / portion</Pill>
                : <Pill tone="warning" size="sm">Uncosted — {uncostedNames}</Pill>}
            </div>
            <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>{r.product}<span style={{ marginLeft: 10, fontFamily: 'var(--font-mono)' }}>{r.variantSku}</span><span style={{ marginLeft: 10 }}>batch yields {cmRcpYield(r)}</span></div>
          </div>
          <button onClick={onClose} style={{ width: 26, height: 26, borderRadius: 6, border: '1px solid var(--border-light)', background: 'var(--surface)', cursor: 'pointer', display: 'grid', placeItems: 'center' }}><Icon name="close" size={13} color="var(--text-secondary)" /></button>
        </div>

        <div style={{ flex: 1, overflow: 'auto', padding: 22, display: 'flex', flexDirection: 'column', gap: 18 }}>
          {/* Component editor */}
          <div>
            <div style={label}>Components — per batch of {cmRcpYield(r)}</div>
            <div style={{ border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 110px 100px 100px 26px', gap: 10, padding: '8px 13px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
                <div>Ingredient</div><div style={{ textAlign: 'right' }}>Qty / batch</div><div style={{ textAlign: 'right' }}>Per portion</div><div style={{ textAlign: 'right' }}>Cost / portion</div><div />
              </div>
              {r.components.map((c, i) => {
                const ing = cmRcpIng(c.ing);
                const cc = cmRcpCompCost(c, r);
                return (
                  <div key={c.ing} style={{ display: 'grid', gridTemplateColumns: '1fr 110px 100px 100px 26px', gap: 10, padding: '9px 13px', alignItems: 'center', borderBottom: '1px solid var(--border-light)', fontSize: 12.5 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 9, minWidth: 0 }}>
                      <span style={{ width: 24, height: 24, borderRadius: 6, background: 'var(--surface-inset)', display: 'grid', placeItems: 'center', fontSize: 12, flex: 'none' }}>{ing.emoji}</span>
                      <span style={{ color: 'var(--text-primary)', fontWeight: 500, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{ing.name}</span>
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: 6 }}>
                      <input defaultValue={c.qty} style={qtyInput} />
                      <span style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)', width: 18 }}>{ing.unit}</span>
                    </div>
                    <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-secondary)' }}>{cmUnit(c.qty / r.yield, ing.unit)}</div>
                    <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 11.5, fontWeight: 600, color: cc != null ? 'var(--text-primary)' : 'var(--warning)' }}>{cc != null ? cmMoney(cc, r.ccy) : '—'}</div>
                    <button style={{ width: 22, height: 22, borderRadius: 5, border: 'none', background: 'transparent', cursor: 'pointer', display: 'grid', placeItems: 'center' }}><Icon name="trash" size={12} color="var(--text-tertiary)" /></button>
                  </div>
                );
              })}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 110px 26px', gap: 10, padding: '9px 13px', alignItems: 'center', borderBottom: '1px solid var(--border-light)', background: 'var(--surface-inset)' }}>
                <select defaultValue="" style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 7, padding: '6px 9px', fontSize: 12, color: 'var(--text-secondary)' }}>
                  <option value="" disabled>Add an ingredient…</option>
                  {CM_INGREDIENTS.filter(x => x.active).map(x => <option key={x.id}>{x.name}</option>)}
                </select>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: 6 }}>
                  <input placeholder="qty" style={qtyInput} />
                </div>
                <button style={{ width: 22, height: 22, borderRadius: 5, border: '1px solid var(--border-light)', background: 'var(--surface)', cursor: 'pointer', display: 'grid', placeItems: 'center' }}><Icon name="plus" size={12} color="var(--brand-primary)" /></button>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '10px 13px' }}>
                <span style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>Per-portion standard cost</span>
                {r.perPortionCost != null
                  ? <span style={{ fontFamily: 'var(--font-mono)', fontSize: 15, fontWeight: 700, color: 'var(--text-primary)' }}>{cmMoney(r.perPortionCost, r.ccy)}</span>
                  : <span style={{ fontSize: 11.5, color: 'var(--warning)', fontWeight: 600 }}>— unknown, not zero: {uncostedNames} is uncosted (051)</span>}
              </div>
            </div>
            <div style={{ fontSize: 11, color: 'var(--text-tertiary)', marginTop: 7, lineHeight: 1.5 }}>Quantities are per batch in each ingredient's base unit; one batch yields {cmRcpYield(r)}. Costs use the current effective-dated ingredient cost.</div>
            {r.note && (
              <div style={{ display: 'flex', gap: 9, padding: '10px 13px', borderRadius: 10, background: 'var(--brand-primary-10)', borderLeft: '3px solid var(--brand-primary)', marginTop: 8 }}>
                <Icon name="clock" size={14} color="var(--brand-primary)" style={{ flex: 'none', marginTop: 1 }} />
                <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>{r.note}</div>
              </div>
            )}
          </div>

          {/* Validation — rejected examples */}
          <div>
            <div style={label}>Validation — rejected examples</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 9, padding: '9px 12px', borderRadius: 9, background: 'var(--danger-light)', border: '1px solid var(--danger)' }}>
                <span style={{ width: 24, height: 24, borderRadius: 6, background: 'var(--surface)', display: 'grid', placeItems: 'center', fontSize: 12, flex: 'none', opacity: 0.6 }}>🛢️</span>
                <div style={{ flex: 1, fontSize: 12, color: 'var(--text-primary)', fontWeight: 500 }}>Groundnut oil<span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)', marginLeft: 8 }}>0.5 L</span></div>
                <div style={{ fontSize: 11.5, color: 'var(--danger)', display: 'flex', alignItems: 'center', gap: 5 }}><Icon name="ban" size={12} /> inactive ingredient — components must reference active ingredients</div>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 9, padding: '9px 12px', borderRadius: 9, background: 'var(--danger-light)', border: '1px solid var(--danger)' }}>
                <span style={{ width: 24, height: 24, borderRadius: 6, background: 'var(--surface)', display: 'grid', placeItems: 'center', fontSize: 12, flex: 'none' }}>🍚</span>
                <div style={{ flex: 1, fontSize: 12, color: 'var(--text-primary)', fontWeight: 500 }}>Long-grain rice<span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--danger)', marginLeft: 8 }}>0 kg</span></div>
                <div style={{ fontSize: 11.5, color: 'var(--danger)', display: 'flex', alignItems: 'center', gap: 5 }}><Icon name="ban" size={12} /> non-positive quantity — must be greater than zero</div>
              </div>
              <div style={{ fontSize: 11, color: 'var(--text-tertiary)', lineHeight: 1.5 }}>Save is blocked while any component row is invalid (050 guards).</div>
            </div>
          </div>

          {/* Explosion preview (050) */}
          <div>
            <div style={label}>Explosion preview (050)</div>
            <div style={{ border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '10px 13px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)' }}>
                <span style={{ fontSize: 12, color: 'var(--text-secondary)', fontWeight: 500 }}>Portions</span>
                <input type="number" min="0" value={portions} onChange={e => setPortions(Math.max(0, Math.floor(+e.target.value || 0)))} style={{ width: 74, background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 7, padding: '6px 9px', fontSize: 13, fontFamily: 'var(--font-mono)', fontWeight: 700, color: 'var(--text-primary)', textAlign: 'right' }} />
                <div style={{ display: 'flex', gap: 4 }}>
                  {[20, 40, 60].map(n => (
                    <button key={n} onClick={() => setPortions(n)} style={{
                      fontSize: 11, padding: '3px 9px', borderRadius: 999, cursor: 'pointer', fontFamily: 'var(--font-mono)',
                      border: '1px solid ' + (portions === n ? 'var(--brand-primary)' : 'var(--border-light)'),
                      background: portions === n ? 'var(--brand-primary-10)' : 'var(--surface)',
                      color: portions === n ? 'var(--brand-primary)' : 'var(--text-secondary)', fontWeight: portions === n ? 600 : 500,
                    }}>{n}</button>
                  ))}
                </div>
                <span style={{ marginLeft: 'auto', fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{portions} ÷ {r.yield} = {(portions / r.yield).toLocaleString('en-NG', { maximumFractionDigits: 2 })} batches</span>
              </div>
              {r.components.map(c => {
                const ing = cmRcpIng(c.ing);
                const total = (portions * c.qty) / r.yield;
                return (
                  <div key={c.ing} style={{ display: 'flex', alignItems: 'center', gap: 9, padding: '9px 13px', borderBottom: '1px solid var(--border-light)', fontSize: 12.5 }}>
                    <span style={{ width: 24, height: 24, borderRadius: 6, background: 'var(--surface-inset)', display: 'grid', placeItems: 'center', fontSize: 12, flex: 'none' }}>{ing.emoji}</span>
                    <span style={{ flex: 1, color: 'var(--text-primary)', fontWeight: 500 }}>{ing.name}</span>
                    <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{portions} × {c.qty} ÷ {r.yield}</span>
                    <span style={{ fontFamily: 'var(--font-mono)', fontSize: 13, fontWeight: 700, color: 'var(--text-primary)', minWidth: 76, textAlign: 'right' }}>{cmUnit(total, ing.unit)}</span>
                  </div>
                );
              })}
              <div style={{ padding: '9px 13px', fontSize: 11, color: 'var(--text-tertiary)', lineHeight: 1.5 }}>portions × qty ÷ yield — the same expansion the prep list (055) and the kitchen sheet's frozen snapshot (056) consume.</div>
            </div>
          </div>
        </div>

        <div style={{ flex: 'none', padding: '14px 22px', borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <button className="btn btn-ghost btn-sm" style={{ color: 'var(--danger)' }}><Icon name="trash" size={12} /> Remove recipe</button>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-outline btn-sm" onClick={onClose}>Cancel</button>
            <button className="btn btn-primary btn-sm"><Icon name="check" size={12} /> Save recipe</button>
          </div>
        </div>
      </div>
    </>
  );
}

Object.assign(window, { ScreenCommerceRecipes });
