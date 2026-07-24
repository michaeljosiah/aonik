// Commerce · Storefront — Spec 070 §9: Storefront config (the tunables document)
//   PUT /commerce/admin/storefront-config    · typed write; null members leave the
//                                              stored setting unchanged; an explicit
//                                              empty string clears a tenant override
//   GET /commerce/config/storefront          · the public document — NEVER 404s;
//                                              served with Vary: X-Tenant-Id
// Everything the frontend must never hard-code, edited in one place. The right
// column renders the document exactly as AbbysTable's pages consume it.

function ScreenStorefrontConfig() {
  const cfg = CS_CONFIG;
  const plan = CS_PLAN;
  const free = cfg.delivery.charged === 0;

  const Field = ({ label, hint, value, mono, wide }) => (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
      <div style={{ display: 'flex', alignItems: 'baseline', gap: 8 }}>
        <span style={{ fontSize: 11.5, fontWeight: 600, color: 'var(--text-primary)' }}>{label}</span>
        {hint && <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>{hint}</span>}
      </div>
      <div style={{ fontSize: 12.5, fontWeight: 500, color: 'var(--text-primary)', fontFamily: mono ? 'var(--font-mono)' : 'var(--font-sans)', background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '8px 11px', width: wide ? 'auto' : 'fit-content', minWidth: 120 }}>{value}</div>
    </div>
  );

  return (
    <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
      <div>
        <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Storefront config</div>
        <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>The single document of tunables the frontend must never hard-code. Edit on the left; the right shows the document exactly as the customer's pages consume it — the same read, the same values.</div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, alignItems: 'start' }}>
        {/* The document */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '16px 18px', display: 'flex', flexDirection: 'column', gap: 15 }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>The document</span>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>Commerce.Storefront.* · tenant scope</span>
          </div>

          <Field label="Recommended-choice label" hint="what the personaliser calls the default" value={cfg.recommendedChoiceLabel} />
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
            <Field label="Results page size" hint="menu grid" value={cfg.resultsPageSize} mono />
            <Field label="Canonical currency" hint="from Tenant.DefaultCurrency" value={cfg.currency} mono />
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
            <Field label="Delivery — list amount" hint="struck through" value={csMoney(cfg.delivery.list)} mono />
            <Field label="Delivery — charged" hint="0 renders as free" value={csMoney(cfg.delivery.charged)} mono />
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
            <Field label="Default box" hint="the bundle Step 1 builds" value={cfg.defaultBoxSlug} mono />
            <Field label="Extras collection" hint="the Spec 071 rail" value={cfg.extrasCollectionSlug} mono />
          </div>
          <Field label="Back-to-top trigger" hint="served verbatim to the frontend" value={cfg.backToTop} mono wide />

          <div style={{ fontSize: 11, color: 'var(--text-tertiary)', lineHeight: 1.55, borderTop: '1px solid var(--border-light)', paddingTop: 11 }}>
            Omitted members leave stored values unchanged; an explicit empty string clears a tenant override back to the default. The public read never 404s — an unconfigured storefront gets a valid minimal document — and serves with <span style={{ fontFamily: 'var(--font-mono)' }}>Vary: X-Tenant-Id</span>.
          </div>
          <button className="btn btn-primary btn-sm" style={{ alignSelf: 'flex-end' }}><Icon name="check" size={12} /> Save config</button>
        </div>

        {/* What the customer sees */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
            <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 10 }}>The personaliser</div>
            <div style={{ border: '1px solid var(--border-light)', borderRadius: 10, padding: '12px 14px' }}>
              <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 8 }}>Protein</div>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 11.5, fontWeight: 600, color: 'var(--brand-primary)', background: 'var(--brand-primary-10)', border: '1px solid var(--brand-primary)', borderRadius: 999, padding: '5px 12px' }}>
                  Chicken
                  <span style={{ fontSize: 9, fontWeight: 700, background: 'var(--brand-primary)', color: '#fff', borderRadius: 4, padding: '1px 6px' }}>{cfg.recommendedChoiceLabel}</span>
                </span>
                <span style={{ fontSize: 11.5, color: 'var(--text-secondary)', background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 999, padding: '5px 12px' }}>Beef</span>
                <span style={{ fontSize: 11.5, color: 'var(--text-secondary)', background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 999, padding: '5px 12px' }}>Suya salmon <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5 }}>+£1.50</span></span>
              </div>
              <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 8 }}>The badge text is the label configured on the left — change it there and every product page follows.</div>
            </div>
          </div>

          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
            <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 10 }}>The order summary's delivery line</div>
            <div style={{ border: '1px solid var(--border-light)', borderRadius: 10, padding: '12px 14px', display: 'flex', flexDirection: 'column', gap: 7 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12.5, color: 'var(--text-secondary)' }}>
                <span>Box (8 dishes)</span><span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }}>{csMoney(csBoxPrice(8))}</span>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12.5, color: 'var(--text-secondary)' }}>
                <span>Delivery</span>
                <span>
                  <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)', textDecoration: 'line-through', marginRight: 8 }}>{csMoney(cfg.delivery.list)}</span>
                  <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 700, color: free ? 'var(--success)' : 'var(--text-primary)' }}>{free ? 'Free' : csMoney(cfg.delivery.charged)}</span>
                </span>
              </div>
              <div style={{ height: 1, background: 'var(--border-light)' }} />
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
                <span style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>Total</span>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: 15, fontWeight: 700, color: 'var(--text-primary)' }}>{csMoney(csBoxPrice(8) + cfg.delivery.charged)}</span>
              </div>
            </div>
          </div>

          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
            <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 10 }}>Step 1 — from the embedded box plan</div>
            <div style={{ display: 'flex', gap: 8 }}>
              {plan.presets.map(p => (
                <div key={p.size} style={{ flex: 1, border: '1px solid ' + (p.badge === 'Most popular' ? 'var(--brand-primary)' : 'var(--border-light)'), borderRadius: 10, padding: '10px 12px', textAlign: 'center', position: 'relative' }}>
                  {p.badge === 'Most popular' && <span style={{ position: 'absolute', top: -8, left: '50%', transform: 'translateX(-50%)', fontSize: 8.5, fontWeight: 700, letterSpacing: '0.04em', color: '#fff', background: 'var(--brand-primary)', borderRadius: 999, padding: '2px 8px', whiteSpace: 'nowrap' }}>{p.badge.toUpperCase()}</span>}
                  <div style={{ fontSize: 13, fontWeight: 700, color: 'var(--text-primary)' }}>{p.size} units</div>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: 14, fontWeight: 700, color: 'var(--text-primary)', marginTop: 3 }}>{csMoney(p.price)}</div>
                  {p.saving && <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--success)', marginTop: 2 }}>save {csMoney(p.saving)}</div>}
                </div>
              ))}
            </div>
            <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 9 }}>Null when no default box is set — the two states are indistinguishable by design, and the frontend treats both as "no box to render".</div>
          </div>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { ScreenStorefrontConfig });
