// Commerce · Storefront — Spec 067: Food content (option-dependent, never computed)
//   PUT    /commerce/admin/products/{id}/content              · default block upsert
//   POST   /commerce/admin/products/{id}/content/confirm-review · clear RequiresReview
//   POST   /commerce/admin/products/{id}/content-variants     · author a combination
//   PUT    /commerce/admin/content-variants/{id}              · edit
//   DELETE /commerce/admin/content-variants/{id}              · retire (soft, revivable)
//   GET    /commerce/admin/products/{id}/content-coverage     · single-choice gaps
// The safety model this screen renders: figures may fall back to the default
// block (captioned "standard preparation"), but ingredients, allergens and
// heating are EXACT-AUTHORED OR WITHHELD — never substituted, never inherited.

const CS_CONTENT_STATE = {
  authored: { tone: 'success', label: 'Authored' },
  review:   { tone: 'warning', label: 'Review' },
  withheld: { tone: 'muted',   label: 'Withheld' },
};

function ScreenStorefrontContent() {
  const [slug, setSlug] = React.useState('suya-salmon');
  const d = csDish(slug);
  const c = d.content;
  const authored = CS_DISHES.filter(x => x.content.state === 'authored');
  const review = CS_DISHES.filter(x => x.content.state === 'review');
  const variants = CS_DISHES.reduce((a, x) => a + x.content.variants.length, 0);
  const gaps = CS_DISHES.reduce((a, x) => a + x.content.gaps.length, 0);

  const kpis = [
    { l: 'Products published', v: authored.length + ' of ' + CS_DISHES.length, s: 'default blocks serving' },
    { l: 'Combination variants', v: variants, s: 'exact-authored declarations' },
    { l: 'Coverage gaps', v: gaps, s: 'single-choice combinations unauthored' },
    { l: 'Awaiting review', v: review.length, s: 'default combination changed', warn: true },
  ];

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <style>{`.cs-drow:hover{background:var(--surface-inset);cursor:pointer;}`}</style>
      <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
        <div>
          <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Product content</div>
          <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>Option-dependent declarations — figures, ingredients, allergens, usage steps — exactly as the customer reads them. Declarations are exact-authored or withheld; the salmon variant can never surface the standard preparation's shellfish line.</div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
          {kpis.map(k => (
            <div key={k.l} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
              <div style={{ fontSize: 11, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>{k.l}</div>
              <div style={{ fontSize: 22, fontWeight: 700, color: k.warn && parseInt(k.v) > 0 ? 'var(--warning)' : 'var(--text-primary)', marginTop: 4, fontFamily: 'var(--font-mono)' }}>{k.v}</div>
              <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{k.s}</div>
            </div>
          ))}
        </div>

        {/* Review queue — fed by 066 default moves and retired choices */}
        {review.length > 0 && (
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderLeft: '3px solid var(--warning)', borderRadius: 10, padding: '13px 16px', display: 'flex', flexDirection: 'column', gap: 10 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <Icon name="warn" size={14} color="var(--warning)" />
              <span style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>Review queue</span>
              <span style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>while flagged, declarations serve withheld — figures keep serving, captioned</span>
            </div>
            {review.map(x => (
              <div key={x.slug} style={{ display: 'flex', alignItems: 'center', gap: 10, background: 'var(--surface-inset)', borderRadius: 8, padding: '9px 12px' }}>
                <span style={{ fontSize: 15 }}>{x.emoji}</span>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <span style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{x.name}</span>
                  <span style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginLeft: 8 }}>{x.content.reviewReason}</span>
                </div>
                <button className="btn btn-outline btn-sm" onClick={() => setSlug(x.slug)}>Open</button>
                <button className="btn btn-primary btn-sm"><Icon name="check" size={12} /> Confirm review</button>
              </div>
            ))}
            <InlineProposal agent="Chef" confidence={92} summary="Egusi's flagged variant points at the retired okra side — retiring that variant clears its block. Suya salmon needs a fresh look before confirming: plantain adds ~90 kcal over the old salad default." />
          </div>
        )}

        <div style={{ display: 'grid', gridTemplateColumns: '250px 1fr', gap: 16, alignItems: 'start' }}>
          {/* Dish rail */}
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
            {CS_DISHES.filter(x => x.status !== 'draft').map((x, i, arr) => {
              const on = x.slug === slug;
              const st = CS_CONTENT_STATE[x.content.state];
              return (
                <div key={x.slug} className="cs-drow" onClick={() => setSlug(x.slug)} style={{ display: 'flex', alignItems: 'center', gap: 9, padding: '10px 13px', borderBottom: i < arr.length - 1 ? '1px solid var(--border-light)' : 'none', background: on ? 'var(--brand-primary-10)' : 'transparent', borderLeft: '3px solid ' + (on ? 'var(--brand-primary)' : 'transparent') }}>
                  <span style={{ fontSize: 15 }}>{x.emoji}</span>
                  <span style={{ fontSize: 12.5, fontWeight: on ? 600 : 500, color: 'var(--text-primary)', flex: 1, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{x.name}</span>
                  <Pill tone={st.tone} dot size="sm">{st.label}</Pill>
                </div>
              );
            })}
          </div>

          {/* Workbench — the food label as the customer reads it */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            {c.state === 'review' && (
              <div style={{ background: 'var(--warning-light)', border: '1px solid var(--warning)', borderRadius: 10, padding: '10px 14px', fontSize: 12, color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: 8 }}>
                <Icon name="warn" size={13} color="var(--warning)" />
                <span><b>Under review</b> — {c.reviewReason}. Customers currently see figures captioned as the standard preparation, with declarations withheld.</span>
              </div>
            )}
            {!c.fig ? (
              <div style={{ background: 'var(--surface)', border: '1px dashed var(--border-medium)', borderRadius: 10, padding: '38px 20px', textAlign: 'center' }}>
                <Icon name="file" size={22} color="var(--text-tertiary)" />
                <div style={{ fontSize: 13.5, fontWeight: 600, color: 'var(--text-primary)', marginTop: 8 }}>Nothing published for {d.name}</div>
                <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 4, maxWidth: 420, margin: '4px auto 0' }}>The dish page shows its explicit "not yet published" state. Nothing is inferred, nothing is borrowed from another dish — authoring the block below is the only way content appears.</div>
                <button className="btn btn-primary btn-sm" style={{ marginTop: 14 }}><Icon name="plus" size={12} /> Author the default block</button>
              </div>
            ) : (
              <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
                <div style={{ padding: '13px 16px', borderBottom: '1px solid var(--border-light)', display: 'flex', alignItems: 'center', gap: 10 }}>
                  <span style={{ fontSize: 18 }}>{d.emoji}</span>
                  <div style={{ flex: 1 }}>
                    <div style={{ fontSize: 14.5, fontWeight: 700, color: 'var(--text-primary)' }}>{d.name}</div>
                    <div style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{c.servingLabel}</div>
                  </div>
                  <button className="btn btn-ghost btn-sm"><Icon name="edit" size={12} /> Edit block</button>
                </div>

                {/* The figure grid — nulls render as "not published", never zero */}
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, 1fr)', borderBottom: '1px solid var(--border-light)' }}>
                  {[['Energy', c.fig.kcal, 'kcal'], ['Protein', c.fig.protein, 'g'], ['Carbs', c.fig.carbs, 'g'], ['Fat', c.fig.fat, 'g'], ['Fibre', c.fig.fibre, 'g'], ['Sugars', c.fig.sugars, 'g'], ['Salt', c.fig.salt, 'g']].map(([l, v, u], i) => (
                    <div key={l} style={{ padding: '12px 10px', borderLeft: i ? '1px solid var(--border-light)' : 'none', textAlign: 'center' }}>
                      <div style={{ fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>{l}</div>
                      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 16, fontWeight: 700, color: 'var(--text-primary)', marginTop: 3 }}>{v != null ? v : '—'}</div>
                      <div style={{ fontSize: 9.5, color: 'var(--text-tertiary)' }}>{u}</div>
                    </div>
                  ))}
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 0 }}>
                  <div style={{ padding: '13px 16px', borderRight: '1px solid var(--border-light)' }}>
                    <div style={{ fontSize: 10, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)', marginBottom: 6 }}>Ingredients</div>
                    <div style={{ fontSize: 12, color: 'var(--text-secondary)', lineHeight: 1.55 }}>{c.state === 'review' ? <span style={{ color: 'var(--warning)', fontStyle: 'italic' }}>Withheld while under review</span> : c.ingredients ? c.ingredients : <span style={{ color: 'var(--text-tertiary)', fontStyle: 'italic' }}>Not yet published — figures serve, declarations never substitute</span>}</div>
                  </div>
                  <div style={{ padding: '13px 16px' }}>
                    <div style={{ fontSize: 10, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)', marginBottom: 6 }}>Allergens</div>
                    {c.state === 'review'
                      ? <div style={{ fontSize: 12, color: 'var(--warning)', fontStyle: 'italic' }}>Withheld while under review — never substituted</div>
                      : c.allergens
                      ? <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{c.allergens}</div>
                      : <div style={{ fontSize: 12, color: 'var(--text-tertiary)', fontStyle: 'italic' }}>Not yet published for this product — exact-authored or absent, never substituted</div>}
                  </div>
                </div>

                {(c.heating.length > 0 || c.state === 'review') && (
                  <div style={{ padding: '13px 16px', borderTop: '1px solid var(--border-light)' }}>
                    <div style={{ fontSize: 10, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)', marginBottom: 8 }}>Heating</div>
                    {c.state === 'review'
                      ? <div style={{ fontSize: 12, color: 'var(--warning)', fontStyle: 'italic' }}>Withheld while under review — the old timings may not fit the new standard combination</div>
                      : <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                          {c.heating.map((h, i) => (
                            <div key={i} style={{ display: 'flex', gap: 10, fontSize: 12 }}>
                              <span style={{ fontWeight: 600, color: 'var(--text-primary)', minWidth: 84 }}>{h.m}</span>
                              <span style={{ color: 'var(--text-secondary)' }}>{h.b}</span>
                            </div>
                          ))}
                        </div>}
                  </div>
                )}
              </div>
            )}

            {/* Combination variants + coverage gaps */}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
              <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '13px 16px' }}>
                <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 8 }}>Combination variants</div>
                {c.variants.length === 0
                  ? <div style={{ fontSize: 12, color: 'var(--text-tertiary)' }}>None — every combination serves the default block's figures, captioned as the standard preparation.</div>
                  : c.variants.map((v, i) => (
                    <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '7px 0', borderTop: i ? '1px solid var(--border-light)' : 'none', fontSize: 12 }}>
                      <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-primary)', background: 'var(--surface-inset)', borderRadius: 5, padding: '2px 8px' }}>{v.sel}</span>
                      <span style={{ color: v.stale ? 'var(--warning)' : 'var(--text-secondary)', flex: 1 }}>{v.note}</span>
                      <button className="btn btn-ghost btn-sm"><Icon name="edit" size={11} /></button>
                    </div>
                  ))}
                <button className="btn btn-outline btn-sm" style={{ marginTop: 10 }}><Icon name="plus" size={11} /> Author a combination</button>
              </div>
              <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '13px 16px' }}>
                <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 8 }}>Coverage gaps</div>
                {c.gaps.length === 0
                  ? <div style={{ fontSize: 12, color: 'var(--text-tertiary)' }}>No single-choice gaps — every offered choice substituted alone has an authored combination or falls back honestly.</div>
                  : c.gaps.map((gp, i) => (
                    <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '7px 0', borderTop: i ? '1px solid var(--border-light)' : 'none', fontSize: 12 }}>
                      <Icon name="warn" size={12} color="var(--warning)" />
                      <span style={{ color: 'var(--text-secondary)', flex: 1 }}>{csGroup(gp.group).label}: <b style={{ color: 'var(--text-primary)' }}>{csGroup(gp.group).choices.find(ch => ch.key === gp.choice).label}</b> — no authored declaration; customers see the withheld state</span>
                      <button className="btn btn-ghost btn-sm">Author</button>
                    </div>
                  ))}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { ScreenStorefrontContent });
