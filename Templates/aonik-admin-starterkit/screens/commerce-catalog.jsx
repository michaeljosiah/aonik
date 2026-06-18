// Commerce · Spec 042 — Catalog group
// Admin pages for the Aonik.Commerce retail module's catalog surface:
//   • ScreenCommerceProducts — product catalog (rail + KPIs + Kind/status filters +
//     grid/list), with a tabbed product editor drawer: Details · Variants · Pricing ·
//     Media · Bundle (the build-your-own-box / composite-product builder, §12).
//   • ScreenCommerceCategories — the parent/child category tree.
// Driver domain: a wellness-food storefront. Reuses Icon / Pill / AonikMark.
// Design only — backend (Phases 1–5) already landed on master.

const CM_KIND = {
  simple:  { label: 'Simple',  color: '#5a6a76' },
  variant: { label: 'Variant', color: '#0e7490' },
  bundle:  { label: 'Bundle',  color: '#7b76b6' },
};
const CM_STATUS = {
  active:   { tone: 'success', label: 'Active' },
  draft:    { tone: 'warning', label: 'Draft' },
  archived: { tone: 'muted',   label: 'Archived' },
};

const cmMoney = (a, ccy = 'NGN') =>
  a == null ? '—' : ccy === 'GBP' ? '£' + a.toFixed(2) : ccy === 'USD' ? '$' + a.toFixed(2) : '₦' + a.toLocaleString('en-NG');

// ─── Categories (flat with parent ref → tree on the categories screen) ────
const CM_CATEGORIES = [
  { id: 'granola', name: 'Granola & Cereals', parent: null,     count: 3, slug: 'granola' },
  { id: 'boxes',   name: 'Wellness Boxes',    parent: null,     count: 3, slug: 'boxes', bundle: true },
  { id: 'drinks',  name: 'Cold-Brew & Drinks',parent: null,     count: 3, slug: 'drinks' },
  { id: 'coldbrew',name: 'Cold-Brew',         parent: 'drinks', count: 1, slug: 'cold-brew' },
  { id: 'smoothies',name:'Smoothies',         parent: 'drinks', count: 1, slug: 'smoothies' },
  { id: 'shots',   name: 'Wellness Shots',    parent: null,     count: 2, slug: 'shots' },
  { id: 'snacks',  name: 'Snacks & Bites',    parent: null,     count: 3, slug: 'snacks' },
  { id: 'bars',    name: 'Protein Bars',      parent: 'snacks', count: 1, slug: 'protein-bars' },
];
const CM_TOP_CATS = CM_CATEGORIES.filter(c => !c.parent);
const cmCatName = id => (CM_CATEGORIES.find(c => c.id === id) || {}).name || '—';

