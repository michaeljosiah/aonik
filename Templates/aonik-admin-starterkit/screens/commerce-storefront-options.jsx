// Commerce · Storefront — Spec 066: Personalisation (option groups)
//   GET/POST /commerce/admin/option-groups            · group catalogue
//   PUT      /commerce/admin/option-groups/{id}        · label/mode/currency/active
//   POST     /commerce/admin/option-groups/{id}/choices · add choice
//   PUT      /commerce/admin/option-choices/{id}       · label/note/price/active
//   PUT      /commerce/admin/option-groups/{id}/recommended-default
//            → RecommendedDefaultChangeResult{ affectedProductSlugs } — feeds 067 review
//   PUT      /commerce/admin/products/{id}/option-groups · per-dish narrowing (full replace)
//   PUT      /commerce/admin/products/{id}/surcharge     · per-unit signature upgrade
// Choices price as SIGNED DELTAS against the group's recommended default; the
// storefront's "Abby's choice" label comes from config, never hard-coded here.

function ScreenStorefrontOptions() {
  const [groupKey, setGroupKey] = React.useState('side');
  const [dish, setDish] = React.useState(null);
  const groups = CS_OPTION_GROUPS;
  const g = groups.find(x => x.key === groupKey);
  // Spec 066 §8 — stored choice prices are ABSOLUTE; the storefront shows the
  // delta against the recommended default, so we derive it the same way here.
  const dfltPrice = (g.choices.find(c => c.dflt) || { price: 0 }).price;
  const choiceCount = groups.reduce((a, x) => a + x.choices.length, 0);
  const narrowed = CS_DISHES.filter(d => d.groups.length > 0);
  const surcharged = CS_DISHES.filter(d => d.surcharge != null);

  const kpis = [
    { l: 'Option groups', v: groups.length, s: 'tenant catalogue, GBP' },
    { l: 'Choices', v: choiceCount, s: 'priced as deltas vs default' },
    { l: 'Products narrowed', v: narrowed.length + ' of ' + CS_DISHES.length, s: 'per-product offer' },
    { l: 'Unit surcharges', v: surcharged.length, s: 'per-unit, on top of the bundle' },
  ];

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <style>{`.cs-orow:hover{background:var(--surface-inset);cursor:pointer;} .cs-grow:hover{border-color:var(--brand-primary);cursor:pointer;}`}</style>
      <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        <div>
          <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Personalisation</div>
          <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>The option catalogue every product narrows from. Choices price as signed deltas against the recommended default — the storefront labels that default "{CS_CONFIG.recommendedChoiceLabel}" (a config value, not code).</div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
          {kpis.map(k => (
            <div key={k.l} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
              <div style={{ fontSize: 11, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>{k.l}</div>
              <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', marginTop: 4, fontFamily: 'var(--font-mono)' }}>{k.v}</div>
              <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{k.s}</div>
            </div>
          ))}
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '250px 1fr', gap: 16, alignItems: 'start' }}>
          {/* Group rail */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {groups.map(x => {
              const on = x.key === groupKey;
              const dflt = x.choices.find(c => c.dflt);
              return (
                <div key={x.key} className="cs-grow" onClick={() => setGroupKey(x.key)}
                     style={{ background: 'var(--surface)', border: '1px solid ' + (on ? 'var(--brand-primary)' : 'var(--border-light)'), borderRadius: 10, padding: '12px 14px', boxShadow: on ? '0 1px 6px rgba(5,90,96,0.14)' : 'none' }}>
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                    <span style={{ fontSize: 13.5, fontWeight: 600, color: 'var(--text-primary)' }}>{x.label}</span>
                    <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>{x.choices.length}</span>
                  </div>
                  <div style={{ fontSize: 11, color: 'var(--text-tertiary)', marginTop: 3, display: 'flex', alignItems: 'center', gap: 5 }}>
                    <Icon name="star" size={10} color="var(--brand-primary)" />
                    <span>{dflt ? dflt.label : 'no default'}</span>
                  </div>
                </div>
              );
            })}
            <button className="btn btn-outline btn-sm" style={{ justifyContent: 'center' }}><Icon name="plus" size={12} /> New group</button>
          </div>

          {/* Selected group detail */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
              <div style={{ padding: '13px 16px', borderBottom: '1px solid var(--border-light)', display: 'flex', alignItems: 'center', gap: 10 }}>
                <span style={{ fontSize: 14.5, fontWeight: 700, color: 'var(--text-primary)' }}>{g.label}</span>
                <Pill tone="tint" size="sm">Choose one</Pill>
                <span style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>{g.help}</span>
                <span style={{ marginLeft: 'auto', fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>{g.ccy}</span>
                <button className="btn btn-ghost btn-sm"><Icon name="edit" size={12} /> Edit group</button>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '26px 1fr 110px 120px 90px', gap: 12, padding: '9px 16px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
                <div></div><div>Choice</div><div style={{ textAlign: 'right' }}>vs default</div><div>Status</div><div style={{ textAlign: 'right' }}></div>
              </div>
              {g.choices.map((c, i) => (
                <div key={c.key} className="cs-orow" style={{ display: 'grid', gridTemplateColumns: '26px 1fr 110px 120px 90px', gap: 12, padding: '10px 16px', alignItems: 'center', borderBottom: i < g.choices.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5 }}>
                  <div>{c.dflt && <Icon name="star" size={13} color="var(--brand-primary)" />}</div>
                  <div>
                    <span style={{ color: 'var(--text-primary)', fontWeight: c.dflt ? 600 : 500 }}>{c.label}</span>
                    {c.note && <span style={{ fontSize: 11, color: 'var(--text-tertiary)', marginLeft: 8 }}>{c.note}</span>}
                    {c.dflt && <span style={{ fontSize: 10, fontWeight: 700, color: 'var(--brand-primary)', background: 'var(--brand-primary-10)', padding: '1.5px 7px', borderRadius: 999, marginLeft: 8 }}>{CS_CONFIG.recommendedChoiceLabel}</span>}
                  </div>
                  <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 600, color: (c.price - dfltPrice) > 0 ? 'var(--text-primary)' : (c.price - dfltPrice) < 0 ? 'var(--success)' : 'var(--text-tertiary)' }}>{csSigned(c.price - dfltPrice)}</div>
                  <div><Pill tone={c.active ? 'success' : 'muted'} dot size="sm">{c.active ? 'Active' : 'Retired'}</Pill></div>
                  <div style={{ textAlign: 'right' }}>
                    {!c.dflt && <button className="btn btn-ghost btn-sm" title="Move the recommended default here — reports every affected product"><Icon name="star" size={11} /> Make default</button>}
                  </div>
                </div>
              ))}
              <div style={{ padding: '9px 16px', borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <button className="btn btn-ghost btn-sm"><Icon name="plus" size={11} /> Add choice</button>
                <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>Stored prices are absolute (066 §8) — the deltas above derive against the default.</span>
              </div>
            </div>

            {/* The consequence card — yesterday's default move */}
            {g.key === CS_DEFAULT_MOVE.group && (
              <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderLeft: '3px solid var(--warning)', borderRadius: 10, padding: '13px 16px' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <Icon name="warn" size={14} color="var(--warning)" />
                  <span style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>Default moved: {csGroup(CS_DEFAULT_MOVE.group).choices.find(c => c.key === CS_DEFAULT_MOVE.from).label} → {csGroup(CS_DEFAULT_MOVE.group).choices.find(c => c.key === CS_DEFAULT_MOVE.to).label}</span>
                  <span style={{ fontSize: 11, color: 'var(--text-tertiary)', marginLeft: 'auto', fontFamily: 'var(--font-mono)' }}>{CS_DEFAULT_MOVE.when}</span>
                </div>
                <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 6, lineHeight: 1.5 }}>
                  "The standard preparation" just changed for {CS_DEFAULT_MOVE.affected.length} products. Their content blocks are flagged for review — figures keep serving, declarations arrive withheld until each is confirmed. Five have been confirmed since; Suya-Spiced Salmon is still open in the queue.
                </div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginTop: 9 }}>
                  {CS_DEFAULT_MOVE.affected.map(s => {
                    const d = csDish(s);
                    return <span key={s} style={{ fontSize: 11, color: 'var(--text-secondary)', background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 999, padding: '3px 10px' }}>{d.emoji} {d.name}</span>;
                  })}
                  <button className="btn btn-outline btn-sm" style={{ marginLeft: 'auto' }}>Open review queue <Icon name="arrowright" size={11} /></button>
                </div>
              </div>
            )}
          </div>
        </div>

        {/* Per-dish narrowing */}
        <div>
          <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 8 }}>Per-product narrowing</div>
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 300px 130px 110px', gap: 12, padding: '10px 14px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
              <div>Product</div><div>Offers</div><div style={{ textAlign: 'right' }}>Unit surcharge</div><div></div>
            </div>
            {CS_DISHES.filter(d => d.status === 'active').map((d, i, arr) => (
              <div key={d.slug} className="cs-orow" onClick={() => setDish(d)} style={{ display: 'grid', gridTemplateColumns: '1fr 300px 130px 110px', gap: 12, padding: '10px 14px', alignItems: 'center', borderBottom: i < arr.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
                  <span style={{ fontSize: 16 }}>{d.emoji}</span>
                  <div><div style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{d.name}</div><div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{d.slug}</div></div>
                </div>
                <div style={{ display: 'flex', gap: 5, flexWrap: 'wrap' }}>
                  {d.groups.length === 0
                    ? <span style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>Not personalisable — panel hidden</span>
                    : d.groups.map(k => <span key={k} style={{ fontSize: 10.5, color: 'var(--text-secondary)', background: 'var(--surface-inset)', borderRadius: 5, padding: '2px 8px' }}>{csGroup(k).label}</span>)}
                </div>
                <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 600, color: d.surcharge ? 'var(--text-primary)' : 'var(--text-tertiary)' }}>{d.surcharge ? '+' + csMoney(d.surcharge).slice(0) : '—'}</div>
                <div style={{ textAlign: 'right', fontSize: 11.5, color: 'var(--brand-primary)', fontWeight: 600 }}>Edit offer</div>
              </div>
            ))}
          </div>
        </div>
      </div>
      {dish && <CsNarrowingDrawer d={dish} onClose={() => setDish(null)} />}
    </div>
  );
}

