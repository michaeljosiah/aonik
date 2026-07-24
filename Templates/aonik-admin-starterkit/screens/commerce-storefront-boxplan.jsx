// Commerce · Storefront — Spec 068: Box plans (size-tiered container pricing)
//   PUT /commerce/admin/products/{id}/size-plan   · full replace (formula + presets)
//   GET /commerce/catalog/products/{slug}/box-plan · what Step 1 renders
// Presets WIN at their size; every other size prices basePrice + (size − baseSize)
// × perSpacePrice. Growing a box always charges boxPrice(target) − boxPrice(current)
// — the marginal cost bends around discounted presets, so it is never a flat
// per-dish figure. Savings are AUTHORED display values, never computed.

function ScreenStorefrontBoxPlan() {
  const plan = CS_PLAN;
  const sizes = [];
  for (let s = 6; s <= 14; s++) sizes.push(s);
  const formula = s => plan.basePrice + (s - plan.baseSize) * plan.perSpace;

  // Chart geometry — 660×240 plot, y spans £85–£225.
  const X = s => 40 + (s - 6) * (600 / 8);
  const Y = v => 210 - ((v - 85) / 140) * 185;
  const fPts = sizes.map(s => X(s) + ',' + Y(formula(s))).join(' ');
  const ePts = sizes.map(s => X(s) + ',' + Y(csBoxPrice(s))).join(' ');
  const grows = [[6, 8], [8, 12], [12, 14]].map(([a, b]) => ({ from: a, to: b, cost: csBoxPrice(b) - csBoxPrice(a) }));

  const kpis = [
    { l: 'Sizes', v: plan.min + '–' + plan.max, s: 'units per box' },
    { l: 'Base', v: csMoney(plan.basePrice), s: 'at the ' + plan.baseSize + '-box' },
    { l: 'Per space', v: csMoney(plan.perSpace), s: 'formula beyond base' },
    { l: 'Presets', v: plan.presets.length, s: 'merchandised price points' },
  ];

  return (
    <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
      <div>
        <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Box plans</div>
        <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>Container pricing for {plan.bundleName} — Step 1's entire pricing UI in one read. Presets override the formula at their size; growing a box charges the difference between the two box prices, never per-space × spaces.</div>
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

      <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: 16, alignItems: 'start' }}>
        {/* The price curve */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 14, marginBottom: 6 }}>
            <span style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>What the customer pays by size</span>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5, fontSize: 10.5, color: 'var(--text-tertiary)' }}><span style={{ width: 16, height: 0, borderTop: '2px dashed var(--border-medium)' }} /> formula</span>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5, fontSize: 10.5, color: 'var(--text-tertiary)' }}><span style={{ width: 16, height: 2, background: 'var(--brand-primary)' }} /> effective</span>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5, fontSize: 10.5, color: 'var(--text-tertiary)' }}><span style={{ width: 8, height: 8, borderRadius: 99, background: 'var(--brand-primary)' }} /> preset wins</span>
          </div>
          <svg viewBox="0 0 660 240" style={{ width: '100%', height: 'auto', display: 'block' }}>
            {[95, 125, 155, 185, 215].map(v => (
              <g key={v}>
                <line x1="40" x2="640" y1={Y(v)} y2={Y(v)} stroke="var(--border-light)" strokeWidth="1" />
                <text x="34" y={Y(v) + 3.5} textAnchor="end" fontSize="9.5" fill="var(--text-tertiary)" fontFamily="var(--font-mono)">£{v}</text>
              </g>
            ))}
            {sizes.map(s => (
              <text key={s} x={X(s)} y="228" textAnchor="middle" fontSize="10" fill="var(--text-tertiary)" fontFamily="var(--font-mono)">{s}</text>
            ))}
            <polyline points={fPts} fill="none" stroke="var(--border-medium)" strokeWidth="1.8" strokeDasharray="5 4" />
            <polyline points={ePts} fill="none" stroke="var(--brand-primary)" strokeWidth="2.4" strokeLinejoin="round" />
            {plan.presets.map(p => (
              <g key={p.size}>
                <circle cx={X(p.size)} cy={Y(p.price)} r="5.5" fill="var(--brand-primary)" stroke="var(--surface)" strokeWidth="2" />
                {p.price < formula(p.size) && (
                  <g>
                    <line x1={X(p.size)} x2={X(p.size)} y1={Y(p.price)} y2={Y(formula(p.size))} stroke="var(--brand-primary)" strokeWidth="1" strokeDasharray="2 3" opacity="0.6" />
                    <text x={X(p.size) + 9} y={(Y(p.price) + Y(formula(p.size))) / 2 + 3} fontSize="9.5" fill="var(--success)" fontFamily="var(--font-mono)">−£{formula(p.size) - p.price}</text>
                  </g>
                )}
                <text x={X(p.size)} y={Y(p.price) + 20} textAnchor="middle" fontSize="10" fontWeight="700" fill="var(--text-primary)" fontFamily="var(--font-mono)">£{p.price}</text>
              </g>
            ))}
          </svg>
          {/* Marginal grow costs read off the curve */}
          <div style={{ display: 'flex', gap: 10, marginTop: 10, paddingTop: 12, borderTop: '1px solid var(--border-light)' }}>
            {grows.map(gr => (
              <div key={gr.from} style={{ flex: 1, background: 'var(--surface-inset)', borderRadius: 8, padding: '9px 12px' }}>
                <div style={{ fontSize: 10, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--text-tertiary)' }}>Grow {gr.from} → {gr.to}</div>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 15, fontWeight: 700, color: 'var(--text-primary)', marginTop: 2 }}>+{csMoney(gr.cost)}</div>
                <div style={{ fontSize: 10.5, color: 'var(--text-secondary)' }}>{csMoney(csBoxPrice(gr.to))} − {csMoney(csBoxPrice(gr.from))}</div>
              </div>
            ))}
          </div>
        </div>

        {/* Formula editor */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px', display: 'flex', flexDirection: 'column', gap: 11 }}>
          <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>Plan formula</div>
          {[['Bundle', plan.bundleName + '  ·  ' + plan.bundleSlug, false],
            ['Size range', plan.min + ' – ' + plan.max, true],
            ['Base size', String(plan.baseSize), true],
            ['Base price', csMoney(plan.basePrice), true],
            ['Per space', csMoney(plan.perSpace), true],
            ['Currency', plan.ccy, true]].map(([l, v, mono]) => (
            <div key={l} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10 }}>
              <span style={{ fontSize: 11.5, color: 'var(--text-secondary)' }}>{l}</span>
              <span style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)', fontFamily: mono ? 'var(--font-mono)' : 'var(--font-sans)', background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 7, padding: '5px 10px', minWidth: 96, textAlign: 'right' }}>{v}</span>
            </div>
          ))}
          <div style={{ fontSize: 11, color: 'var(--text-tertiary)', lineHeight: 1.5, borderTop: '1px solid var(--border-light)', paddingTop: 10 }}>
            Any size without a preset prices as <span style={{ fontFamily: 'var(--font-mono)' }}>£{plan.basePrice} + (size − {plan.baseSize}) × £{plan.perSpace}</span>. Saving a plan is a full replace.
          </div>
          <button className="btn btn-primary btn-sm" style={{ alignSelf: 'flex-end' }}><Icon name="check" size={12} /> Save plan</button>
        </div>
      </div>

      {/* Presets */}
      <div>
        <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 8 }}>Merchandised presets — they always win at their size</div>
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
          <div style={{ display: 'grid', gridTemplateColumns: '70px 110px 110px 110px 140px 1fr 90px', gap: 12, padding: '10px 14px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
            <div style={{ textAlign: 'right' }}>Size</div><div style={{ textAlign: 'right' }}>Price</div><div style={{ textAlign: 'right' }}>Formula</div><div style={{ textAlign: 'right' }}>Authored saving</div><div>Badge</div><div>Blurb</div><div></div>
          </div>
          {plan.presets.map((p, i) => (
            <div key={p.size} style={{ display: 'grid', gridTemplateColumns: '70px 110px 110px 110px 140px 1fr 90px', gap: 12, padding: '11px 14px', alignItems: 'center', borderBottom: i < plan.presets.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5 }}>
              <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 700, color: 'var(--text-primary)' }}>{p.size}</div>
              <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 600, color: 'var(--text-primary)' }}>{csMoney(p.price)}</div>
              <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>{csMoney(formula(p.size))}</div>
              <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: p.saving ? 'var(--success)' : 'var(--text-tertiary)' }}>{p.saving ? '−' + csMoney(p.saving) : '—'}</div>
              <div>{p.badge && <Pill tone={p.badge === 'Most popular' ? 'tint' : 'muted'} size="sm">{p.badge}</Pill>}</div>
              <div style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{p.blurb}</div>
              <div style={{ textAlign: 'right', fontSize: 11.5, color: 'var(--brand-primary)', fontWeight: 600, cursor: 'pointer' }}>Edit</div>
            </div>
          ))}
          <div style={{ padding: '9px 14px', borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <button className="btn btn-ghost btn-sm"><Icon name="plus" size={11} /> Add preset</button>
            <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>Savings are display values authored here — the storefront never computes one.</span>
          </div>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { ScreenStorefrontBoxPlan });
