// Commerce · Storefront — Specs 070 + 071: Merchandising
//   GET/POST /commerce/admin/collections            · list/create
//   PUT      /commerce/admin/collections/{id}       · rename/kind/sort/active
//   PUT      /commerce/admin/collections/{id}/items · FULL-REPLACE ranked membership
//   GET/POST /commerce/admin/facet-groups           · filter definitions
//   PUT      /commerce/admin/facet-groups/{id}      · label/options/sort/active
//                                                     (key + match kind are immutable)
//   GET      /commerce/catalog/extras               · the extras rail the customer sees
// Ranks are unique among active members; drafts stage invisibly and surface on
// activation. The extras collection is the ONE place dishes carry retail prices.

function ScreenStorefrontMerch() {
  const [tab, setTab] = React.useState('Collections');
  const [colId, setColId] = React.useState('col-feat');
  const col = CS_COLLECTIONS.find(c => c.id === colId);
  const isExtras = col.slug === CS_CONFIG.extrasCollectionSlug;
  const members = CS_COLLECTIONS.reduce((a, c) => a + c.items.length, 0);

  const kpis = [
    { l: 'Collections', v: CS_COLLECTIONS.length, s: 'homepage rails + extras' },
    { l: 'Curated members', v: members, s: 'ranked placements' },
    { l: 'Facet groups', v: CS_FACETS.length, s: 'every menu filter, as data' },
    { l: 'Drafts staged', v: 1, s: 'invisible until activation' },
  ];

  return (
    <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
      <style>{`.cs-mrow:hover{background:var(--surface-inset);cursor:pointer;}`}</style>
      <div>
        <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Merchandising</div>
        <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>What the shop window shows and how the menu filters — all of it data. Adding, renaming or retiring a rail or a filter group needs no frontend change.</div>
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

      <div style={{ display: 'inline-flex', padding: 4, gap: 2, background: 'var(--surface-inset)', borderRadius: 10, alignSelf: 'flex-start' }}>
        {['Collections', 'Facet groups'].map(t => {
          const on = tab === t;
          return <button key={t} onClick={() => setTab(t)} style={{ height: 30, padding: '0 14px', borderRadius: 8, cursor: 'pointer', border: 'none', fontSize: 12, fontWeight: on ? 600 : 500, background: on ? 'var(--surface)' : 'transparent', color: on ? 'var(--text-primary)' : 'var(--text-secondary)', boxShadow: on ? '0 1px 3px rgba(20,25,30,0.10)' : 'none' }}>{t}</button>;
        })}
      </div>

      {tab === 'Collections' ? (
        <div style={{ display: 'grid', gridTemplateColumns: '270px 1fr', gap: 16, alignItems: 'start' }}>
          {/* Collection list */}
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
            {CS_COLLECTIONS.map((c, i) => {
              const on = c.id === colId;
              return (
                <div key={c.id} className="cs-mrow" onClick={() => setColId(c.id)} style={{ padding: '11px 14px', borderBottom: i < CS_COLLECTIONS.length - 1 ? '1px solid var(--border-light)' : 'none', background: on ? 'var(--brand-primary-10)' : 'transparent', borderLeft: '3px solid ' + (on ? 'var(--brand-primary)' : 'transparent') }}>
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                    <span style={{ fontSize: 12.5, fontWeight: on ? 700 : 600, color: 'var(--text-primary)' }}>{c.title}</span>
                    <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>{c.items.length}</span>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 7, marginTop: 3 }}>
                    <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)' }}>{c.slug}</span>
                    {c.slug === CS_CONFIG.extrasCollectionSlug && <span style={{ fontSize: 9, fontWeight: 700, letterSpacing: '0.04em', color: 'var(--brand-primary)', background: 'var(--brand-primary-10)', padding: '1.5px 6px', borderRadius: 4 }}>EXTRAS RAIL</span>}
                  </div>
                </div>
              );
            })}
            <div style={{ padding: '9px 14px', background: 'var(--surface-inset)', borderTop: '1px solid var(--border-light)' }}>
              <button className="btn btn-ghost btn-sm"><Icon name="plus" size={11} /> New collection</button>
            </div>
          </div>

          {/* Selected collection: ranked members + live rail preview */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
              <div style={{ padding: '13px 16px', borderBottom: '1px solid var(--border-light)', display: 'flex', alignItems: 'center', gap: 10 }}>
                <div style={{ flex: 1 }}>
                  <div style={{ fontSize: 14.5, fontWeight: 700, color: 'var(--text-primary)' }}>{col.title}</div>
                  <div style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{col.subtitle} · rank is the curated sort inside a collection</div>
                </div>
                <Pill tone={col.active ? 'success' : 'muted'} dot size="sm">{col.active ? 'Active' : 'Inactive'}</Pill>
                <button className="btn btn-ghost btn-sm"><Icon name="edit" size={12} /> Edit</button>
              </div>
              {col.items.map((it, i) => {
                const d = it.name ? null : csDish(it.slug);
                const draft = d && d.status === 'draft';
                return (
                  <div key={it.slug} style={{ display: 'grid', gridTemplateColumns: '30px 40px 1fr 130px 110px', gap: 10, padding: '9px 16px', alignItems: 'center', borderBottom: i < col.items.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5, opacity: draft ? 0.55 : 1 }}>
                    <Icon name="menu" size={13} color="var(--text-tertiary)" />
                    <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, fontWeight: 700, color: 'var(--text-tertiary)' }}>{String(it.rank).padStart(2, '0')}</span>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <span style={{ fontSize: 15 }}>{it.emoji || (d && d.emoji)}</span>
                      <span style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{it.name || (d && d.name)}</span>
                    </div>
                    <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontWeight: 600, color: it.price != null ? 'var(--text-primary)' : 'var(--text-tertiary)' }}>{it.price != null ? csMoney(it.price) : isExtras ? '—' : ''}</div>
                    <div style={{ textAlign: 'right' }}>
                      {draft
                        ? <Pill tone="warning" size="sm">Draft — staged</Pill>
                        : isExtras && it.price == null
                        ? <Pill tone="warning" size="sm">Unpriceable — skipped publicly</Pill>
                        : <Pill tone="success" dot size="sm">Live</Pill>}
                    </div>
                  </div>
                );
              })}
              <div style={{ padding: '9px 16px', borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <button className="btn btn-ghost btn-sm"><Icon name="plus" size={11} /> Add member</button>
                <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>Reorder saves as one full replace — an idempotent PUT of every rank.</span>
              </div>
            </div>

            {isExtras && col.skipped > 0 && (
              <div style={{ background: 'var(--warning-light)', border: '1px solid var(--warning)', borderRadius: 10, padding: '10px 14px', fontSize: 12, color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: 8 }}>
                <Icon name="warn" size={13} color="var(--warning)" />
                <span><b>{col.skipped} member skipped on the public read</b> — {col.skippedNote}.</span>
              </div>
            )}

            {/* The rail as the customer sees it */}
            <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '13px 16px' }}>
              <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 10 }}>Storefront preview — rank order, drafts hidden</div>
              <div style={{ display: 'flex', gap: 10, overflow: 'auto', paddingBottom: 4 }}>
                {col.items.filter(it => { const d = it.name ? null : csDish(it.slug); return !(d && d.status === 'draft') && !(isExtras && it.price == null); }).map(it => {
                  const d = it.name ? null : csDish(it.slug);
                  return (
                    <div key={it.slug} style={{ flex: 'none', width: 118, border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden', background: 'var(--surface)' }}>
                      <div style={{ height: 58, background: 'var(--surface-inset)', display: 'grid', placeItems: 'center', fontSize: 24 }}>{it.emoji || (d && d.emoji)}</div>
                      <div style={{ padding: '7px 9px' }}>
                        <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-primary)', lineHeight: 1.3, height: 27, overflow: 'hidden' }}>{it.name || (d && d.name)}</div>
                        {it.price != null
                          ? <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, fontWeight: 700, color: 'var(--text-primary)', marginTop: 3 }}>{csMoney(it.price)}</div>
                          : <div style={{ fontSize: 9.5, color: 'var(--text-tertiary)', marginTop: 3 }}>no standalone price</div>}
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          </div>
        </div>
      ) : (
        /* Facet groups */
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
          <div style={{ display: 'grid', gridTemplateColumns: '150px 110px 190px 1fr 90px', gap: 12, padding: '10px 14px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
            <div>Group</div><div>Match kind</div><div>Source</div><div>Options — the storefront submits values, never labels</div><div></div>
          </div>
          {CS_FACETS.map((f, i) => (
            <div key={f.id} style={{ display: 'grid', gridTemplateColumns: '150px 110px 190px 1fr 90px', gap: 12, padding: '11px 14px', alignItems: 'start', borderBottom: i < CS_FACETS.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5 }}>
              <div>
                <div style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{f.label}</div>
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>facet.{f.key}</div>
              </div>
              <div><Pill tone={f.match === 'Range' ? 'tint' : 'muted'} size="sm">{f.match}</Pill></div>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: f.source ? 'var(--text-secondary)' : 'var(--text-tertiary)', paddingTop: 3 }}>{f.source || '—'}</div>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 5 }}>
                {f.options.map(o => (
                  <span key={o.v} style={{ fontSize: 10.5, color: 'var(--text-secondary)', background: 'var(--surface-inset)', borderRadius: 5, padding: '2.5px 8px' }}>
                    {o.l}
                    {o.min != null && <span style={{ fontFamily: 'var(--font-mono)', fontSize: 9.5, color: 'var(--text-tertiary)', marginLeft: 5 }}>[{o.min},{o.max})</span>}
                  </span>
                ))}
              </div>
              <div style={{ textAlign: 'right', fontSize: 11.5, color: 'var(--brand-primary)', fontWeight: 600, cursor: 'pointer', paddingTop: 3 }}>Edit</div>
            </div>
          ))}
          <div style={{ padding: '9px 14px', borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <button className="btn btn-ghost btn-sm"><Icon name="plus" size={11} /> New facet group</button>
            <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>A live group's key and match kind never change — retire and replace instead.</span>
          </div>
        </div>
      )}
    </div>
  );
}

Object.assign(window, { ScreenStorefrontMerch });