// ─── Products (wellness-food catalog) ─────────────────────────────────────
// variant: { sku, opt, weight, active, ngn, gbp, onHand, reserved }
const CM_PRODUCTS = [
  { id: 'p-alm', name: 'Almond & Honey Granola', slug: 'almond-honey-granola', cat: 'granola', kind: 'variant', status: 'active', emoji: '🥣', color: '#b4741e', tags: ['bestseller', 'vegan'], media: 4,
    variants: [
      { sku: 'GRN-ALM-250', opt: '250 g', weight: 250, active: true, ngn: 2800, gbp: 4.50, onHand: 120, reserved: 8 },
      { sku: 'GRN-ALM-500', opt: '500 g', weight: 500, active: true, ngn: 4500, gbp: 7.20, onHand: 86,  reserved: 4 },
      { sku: 'GRN-ALM-1KG', opt: '1 kg',  weight: 1000, active: true, ngn: 8000, gbp: 12.80, onHand: 40, reserved: 2 },
    ] },
  { id: 'p-cac', name: 'Cacao Crunch Granola', slug: 'cacao-crunch-granola', cat: 'granola', kind: 'variant', status: 'active', emoji: '🥣', color: '#6b4226', tags: ['vegan'], media: 3,
    variants: [
      { sku: 'GRN-CAC-500', opt: '500 g', weight: 500, active: true, ngn: 4800, gbp: 7.60, onHand: 64, reserved: 3 },
      { sku: 'GRN-CAC-1KG', opt: '1 kg',  weight: 1000, active: true, ngn: 8600, gbp: 13.60, onHand: 28, reserved: 1 },
    ] },
  { id: 'p-ber', name: 'Berry Bliss Granola', slug: 'berry-bliss-granola', cat: 'granola', kind: 'simple', status: 'active', emoji: '🍓', color: '#b03060', tags: [], media: 2,
    variants: [{ sku: 'GRN-BER-500', opt: '500 g', weight: 500, active: true, ngn: 5200, gbp: 8.20, onHand: 9, reserved: 5 }] },
  { id: 'p-cb', name: 'Cold-Brew Coffee', slug: 'cold-brew-coffee', cat: 'drinks', kind: 'variant', status: 'active', emoji: '☕', color: '#3a2a1a', tags: ['caffeine'], media: 5,
    variants: [
      { sku: 'DRK-CB-250', opt: '250 ml', weight: 280, active: true, ngn: 1800, gbp: 2.90, onHand: 200, reserved: 12 },
      { sku: 'DRK-CB-1L',  opt: '1 L',     weight: 1050, active: true, ngn: 5500, gbp: 8.80, onHand: 74, reserved: 6 },
    ] },
  { id: 'p-zobo', name: 'Hibiscus Cooler (Zobo)', slug: 'hibiscus-cooler', cat: 'drinks', kind: 'simple', status: 'active', emoji: '🧃', color: '#9b1c31', tags: ['vegan'], media: 2,
    variants: [{ sku: 'DRK-ZOBO-500', opt: '500 ml', weight: 540, active: true, ngn: 1500, gbp: 2.40, onHand: 150, reserved: 9 }] },
  { id: 'p-smooth', name: 'Green Smoothie', slug: 'green-smoothie', cat: 'drinks', kind: 'variant', status: 'draft', emoji: '🥤', color: '#2e7d32', tags: [], media: 1,
    variants: [
      { sku: 'DRK-SMO-ORG', opt: 'Original', weight: 320, active: true, ngn: 2200, gbp: 3.50, onHand: 0, reserved: 0 },
      { sku: 'DRK-SMO-GIN', opt: 'Ginger',   weight: 320, active: true, ngn: 2400, gbp: 3.80, onHand: 0, reserved: 0 },
    ] },
  { id: 'p-ginger', name: 'Ginger Wellness Shot', slug: 'ginger-wellness-shot', cat: 'shots', kind: 'variant', status: 'active', emoji: '🫚', color: '#c79100', tags: ['bestseller'], media: 3,
    variants: [
      { sku: 'SHOT-GIN-1',  opt: 'Single',   weight: 60,  active: true, ngn: 900,  gbp: 1.50, onHand: 320, reserved: 24 },
      { sku: 'SHOT-GIN-6',  opt: '6-pack',   weight: 360, active: true, ngn: 5000, gbp: 8.00, onHand: 88,  reserved: 6 },
      { sku: 'SHOT-GIN-12', opt: '12-pack',  weight: 720, active: true, ngn: 9400, gbp: 15.00, onHand: 42, reserved: 2 },
    ] },
  { id: 'p-turm', name: 'Turmeric Shot', slug: 'turmeric-shot', cat: 'shots', kind: 'simple', status: 'active', emoji: '🟡', color: '#d4a017', tags: [], media: 1,
    variants: [{ sku: 'SHOT-TUR-1', opt: 'Single', weight: 60, active: true, ngn: 950, gbp: 1.55, onHand: 6, reserved: 4 }] },
  { id: 'p-bar', name: 'Protein Energy Bar', slug: 'protein-energy-bar', cat: 'snacks', kind: 'variant', status: 'active', emoji: '🍫', color: '#5d4037', tags: ['protein'], media: 4,
    variants: [
      { sku: 'SNK-BAR-CAC', opt: 'Cacao',  weight: 60, active: true, ngn: 1200, gbp: 1.90, onHand: 140, reserved: 7 },
      { sku: 'SNK-BAR-PNT', opt: 'Peanut', weight: 60, active: true, ngn: 1200, gbp: 1.90, onHand: 96,  reserved: 5 },
      { sku: 'SNK-BAR-BER', opt: 'Berry',  weight: 60, active: false, ngn: 1300, gbp: 2.05, onHand: 0,  reserved: 0 },
    ] },
  { id: 'p-coco', name: 'Coconut Bites', slug: 'coconut-bites', cat: 'snacks', kind: 'simple', status: 'archived', emoji: '🥥', color: '#8a8a8a', tags: [], media: 1,
    variants: [{ sku: 'SNK-COCO-120', opt: '120 g', weight: 120, active: false, ngn: 1800, gbp: 2.85, onHand: 0, reserved: 0 }] },
  // Bundles (build-your-own-box / composite)
  { id: 'p-byob', name: 'Build-Your-Own Wellness Box', slug: 'byo-wellness-box', cat: 'boxes', kind: 'bundle', status: 'active', emoji: '📦', color: '#055a60', tags: ['bestseller', 'gift'], media: 3,
    variants: [],
    bundle: { mode: 'fixed', fixed: 12000, premium: null, ccy: 'NGN',
      slots: [{ name: 'Pick any 6', min: 6, max: 6, from: 'granola', allowDup: true, src: 'category' }] } },
  { id: 'p-break', name: 'Breakfast Box', slug: 'breakfast-box', cat: 'boxes', kind: 'bundle', status: 'active', emoji: '🧺', color: '#0e7490', tags: ['gift'], media: 2,
    variants: [],
    bundle: { mode: 'sum', fixed: null, premium: null, ccy: 'NGN',
      slots: [
        { name: 'Choose 3 granola', min: 3, max: 3, from: 'granola', allowDup: true, src: 'category' },
        { name: 'Choose 2 drinks',  min: 2, max: 2, from: 'drinks',  allowDup: false, src: 'category' },
      ] } },
  { id: 'p-detox', name: 'Detox Starter Box', slug: 'detox-starter-box', cat: 'boxes', kind: 'bundle', status: 'draft', emoji: '🌿', color: '#2e7d32', tags: [], media: 1,
    variants: [],
    bundle: { mode: 'sumplus', fixed: null, premium: 1500, ccy: 'NGN',
      slots: [{ name: 'Pick 4 shots', min: 4, max: 4, from: 'shots', allowDup: true, src: 'category' }] } },
];

// derived helpers
const cmStock = p => p.variants.reduce((a, v) => ({ onHand: a.onHand + v.onHand, reserved: a.reserved + v.reserved }), { onHand: 0, reserved: 0 });
const cmAvail = p => { const s = cmStock(p); return s.onHand - s.reserved; };
const cmLow = p => p.kind !== 'bundle' && p.status === 'active' && cmAvail(p) <= 10;
const cmPriceFrom = p => {
  if (p.kind === 'bundle') return p.bundle.mode === 'fixed' ? p.bundle.fixed : null;
  const prices = p.variants.map(v => v.ngn).filter(Boolean);
  return prices.length ? Math.min(...prices) : null;
};