// Drawer — one dish's narrowing: which groups it offers, which choices are
// allowed, its default override and surcharge. Full-replace semantics (066).
function CsNarrowingDrawer({ d, onClose }) {
  return (
    <>
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(20,25,30,0.28)', zIndex: 35 }} />
      <div style={{ position: 'absolute', top: 0, right: 0, bottom: 0, width: 520, background: 'var(--surface)', borderLeft: '1px solid var(--border-light)', boxShadow: '-12px 0 32px -8px rgba(0,0,0,0.18)', zIndex: 36, display: 'flex', flexDirection: 'column' }}>
        <div style={{ padding: '18px 22px 16px', borderBottom: '1px solid var(--border-light)', display: 'flex', alignItems: 'center', gap: 12 }}>
          <span style={{ fontSize: 26 }}>{d.emoji}</span>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ fontSize: 15, fontWeight: 700, color: 'var(--text-primary)' }}>{d.name}</div>
            <div style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{d.slug}</div>
          </div>
          <button onClick={onClose} style={{ width: 26, height: 26, borderRadius: 6, border: '1px solid var(--border-light)', background: 'var(--surface)', cursor: 'pointer', display: 'grid', placeItems: 'center' }}><Icon name="close" size={13} color="var(--text-secondary)" /></button>
        </div>
        <div style={{ flex: 1, overflow: 'auto', padding: 22, display: 'flex', flexDirection: 'column', gap: 16 }}>
          {CS_OPTION_GROUPS.map(g => {
            const offered = d.groups.includes(g.key);
            return (
              <div key={g.key} style={{ border: '1px solid var(--border-light)', borderRadius: 10, padding: '12px 14px', opacity: offered ? 1 : 0.55 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <span style={{ width: 15, height: 15, borderRadius: 4, border: '1px solid ' + (offered ? 'var(--brand-primary)' : 'var(--border-medium)'), background: offered ? 'var(--brand-primary)' : 'transparent', display: 'grid', placeItems: 'center' }}>{offered && <Icon name="check" size={10} color="#fff" />}</span>
                  <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{g.label}</span>
                  {!offered && <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>not offered on this product</span>}
                </div>
                {offered && (() => {
                  // Spec 066 — narrowing is group inclusion AND the allowed-choice /
                  // default-override intersection; excluded choices render struck so a
                  // full-replace save persists exactly what the operator sees.
                  const nr = (d.narrow || {})[g.key] || {};
                  const allowed = nr.allowed || g.choices.map(c => c.key);
                  const effDflt = nr.dflt || (g.choices.find(x => x.dflt) || {}).key;
                  const gDflt = (g.choices.find(x => x.dflt) || { price: 0 }).price;
                  return (
                    <div style={{ marginTop: 9 }}>
                      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                        {g.choices.map(c => {
                          const inOffer = allowed.includes(c.key);
                          const isDflt = c.key === effDflt;
                          const dl = c.price - gDflt;
                          return (
                            <span key={c.key} style={{ display: 'inline-flex', alignItems: 'center', gap: 5, fontSize: 11.5, color: !inOffer ? 'var(--text-tertiary)' : isDflt ? 'var(--brand-primary)' : 'var(--text-secondary)', background: !inOffer ? 'transparent' : isDflt ? 'var(--brand-primary-10)' : 'var(--surface-inset)', border: '1px ' + (inOffer ? 'solid' : 'dashed') + ' ' + (inOffer && isDflt ? 'var(--brand-primary)' : 'var(--border-light)'), borderRadius: 999, padding: '4px 11px', fontWeight: isDflt ? 600 : 500, textDecoration: inOffer ? 'none' : 'line-through', opacity: inOffer ? 1 : 0.6 }}>
                              {isDflt && <Icon name="star" size={10} color="var(--brand-primary)" />}
                              {c.label}
                              {inOffer && dl !== 0 && <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5 }}>{csSigned(dl)}</span>}
                            </span>
                          );
                        })}
                      </div>
                      {(nr.allowed || nr.dflt) && (
                        <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 6 }}>
                          {nr.allowed ? 'Narrowed — struck choices are excluded from this product; the full-replace save persists exactly this intersection.' : ''}
                          {nr.dflt ? ' Default overridden for this product.' : ''}
                        </div>
                      )}
                    </div>
                  );
                })()}
              </div>
            );
          })}
          <div style={{ border: '1px solid var(--border-light)', borderRadius: 10, padding: '12px 14px', display: 'flex', alignItems: 'center', gap: 12 }}>
            <div style={{ flex: 1 }}>
              <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>Unit surcharge</div>
              <div style={{ fontSize: 11, color: 'var(--text-tertiary)', marginTop: 2 }}>The one price-like field a product card may show — an on-top-of-the-bundle delta, never a standalone price.</div>
            </div>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 15, fontWeight: 700, color: d.surcharge ? 'var(--text-primary)' : 'var(--text-tertiary)' }}>{d.surcharge ? '+' + csMoney(d.surcharge) : '—'}</span>
          </div>
        </div>
        <div style={{ flex: 'none', padding: '14px 22px', borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)', display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
          <button className="btn btn-outline btn-sm" onClick={onClose}>Close</button>
          <button className="btn btn-primary btn-sm"><Icon name="check" size={12} /> Save offer</button>
        </div>
      </div>
    </>
  );
}

Object.assign(window, { ScreenStorefrontOptions });
