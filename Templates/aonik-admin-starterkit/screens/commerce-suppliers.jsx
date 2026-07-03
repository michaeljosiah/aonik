/* API mapping (Spec 058 §8.4 — landed endpoints):
   UI action              Landed API
   Suppliers CRUD         GET/POST/PUT /commerce/admin/suppliers   (053)
   Catalog read / upsert  GET/PUT /commerce/admin/suppliers/{id}/catalog   (053)
*/
// Commerce · Make · Spec 058 §8.4 — Suppliers
// ScreenCommerceSuppliers — who to order the rice from, and at what pack
// economics. Party-linked suppliers attach a Supplier party role to their
// purchase orders; unlinked ones are provenance-only (053 §11, explained in
// the drawer via linkNote). Derived unit price = packPrice ÷ packSize
// (25 kg sack @ ₦28,000 ⇒ ₦1,120/kg); the cheapest-NGN pill compares only
// same-currency rows — GBP rows cannot price an NGN purchase order and are
// excluded (053 currency honesty guard, the Albion mismatch state).
// Data: CM_SUPPLIERS / CM_INGREDIENTS / cmMoney / cmUnit. Mock-only.

const cmSupIngById = id => CM_INGREDIENTS.find(i => i.id === id) || { name: id, emoji: '❔', unit: '' };

// Cheapest derived unit price per ingredient across NGN catalog rows only.
const CMSUP_CHEAPEST_NGN = (() => {
  const best = {};
  CM_SUPPLIERS.forEach(s => s.catalog.forEach(r => {
    if (r.ccy !== 'NGN') return;
    const unit = r.packPrice / r.packSize;
    if (!best[r.ing] || unit < best[r.ing].unit) best[r.ing] = { unit, supplier: s.id };
  }));
  return best;
})();