// ═══ Products catalog screen ═════════════════════════════════════════════
function ScreenCommerceProducts() {
  const [cat, setCat] = React.useState('all');
  const [kind, setKind] = React.useState('all');
  const [view, setView] = React.useState('grid');
  const [sel, setSel] = React.useState(null);   // product for editor drawer

  let list = CM_PRODUCTS;
  if (cat !== 'all') list = list.filter(p => p.cat === cat);
  if (kind !== 'all') list = list.filter(p => p.kind === kind);

  const kpis = [
    { l: 'Products', v: CM_PRODUCTS.length, s: CM_PRODUCTS.filter(p => p.status === 'active').length + ' active' },
    { l: 'Variants / SKUs', v: CM_PRODUCTS.reduce((a, p) => a + p.variants.length, 0), s: 'across all products' },
    { l: 'Low stock', v: CM_PRODUCTS.filter(cmLow).length, s: '≤ 10 available', warn: true },
    { l: 'Bundles', v: CM_PRODUCTS.filter(p => p.kind === 'bundle').length, s: 'build-your-own boxes' },
  ];

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <style>{`.cm-card{transition:border-color 140ms ease,box-shadow 140ms ease,transform 140ms ease;}
.cm-card:hover{border-color:var(--border-medium)!important;box-shadow:0 4px 14px -8px rgba(20,25,30,0.18);transform:translateY(-1px);}
.cm-row:hover{background:var(--surface-inset);}`}</style>

      <div style={{ height: '100%', display: 'grid', gridTemplateColumns: '220px 1fr', overflow: 'hidden' }}>
        {/* Category rail */}
        <div style={{ borderRight: '1px solid var(--border-light)', padding: '20px 14px', overflow: 'auto', display: 'flex', flexDirection: 'column', gap: 2, background: 'var(--surface-inset)' }}>
          <div style={{ fontSize: 10, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.08em', color: 'var(--text-tertiary)', padding: '4px 8px 8px' }}>Categories</div>
          {[{ id: 'all', name: 'All products', count: CM_PRODUCTS.length }, ...CM_TOP_CATS].map(c => (
            <button key={c.id} onClick={() => setCat(c.id)} style={{
              display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '8px 10px', borderRadius: 6, border: 'none', cursor: 'pointer',
              background: cat === c.id ? 'var(--brand-primary-10)' : 'transparent',
              color: cat === c.id ? 'var(--brand-primary)' : 'var(--text-secondary)',
              fontSize: 12.5, fontWeight: cat === c.id ? 600 : 500, textAlign: 'left',
            }}>
              <span>{c.name}</span>
              <span style={{ fontSize: 11, fontFamily: 'var(--font-mono)', opacity: 0.7 }}>{c.id === 'all' ? CM_PRODUCTS.length : CM_PRODUCTS.filter(p => p.cat === c.id).length}</span>
            </button>
          ))}
          <div style={{ height: 1, background: 'var(--border-light)', margin: '12px 4px' }} />
          <button style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 10px', borderRadius: 6, border: '1px dashed var(--border-medium)', cursor: 'pointer', background: 'transparent', color: 'var(--text-secondary)', fontSize: 12.5, justifyContent: 'center' }}>
            <Icon name="layers" size={12} /> Manage categories
          </button>
        </div>

        {/* Main */}
        <div style={{ padding: '22px 28px', overflow: 'auto', display: 'flex', flexDirection: 'column', gap: 18 }}>
          <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', gap: 16 }}>
            <div>
              <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Products</div>
              <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>The retail catalog — products, variants, pricing and build-your-own boxes. Orders snapshot what's bought from here.</div>
            </div>
            <div style={{ display: 'flex', gap: 8 }}>
              <div style={{ position: 'relative' }}>
                <input placeholder="Search products" style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 6, padding: '7px 10px 7px 28px', fontSize: 12.5, color: 'var(--text-primary)', width: 200, fontFamily: 'var(--font-sans)' }} />
                <span style={{ position: 'absolute', left: 9, top: 8 }}><Icon name="search" size={13} color="var(--text-tertiary)" /></span>
              </div>
              <button className="btn btn-primary btn-sm"><Icon name="plus" size={12} /> Add product</button>
            </div>
          </div>

          {/* KPIs */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
            {kpis.map(k => (
              <div key={k.l} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
                <div style={{ fontSize: 11, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>{k.l}</div>
                <div style={{ fontSize: 22, fontWeight: 700, color: k.warn && k.v > 0 ? 'var(--warning)' : 'var(--text-primary)', marginTop: 4, fontFamily: 'var(--font-brand)' }}>{k.v}</div>
                <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{k.s}</div>
              </div>
            ))}
          </div>

          {/* Kind filter + view toggle */}
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <div style={{ display: 'inline-flex', padding: 4, gap: 2, background: 'var(--surface-inset)', borderRadius: 10 }}>
              {[{ id: 'all', label: 'All kinds' }, { id: 'simple', label: 'Simple' }, { id: 'variant', label: 'Variant' }, { id: 'bundle', label: 'Bundle' }].map(k => {
                const on = kind === k.id;
                return (
                  <button key={k.id} onClick={() => setKind(k.id)} style={{
                    display: 'inline-flex', alignItems: 'center', gap: 6, height: 30, padding: '0 12px', borderRadius: 8, cursor: 'pointer', border: 'none',
                    fontSize: 12, fontWeight: on ? 600 : 500, background: on ? 'var(--surface)' : 'transparent',
                    color: on ? 'var(--text-primary)' : 'var(--text-secondary)', boxShadow: on ? '0 1px 3px rgba(20,25,30,0.10)' : 'none',
                  }}>
                    {k.id !== 'all' && <span style={{ width: 7, height: 7, borderRadius: 9, background: CM_KIND[k.id].color }} />}
                    {k.label}
                  </button>
                );
              })}
            </div>
            <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
              <span style={{ fontSize: 12.5, color: 'var(--text-secondary)', marginRight: 8 }}>Showing <b style={{ color: 'var(--text-primary)' }}>{list.length}</b></span>
              {['grid', 'list'].map(v => (
                <button key={v} onClick={() => setView(v)} style={{
                  background: view === v ? 'var(--surface-inset)' : 'transparent', color: view === v ? 'var(--text-primary)' : 'var(--text-tertiary)',
                  border: '1px solid ' + (view === v ? 'var(--border-medium)' : 'var(--border-light)'), borderRadius: 6, padding: '5px 8px', cursor: 'pointer',
                  display: 'flex', alignItems: 'center', gap: 4, fontSize: 11.5, fontWeight: 500,
                }}>
                  <Icon name={v} size={12} />{v[0].toUpperCase() + v.slice(1)}
                </button>
              ))}
            </div>
          </div>

          {/* Grid */}
          {view === 'grid' && (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12 }}>
              {list.map(p => <CmProductCard key={p.id} p={p} onClick={() => setSel(p)} />)}
              <div onClick={() => setSel({ ...CM_PRODUCTS[0], _new: true })} style={{ border: '1.5px dashed var(--border-medium)', borderRadius: 10, minHeight: 190, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 6, color: 'var(--text-tertiary)', cursor: 'pointer' }}>
                <Icon name="plus" size={18} />
                <div style={{ fontSize: 12.5, fontWeight: 500 }}>New product</div>
                <div style={{ fontSize: 11 }}>Simple, Variant or Bundle</div>
              </div>
            </div>
          )}

          {/* List */}
          {view === 'list' && (
            <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 110px 90px 90px 90px 100px 30px', gap: 12, padding: '10px 14px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 10, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)' }}>
                <div>Product</div><div>Category</div><div>Kind</div><div style={{ textAlign: 'right' }}>From</div><div style={{ textAlign: 'right' }}>Available</div><div>Status</div><div></div>
              </div>
              {list.map((p, i) => {
                const avail = cmAvail(p), low = cmLow(p);
                return (
                  <div key={p.id} className="cm-row" onClick={() => setSel(p)} style={{ display: 'grid', gridTemplateColumns: '1fr 110px 90px 90px 90px 100px 30px', gap: 12, padding: '10px 14px', alignItems: 'center', borderBottom: i < list.length - 1 ? '1px solid var(--border-light)' : 'none', fontSize: 12.5, cursor: 'pointer', opacity: p.status === 'archived' ? 0.6 : 1 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                      <span style={{ width: 28, height: 28, borderRadius: 6, background: p.color + '22', display: 'grid', placeItems: 'center', fontSize: 15, flex: 'none' }}>{p.emoji}</span>
                      <div><div style={{ color: 'var(--text-primary)', fontWeight: 500 }}>{p.name}</div><div style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{p.slug}</div></div>
                    </div>
                    <div style={{ color: 'var(--text-secondary)' }}>{cmCatName(p.cat)}</div>
                    <div><CmKindChip kind={p.kind} /></div>
                    <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }}>{cmMoney(cmPriceFrom(p))}</div>
                    <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', color: p.kind === 'bundle' ? 'var(--text-tertiary)' : low ? 'var(--warning)' : 'var(--text-primary)' }}>{p.kind === 'bundle' ? '—' : avail}{low && ' ⚠'}</div>
                    <div><Pill tone={CM_STATUS[p.status].tone} dot size="sm">{CM_STATUS[p.status].label}</Pill></div>
                    <div style={{ color: 'var(--text-tertiary)' }}><Icon name="chevron" size={14} /></div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      </div>

      {sel && <CommerceProductDrawer p={sel} onClose={() => setSel(null)} />}
    </div>
  );
}

function CmKindChip({ kind }) {
  const k = CM_KIND[kind];
  return <span style={{ fontSize: 9.5, fontWeight: 700, letterSpacing: '0.05em', textTransform: 'uppercase', padding: '2px 7px', borderRadius: 4, color: k.color, background: k.color + '18', fontFamily: 'var(--font-mono)' }}>{k.label}</span>;
}

function CmProductCard({ p, onClick }) {
  const avail = cmAvail(p), low = cmLow(p), from = cmPriceFrom(p);
  return (
    <div className="cm-card" onClick={onClick} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, overflow: 'hidden', cursor: 'pointer', opacity: p.status === 'archived' ? 0.62 : 1, display: 'flex', flexDirection: 'column' }}>
      {/* media band */}
      <div style={{ height: 84, background: p.color + '1f', display: 'flex', alignItems: 'center', justifyContent: 'center', position: 'relative' }}>
        <span style={{ fontSize: 38 }}>{p.emoji}</span>
        <span style={{ position: 'absolute', top: 8, left: 8 }}><CmKindChip kind={p.kind} /></span>
        <span style={{ position: 'absolute', top: 8, right: 8 }}><Pill tone={CM_STATUS[p.status].tone} dot size="sm">{CM_STATUS[p.status].label}</Pill></span>
      </div>
      <div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 8, flex: 1 }}>
        <div>
          <div style={{ fontSize: 13.5, fontWeight: 600, color: 'var(--text-primary)' }}>{p.name}</div>
          <div style={{ fontSize: 11, color: 'var(--text-tertiary)', marginTop: 1 }}>{cmCatName(p.cat)}</div>
        </div>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginTop: 'auto' }}>
          {p.kind === 'variant' && <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>from</span>}
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: 15, fontWeight: 700, color: 'var(--text-primary)' }}>{cmMoney(from)}</span>
          {p.kind === 'variant' && <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{p.variants.length} variants</span>}
          {p.kind === 'bundle' && <span style={{ fontSize: 11, color: CM_KIND.bundle.color }}>{p.bundle.mode === 'fixed' ? 'fixed box price' : p.bundle.mode === 'sum' ? 'sum of items' : 'sum + premium'}</span>}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', borderTop: '1px dashed var(--border-light)', paddingTop: 8 }}>
          <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{p.kind === 'bundle' ? `${p.bundle.slots.length} slot${p.bundle.slots.length > 1 ? 's' : ''}` : <span style={{ fontFamily: 'var(--font-mono)' }}>{p.variants[0].sku}</span>}</span>
          {p.kind === 'bundle'
            ? <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>build-your-own</span>
            : <span style={{ fontSize: 11, fontFamily: 'var(--font-mono)', color: low ? 'var(--warning)' : 'var(--text-secondary)' }}>{avail} avail{low && ' ⚠'}</span>}
        </div>
      </div>
    </div>
  );
}

// ═══ Product editor drawer (tabbed) ══════════════════════════════════════
function CommerceProductDrawer({ p, onClose }) {
  const tabs = p.kind === 'bundle'
    ? ['Details', 'Bundle', 'Pricing', 'Media']
    : ['Details', 'Variants', 'Pricing', 'Media'];
  const [tab, setTab] = React.useState('Details');

  return (
    <>
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(20,25,30,0.28)', zIndex: 35 }} />
      <div style={{ position: 'absolute', top: 0, right: 0, bottom: 0, width: 640, background: 'var(--surface)', borderLeft: '1px solid var(--border-light)', boxShadow: '-12px 0 32px -8px rgba(0,0,0,0.18)', zIndex: 36, display: 'flex', flexDirection: 'column' }}>
        {/* header */}
        <div style={{ padding: '18px 22px 14px', borderBottom: '1px solid var(--border-light)', display: 'flex', alignItems: 'flex-start', gap: 13 }}>
          <span style={{ width: 46, height: 46, borderRadius: 10, background: p.color + '22', display: 'grid', placeItems: 'center', fontSize: 22, flex: 'none' }}>{p.emoji}</span>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <span style={{ fontSize: 16, fontWeight: 700, color: 'var(--text-primary)' }}>{p._new ? 'New product' : p.name}</span>
              <CmKindChip kind={p.kind} />
              <Pill tone={CM_STATUS[p.status].tone} dot size="sm">{CM_STATUS[p.status].label}</Pill>
            </div>
            <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', marginTop: 3, fontFamily: 'var(--font-mono)' }}>{cmCatName(p.cat)}<span style={{ marginLeft: 10, opacity: 0.7 }}>/{p.slug}</span></div>
          </div>
          <button onClick={onClose} style={{ width: 26, height: 26, borderRadius: 6, border: '1px solid var(--border-light)', background: 'var(--surface)', cursor: 'pointer', display: 'grid', placeItems: 'center' }}><Icon name="close" size={13} color="var(--text-secondary)" /></button>
        </div>
        {/* tabs */}
        <div style={{ display: 'flex', gap: 4, padding: '0 22px', borderBottom: '1px solid var(--border-light)' }}>
          {tabs.map(t => (
            <button key={t} onClick={() => setTab(t)} style={{ padding: '11px 12px', fontSize: 12.5, fontWeight: t === tab ? 600 : 500, color: t === tab ? 'var(--text-primary)' : 'var(--text-secondary)', border: 'none', background: 'transparent', cursor: 'pointer', borderBottom: '2px solid ' + (t === tab ? 'var(--brand-primary)' : 'transparent'), marginBottom: -1 }}>{t}</button>
          ))}
        </div>
        {/* body */}
        <div style={{ flex: 1, overflow: 'auto', padding: 22 }}>
          {tab === 'Details'  && <CmTabDetails p={p} />}
          {tab === 'Variants' && <CmTabVariants p={p} />}
          {tab === 'Pricing'  && <CmTabPricing p={p} />}
          {tab === 'Media'    && <CmTabMedia p={p} />}
          {tab === 'Bundle'   && <CmTabBundle p={p} />}
        </div>
        {/* footer */}
        <div style={{ flex: 'none', padding: '14px 22px', borderTop: '1px solid var(--border-light)', background: 'var(--surface-inset)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <button className="btn btn-ghost btn-sm" style={{ color: 'var(--text-tertiary)' }}>{p.status === 'archived' ? 'Restore' : 'Archive'}</button>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-outline btn-sm" onClick={onClose}>Cancel</button>
            <button className="btn btn-primary btn-sm"><Icon name="check" size={12} /> Save product</button>
          </div>
        </div>
      </div>
    </>
  );
}

function CmField({ label, children, hint }) {
  return (
    <div>
      <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 6 }}>{label}{hint && <span style={{ textTransform: 'none', letterSpacing: 0, fontWeight: 400, color: 'var(--text-tertiary)', marginLeft: 6 }}>{hint}</span>}</div>
      {children}
    </div>
  );
}
const cmInput = { width: '100%', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '8px 11px', fontSize: 13, color: 'var(--text-primary)', fontFamily: 'var(--font-sans)' };