function ScreenCommerceSuppliers() {
  const [sel, setSel] = React.useState(CM_SUPPLIERS[1]);   // FreshFarm — linkNote + cheaper-than-current tomato

  const catalogRows = CM_SUPPLIERS.reduce((a, s) => a + s.catalog.length, 0);
  const distinctIngs = new Set(CM_SUPPLIERS.flatMap(s => s.catalog.map(r => r.ing))).size;
  const linked = CM_SUPPLIERS.filter(s => s.party).length;
  const ngn = CM_SUPPLIERS.filter(s => s.ccy === 'NGN').length;
  const avgLead = Math.round(CM_SUPPLIERS.reduce((a, s) => a + s.lead, 0) / CM_SUPPLIERS.length);

  const kpis = [
    { l: 'Suppliers', v: CM_SUPPLIERS.length, s: ngn + ' NGN, ' + (CM_SUPPLIERS.length - ngn) + ' GBP' },
    { l: 'Party-linked', v: linked, s: 'their POs carry a Supplier role' },
    { l: 'Catalog rows', v: catalogRows, s: 'packs priced across ' + distinctIngs + ' ingredients' },
    { l: 'Avg lead time', v: avgLead + 'd', s: 'across all suppliers' },
  ];

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <style>{`.cm-suprow:hover{background:var(--surface-inset);cursor:pointer;}`}</style>
      <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Header */}
        <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 16 }}>
          <div>
            <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Suppliers</div>
            <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>Supplier master data and pack economics for raw-material sourcing. Unit prices derive from the pack; purchase-order suggestions only ever use rows in the order's currency.</div>
          </div>
          <button className="btn btn-sm"><Icon name="plus" size={12} /> New supplier</button>
        </div>

        {/* KPIs */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
          {kpis.map(k => (
            <div key={k.l} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
              <div style={{ fontSize: 11, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>{k.l}</div>
              <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', marginTop: 4, fontFamily: 'var(--font-mono)' }}>{k.v}</div>
              <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{k.s}</div>
            </div>
          ))}
        </div>

        {/* Suppliers table */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1.3fr 96px 84px 110px 84px 140px', gap: 12, padding: '10px 14px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
            <div>Supplier</div><div>Currency</div><div>Lead</div><div>Terms</div><div style={{ textAlign: 'right' }}>Catalog</div><div>Linkage</div>
          </div>
          {CM_SUPPLIERS.map((s, i) => (
            <div key={s.id} className="cm-suprow" onClick={() => setSel(s)} style={{ display: 'grid', gridTemplateColumns: '1.3fr 96px 84px 110px 84px 140px', gap: 12, padding: '11px 14px', alignItems: 'center', borderBottom: i < CM_SUPPLIERS.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <Avatar name={s.name} size={30} />
                <div>
                  <div style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{s.name}</div>
                  <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>{s.party ? s.party.name : 'no linked party'}</div>
                </div>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 5, fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-secondary)' }}>
                {s.ccy}
                {s.mismatchNote && <Icon name="alertc" size={12} color="var(--warning)" />}
              </div>
              <div style={{ color: 'var(--text-secondary)', fontSize: 12 }}>{s.lead} days</div>
              <div style={{ color: 'var(--text-secondary)', fontSize: 12 }}>{s.terms}</div>
              <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }}>{s.catalog.length}</div>
              <div>
                {s.party
                  ? <Pill tone="tint" dot size="sm">Party-linked</Pill>
                  : <Pill tone="muted" dot size="sm">Provenance-only</Pill>}
              </div>
            </div>
          ))}
        </div>

        <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Icon name="link" size={12} color="var(--brand-primary)" /> Party-linked suppliers attach a Supplier party role to their purchase orders; provenance-only suppliers are recorded on the PO without one (053 §11).
        </div>
      </div>

      {sel && <CmSupDrawer sup={sel} onClose={() => setSel(null)} />}
    </div>
  );
}

function CmSupDrawer({ sup, onClose }) {
  return (
    <React.Fragment>
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(20,25,30,0.28)', zIndex: 35 }} />
      <div style={{ position: 'absolute', top: 0, right: 0, bottom: 0, width: 600, background: 'var(--surface)', borderLeft: '1px solid var(--border-light)', boxShadow: 'var(--shadow-lg)', zIndex: 36, display: 'flex', flexDirection: 'column' }}>
        {/* Drawer header */}
        <div style={{ padding: '18px 22px 16px', borderBottom: '1px solid var(--border-light)', display: 'flex', alignItems: 'center', gap: 12 }}>
          <Avatar name={sup.name} size={40} />
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ fontSize: 15, fontWeight: 700, color: 'var(--text-primary)' }}>{sup.name}</span>
              {sup.party
                ? <Pill tone="tint" dot size="sm">Party-linked</Pill>
                : <Pill tone="muted" dot size="sm">Provenance-only</Pill>}
            </div>
            <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', marginTop: 2 }}>{sup.catalog.length} catalog rows, all priced in <span style={{ fontFamily: 'var(--font-mono)' }}>{sup.ccy}</span></div>
          </div>
          <button onClick={onClose} style={{ width: 26, height: 26, borderRadius: 6, border: '1px solid var(--border-light)', background: 'var(--surface)', cursor: 'pointer', display: 'grid', placeItems: 'center' }}><Icon name="close" size={13} color="var(--text-secondary)" /></button>
        </div>

        <div style={{ flex: 1, overflow: 'auto', padding: 22, display: 'flex', flexDirection: 'column', gap: 16 }}>
          {/* Identity */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 8 }}>
            {[['Currency', sup.ccy], ['Lead time', sup.lead + ' days'], ['Terms', sup.terms]].map(([l, v]) => (
              <div key={l} style={{ background: 'var(--surface-inset)', borderRadius: 9, padding: '10px 12px' }}>
                <div style={{ fontSize: 9.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase' }}>{l}</div>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 14, fontWeight: 700, color: 'var(--text-primary)', marginTop: 3 }}>{v}</div>
              </div>
            ))}
          </div>

          {/* Party linkage — the 053 §11 rule */}
          {sup.party ? (
            <div style={{ display: 'flex', gap: 9, padding: '11px 13px', borderRadius: 10, background: 'var(--brand-primary-10)', borderLeft: '3px solid var(--brand-primary)' }}>
              <Icon name="users" size={14} color="var(--brand-primary)" style={{ flex: 'none', marginTop: 1 }} />
              <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>
                Linked to <b style={{ color: 'var(--text-primary)' }}>{sup.party.name}</b> <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5 }}>{sup.party.id}</span> — purchase orders to this supplier attach a Supplier party role to the Order (053 §11).
              </div>
            </div>
          ) : (
            <div style={{ display: 'flex', gap: 9, padding: '11px 13px', borderRadius: 10, background: 'var(--surface-inset)', borderLeft: '3px solid var(--border)' }}>
              <Icon name="link" size={14} color="var(--text-tertiary)" style={{ flex: 'none', marginTop: 1 }} />
              <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>{sup.linkNote || 'Not party-linked — purchase orders record this supplier as provenance only; no Supplier role is attached to the Order (053 §11).'}</div>
            </div>
          )}

          {/* Currency-mismatch honesty guard (Albion) */}
          {sup.mismatchNote && (
            <div style={{ display: 'flex', gap: 9, padding: '11px 13px', borderRadius: 10, background: 'var(--warning-light)', borderLeft: '3px solid var(--warning)' }}>
              <Icon name="alertc" size={14} color="var(--warning)" style={{ flex: 'none', marginTop: 1 }} />
              <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>{sup.mismatchNote}</div>
            </div>
          )}

          {/* Catalog — pack economics */}
          <div>
            <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 8 }}>Catalog — pack economics</div>
            <div style={{ border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1.2fr 92px 148px 158px 44px', gap: 10, padding: '8px 13px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
                <div>Ingredient</div><div>SKU</div><div>Pack</div><div>Unit price</div><div style={{ textAlign: 'right' }}>Lead</div>
              </div>
              {sup.catalog.map((r, i) => {
                const ing = cmSupIngById(r.ing);
                const derived = r.packPrice / r.packSize;
                const cheapest = r.ccy === 'NGN' && CMSUP_CHEAPEST_NGN[r.ing] && CMSUP_CHEAPEST_NGN[r.ing].supplier === sup.id;
                const belowCurrent = r.ccy === 'NGN' && ing.cost && ing.cost.ccy === 'NGN' && derived < ing.cost.current;
                return (
                  <div key={r.sku} style={{ display: 'grid', gridTemplateColumns: '1.2fr 92px 148px 158px 44px', gap: 10, padding: '9px 13px', alignItems: 'center', borderBottom: i < sup.catalog.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <span style={{ width: 24, height: 24, borderRadius: 6, background: 'var(--surface-inset)', display: 'grid', placeItems: 'center', fontSize: 12, flex: 'none' }}>{ing.emoji}</span>
                      <span style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{ing.name}</span>
                    </div>
                    <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)' }}>{r.sku}</div>
                    <div style={{ color: 'var(--text-primary)' }}>{r.packLabel} @ {cmMoney(r.packPrice, r.ccy)}</div>
                    <div>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                        <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)' }}>{cmMoney(derived, r.ccy)}/{r.unit}</span>
                        {cheapest && <Pill tone="tint" size="sm">cheapest</Pill>}
                      </div>
                      {belowCurrent && <div style={{ fontSize: 10, color: 'var(--success)', marginTop: 1 }}>below current {cmMoney(ing.cost.current)}/{ing.unit}</div>}
                    </div>
                    <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)' }}>{r.lead}d</div>
                  </div>
                );
              })}
            </div>
            <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 6 }}>"cheapest" compares derived unit prices across NGN catalog rows only — a GBP row never wins an NGN comparison.</div>
          </div>

          {/* Catalog upsert (theatre) */}
          <div style={{ border: '1px solid var(--border-light)', borderRadius: 10, padding: 14, display: 'flex', flexDirection: 'column', gap: 10 }}>
            <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>Add or update a catalog row</div>
            <div style={{ display: 'grid', gridTemplateColumns: '1.2fr 1fr', gap: 8 }}>
              <div>
                <div style={{ fontSize: 10, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 4 }}>Ingredient</div>
                <select defaultValue="ing-tomato" style={{ width: '100%', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '7px 10px', fontSize: 12.5, color: 'var(--text-primary)' }}>
                  {CM_INGREDIENTS.filter(i => i.active).map(i => <option key={i.id} value={i.id}>{i.name}</option>)}
                </select>
              </div>
              <div>
                <div style={{ fontSize: 10, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 4 }}>Supplier SKU</div>
                <input placeholder="FF-TOM-10" style={{ width: '100%', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '7px 10px', fontSize: 12.5, fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }} />
              </div>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 8 }}>
              <div>
                <div style={{ fontSize: 10, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 4 }}>Pack size</div>
                <div style={{ display: 'flex', gap: 6 }}>
                  <input defaultValue={10} style={{ width: 64, background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '7px 10px', fontSize: 12.5, fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }} />
                  <select defaultValue="kg" style={{ flex: 1, background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '7px 8px', fontSize: 12.5, color: 'var(--text-primary)' }}>
                    {['kg', 'L', 'each'].map(u => <option key={u}>{u}</option>)}
                  </select>
                </div>
              </div>
              <div>
                <div style={{ fontSize: 10, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 4 }}>Pack price ({sup.ccy})</div>
                <input defaultValue={7500} style={{ width: '100%', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '7px 10px', fontSize: 12.5, fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }} />
              </div>
              <div>
                <div style={{ fontSize: 10, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 4 }}>Lead (days)</div>
                <input defaultValue={sup.lead} style={{ width: '100%', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '7px 10px', fontSize: 12.5, fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }} />
              </div>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>Upserts by supplier + ingredient — the derived unit price updates with the pack.</span>
              <button className="btn btn-primary btn-sm"><Icon name="check" size={12} /> Save catalog row</button>
            </div>
          </div>
        </div>

        <div style={{ flex: 'none', padding: '14px 22px', borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)', display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
          <button className="btn btn-outline btn-sm" onClick={onClose}>Close</button>
          <button className="btn btn-primary btn-sm"><Icon name="cart" size={12} /> New purchase order</button>
        </div>
      </div>
    </React.Fragment>
  );
}

Object.assign(window, { ScreenCommerceSuppliers });