function CmTabDetails({ p }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <CmField label="Product name"><input defaultValue={p._new ? '' : p.name} placeholder="e.g. Almond & Honey Granola" style={cmInput} /></CmField>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <CmField label="Slug" hint="URL"><input defaultValue={p._new ? '' : p.slug} style={{ ...cmInput, fontFamily: 'var(--font-mono)', fontSize: 12 }} /></CmField>
        <CmField label="Category">
          <select defaultValue={p.cat} style={cmInput}>{CM_TOP_CATS.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select>
        </CmField>
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        <CmField label="Kind">
          <div style={{ display: 'flex', gap: 6 }}>
            {Object.entries(CM_KIND).map(([k, m]) => (
              <span key={k} style={{ flex: 1, textAlign: 'center', padding: '7px 0', borderRadius: 8, fontSize: 12, fontWeight: 600, cursor: 'pointer',
                border: '1px solid ' + (p.kind === k ? m.color : 'var(--border-light)'), color: p.kind === k ? m.color : 'var(--text-secondary)', background: p.kind === k ? m.color + '14' : 'var(--surface)' }}>{m.label}</span>
            ))}
          </div>
        </CmField>
        <CmField label="Status">
          <select defaultValue={p.status} style={cmInput}>{Object.entries(CM_STATUS).map(([k, m]) => <option key={k} value={k}>{m.label}</option>)}</select>
        </CmField>
      </div>
      <CmField label="Description"><textarea rows={3} defaultValue={p._new ? '' : 'Stone-baked, lightly sweetened with raw honey and roasted almonds. Vegan-friendly, no refined sugar.'} style={{ ...cmInput, resize: 'vertical' }} /></CmField>
      <CmField label="Tags">
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', alignItems: 'center' }}>
          {(p.tags || []).map(t => <span key={t} style={{ fontSize: 11.5, padding: '3px 9px', background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 999, color: 'var(--text-secondary)' }}>{t}</span>)}
          <button style={{ fontSize: 11.5, padding: '3px 9px', borderRadius: 999, border: '1px dashed var(--border-medium)', background: 'transparent', color: 'var(--text-tertiary)', cursor: 'pointer' }}>+ tag</button>
        </div>
      </CmField>
    </div>
  );
}

function CmTabVariants({ p }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}><b style={{ color: 'var(--text-primary)' }}>{p.variants.length}</b> variant{p.variants.length > 1 ? 's' : ''} — each with its own SKU, stock & price</div>
        <button className="btn btn-outline btn-sm"><Icon name="plus" size={11} /> Add variant</button>
      </div>
      <div style={{ border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 130px 70px 80px 56px', gap: 10, padding: '8px 12px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)', fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--text-tertiary)' }}>
          <div>Option</div><div>SKU</div><div style={{ textAlign: 'right' }}>Weight</div><div style={{ textAlign: 'right' }}>On hand</div><div style={{ textAlign: 'center' }}>Active</div>
        </div>
        {p.variants.map((v, i) => (
          <div key={v.sku} style={{ display: 'grid', gridTemplateColumns: '1fr 130px 70px 80px 56px', gap: 10, padding: '10px 12px', alignItems: 'center', borderTop: i ? '1px solid var(--border-light)' : 'none', opacity: v.active ? 1 : 0.55 }}>
            <div style={{ fontSize: 12.5, fontWeight: 500, color: 'var(--text-primary)' }}>{v.opt}</div>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-secondary)' }}>{v.sku}</div>
            <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-tertiary)' }}>{v.weight}g</div>
            <div style={{ textAlign: 'right', fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-primary)' }}>{v.onHand}</div>
            <div style={{ textAlign: 'center' }}><span style={{ width: 8, height: 8, borderRadius: 9, background: v.active ? 'var(--success)' : 'var(--gray-400, #9aa3ad)', display: 'inline-block' }} /></div>
          </div>
        ))}
      </div>
      <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', lineHeight: 1.5 }}>The variant <span style={{ fontFamily: 'var(--font-mono)' }}>SKU</span> and id are snapshotted onto the order line at checkout — the order keeps an immutable record of exactly what was bought.</div>
    </div>
  );
}

function CmTabPricing({ p }) {
  if (p.kind === 'bundle') {
    const b = p.bundle;
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
        <div style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}>Bundle price is computed by its pricing mode (set in the <b>Bundle</b> tab). Component prices come from each component's own variant price.</div>
        <div style={{ padding: '14px 16px', borderRadius: 10, background: CM_KIND.bundle.color + '12', border: '1px solid var(--border-light)' }}>
          <div style={{ fontSize: 11, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>Pricing mode</div>
          <div style={{ fontSize: 16, fontWeight: 700, color: 'var(--text-primary)', marginTop: 4 }}>{b.mode === 'fixed' ? 'Fixed box price' : b.mode === 'sum' ? 'Sum of components' : 'Sum + premium'}</div>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: 13, color: 'var(--text-secondary)', marginTop: 4 }}>
            {b.mode === 'fixed' && <>box = <b style={{ color: 'var(--text-primary)' }}>{cmMoney(b.fixed, b.ccy)}</b></>}
            {b.mode === 'sum' && 'box = Σ chosen component prices'}
            {b.mode === 'sumplus' && <>box = Σ components + <b style={{ color: 'var(--text-primary)' }}>{cmMoney(b.premium, b.ccy)}</b> premium</>}
          </div>
        </div>
      </div>
    );
  }
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
      <div style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}>Per-variant, per-currency price. Time-box with effective dates; the active price is snapshotted at cart-add and checkout.</div>
      {p.variants.map(v => (
        <div key={v.sku} style={{ border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '9px 12px', background: 'var(--surface-inset)', borderBottom: '1px solid var(--border-light)' }}>
            <span style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{v.opt}</span>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)' }}>{v.sku}</span>
          </div>
          {[{ ccy: 'NGN', amt: v.ngn }, { ccy: 'GBP', amt: v.gbp }].map((row, i) => (
            <div key={row.ccy} style={{ display: 'grid', gridTemplateColumns: '60px 1fr 1fr 56px', gap: 10, padding: '9px 12px', alignItems: 'center', borderTop: i ? '1px solid var(--border-light)' : 'none' }}>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12, fontWeight: 600, color: 'var(--text-secondary)' }}>{row.ccy}</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 13.5, fontWeight: 700, color: 'var(--text-primary)' }}>{cmMoney(row.amt, row.ccy)}</span>
              <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>from launch, no end</span>
              <span style={{ textAlign: 'center' }}><span style={{ fontSize: 9, fontWeight: 700, color: 'var(--success)', padding: '2px 6px', borderRadius: 4, background: 'var(--brand-primary-10)' }}>LIVE</span></span>
            </div>
          ))}
        </div>
      ))}
      <button className="btn btn-outline btn-sm" style={{ alignSelf: 'flex-start' }}><Icon name="plus" size={11} /> Add currency / price window</button>
    </div>
  );
}

function CmTabMedia({ p }) {
  const n = p.media || 1;
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      <div style={{ fontSize: 12.5, color: 'var(--text-secondary)' }}><b style={{ color: 'var(--text-primary)' }}>{n}</b> image{n > 1 ? 's' : ''} — drag to reorder, first is the cover</div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 10 }}>
        {Array.from({ length: n }).map((_, i) => (
          <div key={i} style={{ position: 'relative', aspectRatio: '1 / 1', borderRadius: 10, background: p.color + (i === 0 ? '2c' : '18'), display: 'grid', placeItems: 'center', fontSize: 30, border: i === 0 ? '2px solid var(--brand-primary)' : '1px solid var(--border-light)' }}>
            {p.emoji}
            {i === 0 && <span style={{ position: 'absolute', bottom: 6, left: 6, fontSize: 9, fontWeight: 700, color: '#fff', background: 'var(--brand-primary)', padding: '2px 6px', borderRadius: 4 }}>COVER</span>}
          </div>
        ))}
        <div style={{ aspectRatio: '1 / 1', borderRadius: 10, border: '1.5px dashed var(--border-medium)', display: 'grid', placeItems: 'center', color: 'var(--text-tertiary)', cursor: 'pointer' }}>
          <div style={{ textAlign: 'center' }}><Icon name="upload" size={16} /><div style={{ fontSize: 10.5, marginTop: 4 }}>Upload</div></div>
        </div>
      </div>
    </div>
  );
}

// The build-your-own-box / composite-product builder (§12)
function CmTabBundle({ p }) {
  const b = p.bundle;
  const modes = [
    { id: 'fixed',   label: 'Fixed box price', desc: 'One price for the whole box' },
    { id: 'sum',     label: 'Sum of components', desc: 'Add up the chosen items' },
    { id: 'sumplus', label: 'Sum + premium',     desc: 'Components plus a box premium' },
  ];
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>A <b style={{ color: CM_KIND.bundle.color }}>bundle</b> lets a customer build a box from component products. It holds no stock of its own — reservation and fulfilment fan out to the chosen components.</div>

      {/* Pricing mode */}
      <CmField label="Pricing mode">
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 8 }}>
          {modes.map(m => {
            const on = b.mode === m.id;
            return (
              <div key={m.id} style={{ padding: '10px 12px', borderRadius: 10, cursor: 'pointer', border: '1px solid ' + (on ? CM_KIND.bundle.color : 'var(--border-light)'), background: on ? CM_KIND.bundle.color + '12' : 'var(--surface)' }}>
                <div style={{ fontSize: 12, fontWeight: 600, color: on ? CM_KIND.bundle.color : 'var(--text-primary)' }}>{m.label}</div>
                <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 2, lineHeight: 1.4 }}>{m.desc}</div>
              </div>
            );
          })}
        </div>
      </CmField>

      {(b.mode === 'fixed' || b.mode === 'sumplus') && (
        <CmField label={b.mode === 'fixed' ? 'Box price' : 'Box premium'}>
          <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <span style={{ fontFamily: 'var(--font-mono)', fontWeight: 700, fontSize: 15, color: 'var(--text-tertiary)' }}>₦</span>
            <input defaultValue={(b.mode === 'fixed' ? b.fixed : b.premium).toLocaleString('en-NG')} style={{ ...cmInput, width: 160, fontFamily: 'var(--font-mono)', fontWeight: 600 }} />
          </div>
        </CmField>
      )}

      {/* Selection slots */}
      <CmField label="Selection slots" hint="what the buyer picks">
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          {b.slots.map((s, i) => (
            <div key={i} style={{ border: '1px solid var(--border-light)', borderRadius: 10, padding: 14, background: 'var(--surface)' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 10 }}>
                <span style={{ width: 22, height: 22, borderRadius: 6, background: CM_KIND.bundle.color + '18', color: CM_KIND.bundle.color, display: 'grid', placeItems: 'center', fontSize: 11, fontWeight: 700, flex: 'none' }}>{i + 1}</span>
                <input defaultValue={s.name} style={{ ...cmInput, fontWeight: 600 }} />
                <button style={{ border: 'none', background: 'transparent', cursor: 'pointer', color: 'var(--text-tertiary)' }}><Icon name="trash" size={14} /></button>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 10 }}>
                <CmField label="Min"><input defaultValue={s.min} style={{ ...cmInput, fontFamily: 'var(--font-mono)' }} /></CmField>
                <CmField label="Max"><input defaultValue={s.max} style={{ ...cmInput, fontFamily: 'var(--font-mono)' }} /></CmField>
                <CmField label="Allow dupes"><select defaultValue={s.allowDup ? 'yes' : 'no'} style={cmInput}><option value="yes">Yes</option><option value="no">No</option></select></CmField>
              </div>
              <div style={{ marginTop: 10 }}>
                <CmField label="Choose from" hint="category or option list">
                  <select defaultValue={s.from} style={cmInput}>{CM_TOP_CATS.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select>
                </CmField>
              </div>
              <div style={{ marginTop: 10, fontSize: 11.5, color: 'var(--text-secondary)', padding: '8px 10px', borderRadius: 8, background: 'var(--surface-inset)' }}>
                Buyer picks <b style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-primary)' }}>{s.min === s.max ? s.max : `${s.min}–${s.max}`}</b> from <b style={{ color: 'var(--text-primary)' }}>{cmCatName(s.from)}</b>
              </div>
            </div>
          ))}
          <button className="btn btn-outline btn-sm" style={{ alignSelf: 'flex-start' }}><Icon name="plus" size={11} /> Add slot</button>
        </div>
      </CmField>

      {/* Live preview */}
      <div style={{ padding: '12px 14px', borderRadius: 10, background: CM_KIND.bundle.color + '10', borderLeft: '3px solid ' + CM_KIND.bundle.color }}>
        <div style={{ fontSize: 10.5, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.05em', color: CM_KIND.bundle.color, marginBottom: 4 }}>Customer sees</div>
        <div style={{ fontSize: 12.5, color: 'var(--text-primary)', lineHeight: 1.5 }}>
          {b.slots.map((s, i) => <span key={i}>{i > 0 && ' + '}<b>{s.min === s.max ? s.max : `${s.min}–${s.max}`}</b> from {cmCatName(s.from)}</span>)}
          {' — '}
          {b.mode === 'fixed' ? <b style={{ fontFamily: 'var(--font-mono)' }}>{cmMoney(b.fixed)}</b> : b.mode === 'sum' ? 'pay the sum' : <>sum + <b style={{ fontFamily: 'var(--font-mono)' }}>{cmMoney(b.premium)}</b></>}
        </div>
      </div>
    </div>
  );
}

// ═══ Categories tree screen ══════════════════════════════════════════════
function ScreenCommerceCategories() {
  return (
    <div style={{ height: '100%', overflow: 'auto', background: 'var(--surface-canvas, #f7f8fa)' }}>
      <div style={{ maxWidth: 880, margin: '0 auto', padding: '26px 32px 48px' }}>
        <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', marginBottom: 20 }}>
          <div>
            <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Categories</div>
            <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>How products are grouped in the catalog and storefront. Parent → child; bundles can draw their slots from any category.</div>
          </div>
          <button className="btn btn-primary btn-sm"><Icon name="plus" size={12} /> Add category</button>
        </div>

        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, overflow: 'hidden' }}>
          {CM_TOP_CATS.map((parent, pi) => {
            const children = CM_CATEGORIES.filter(c => c.parent === parent.id);
            return (
              <div key={parent.id} style={{ borderTop: pi ? '1px solid var(--border-light)' : 'none' }}>
                <div style={{ display: 'grid', gridTemplateColumns: '24px 1fr 90px 110px 30px', gap: 12, alignItems: 'center', padding: '13px 16px' }}>
                  <Icon name="chevdown" size={14} color="var(--text-tertiary)" />
                  <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
                    <Icon name={parent.bundle ? 'package' : 'folder'} size={15} color={parent.bundle ? CM_KIND.bundle.color : 'var(--brand-primary)'} />
                    <span style={{ fontSize: 13.5, fontWeight: 600, color: 'var(--text-primary)' }}>{parent.name}</span>
                    {parent.bundle && <span style={{ fontSize: 9, fontWeight: 700, color: CM_KIND.bundle.color, padding: '1px 6px', borderRadius: 4, background: CM_KIND.bundle.color + '18', fontFamily: 'var(--font-mono)' }}>BUNDLES</span>}
                  </div>
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-secondary)', textAlign: 'right' }}>{CM_PRODUCTS.filter(p => p.cat === parent.id).length} products</span>
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)' }}>/{parent.slug}</span>
                  <Icon name="more" size={14} color="var(--text-tertiary)" />
                </div>
                {children.map(ch => (
                  <div key={ch.id} style={{ display: 'grid', gridTemplateColumns: '24px 1fr 90px 110px 30px', gap: 12, alignItems: 'center', padding: '11px 16px', background: 'var(--surface-inset)' }}>
                    <span />
                    <div style={{ display: 'flex', alignItems: 'center', gap: 9, paddingLeft: 18 }}>
                      <span style={{ width: 12, height: 1, background: 'var(--border-medium)' }} />
                      <Icon name="folder" size={13} color="var(--text-tertiary)" />
                      <span style={{ fontSize: 12.5, color: 'var(--text-primary)' }}>{ch.name}</span>
                    </div>
                    <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-secondary)', textAlign: 'right' }}>{ch.count} products</span>
                    <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)' }}>/{ch.slug}</span>
                    <Icon name="more" size={14} color="var(--text-tertiary)" />
                  </div>
                ))}
              </div>
            );
          })}
        </div>
        <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', marginTop: 12, display: 'flex', alignItems: 'center', gap: 6 }}>
          <Icon name="package" size={12} color={CM_KIND.bundle.color} /> A bundle slot can source its options from any category — e.g. "pick 6 from Granola".
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { ScreenCommerceProducts, ScreenCommerceCategories });
