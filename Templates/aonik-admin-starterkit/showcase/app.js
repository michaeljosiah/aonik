import { businesses, detailTables } from './fixtures.js';

const root = document.querySelector('#showcase');
const params = new URLSearchParams(location.search);
const escape = value => String(value).replace(/[&<>"']/g, c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;' })[c]);
const paths = {
  grid: '<rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/>',
  users: '<circle cx="9" cy="8" r="3"/><path d="M3 21v-2a6 6 0 0 1 12 0v2m1-17a3 3 0 0 1 0 6m2 5a5 5 0 0 1 3 4v2"/>',
  book: '<path d="M4 4h14a2 2 0 0 1 2 2v15H6a2 2 0 0 1-2-2zm0 12h16M8 8h8m-8 4h5"/>',
  receipt: '<path d="M5 3l3 2 4-2 4 2 3-2v18l-3-2-4 2-4-2-3 2zM9 9h6m-6 4h6"/>',
  check: '<path d="M9 12l2 2 4-4m-3-7 8 3v6c0 5-8 9-8 9s-8-4-8-9V6z"/>',
  globe: '<circle cx="12" cy="12" r="9"/><ellipse cx="12" cy="12" rx="4" ry="9"/><path d="M3 12h18"/>',
  spark: '<path d="m12 3 2.5 6.5L21 12l-6.5 2.5L12 21l-2.5-6.5L3 12l6.5-2.5z"/>',
  chevron: '<path d="m9 5 7 7-7 7"/>', down: '<path d="m7 10 5 5 5-5"/>',
  search: '<circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 5 5"/>',
  bell: '<path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9m-11 13h4"/>',
  panel: '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M9 4v16"/>',
  calendar: '<rect x="3" y="5" width="18" height="16" rx="2"/><path d="M7 3v4m10-4v4M3 11h18"/>',
  arrow: '<path d="M5 12h14m-5-5 5 5-5 5"/>',
  download: '<path d="M12 3v12m-5-5 5 5 5-5M4 17v4h16v-4"/>',
  filter: '<path d="M4 6h16M7 12h10m-7 6h4"/>',
  settings: '<circle cx="12" cy="12" r="3"/><path d="m10 3-1 3-3 1-3 3 2 3v4l4 1 3 3 3-2 4-1 1-4 1-4-3-2-2-3z"/>',
  close: '<path d="m6 6 12 12M6 18 18 6"/>',
  sun: '<circle cx="12" cy="12" r="4"/><path d="M12 2v2m0 16v2M2 12h2m16 0h2M5 5l1 1m12 12 1 1M5 19l1-1M18 6l1-1"/>',
  trend: '<path d="m3 17 6-6 4 4 8-10m-6 0h6v6"/>',
  info: '<circle cx="12" cy="12" r="9"/><path d="M12 11v6m0-10h.01"/>',
  ...globalThis.AonikMenuIcons.paths
};
const icon = (name, size = 16) => {
  if (!Object.hasOwn(paths, name)) throw new Error(`Missing icon: ${name}`);
  return `<svg data-icon="${escape(name)}" width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths[name]}</svg>`;
};
const screenIcon = id => globalThis.AonikMenuIcons.screen(id, params.get('business'));
const metricIcons = { 'Order value · GBP':'orders', 'Paid invoices · GBP':'invoices', 'Active customers':'customers', 'Awaiting approval':'approvals', 'AI runs this month':'runs', 'Tokens processed':'models', 'Estimated AI cost · GBP':'wallet', 'Orders this month':'food-orders', 'Awaiting payment':'bill-payments', 'Open carts':'carts', 'Registered customers':'customers', 'Active this month':'users', 'Funded order value · GBP':'wallet', 'Transfer orders':'transfer-orders', 'Completed orders':'approvals' };
const badge = value => `<span class="status ${/Completed|Paid|Active|Published|Reviewed/.test(value) ? 'positive' : /approval|payment|Overdue|Review required/.test(value) ? 'caution' : ''}"><span class="status-dot"></span>${escape(value)}</span>`;
let source, business, profile, visible, nav, scene, table, collapsed = false;
let query = '', filter = 'all';
let theme = params.get('theme') === 'dark' ? 'dark' : 'light';
const isCapture = params.get('capture') === '1';

function enabledModuleClosure(declared) {
  const enabled = new Set();
  const visit = id => {
    if (enabled.has(id)) return;
    const module = source.moduleCatalog.find(module => module.id === id);
    if (!module) throw new Error(`Unknown module: ${id}`);
    enabled.add(id);
    module.dependsOn.forEach(visit);
  };
  source.moduleCatalog.filter(module => module.isCore).forEach(module => visit(module.id));
  declared.forEach(visit);
  return [...enabled];
}

function url(type, screen, capture = isCapture) {
  const values = new URLSearchParams({ business: type, theme });
  if (screen) values.set('screen', screen);
  if (capture) values.set('capture', '1');
  return `showcase.html?${values}`;
}

function resolveProfile() {
  const policies = new Set(['AdminUserPolicy','AdminPolicy','AdminReadPolicy','AdminWritePolicy','AdminUserWritePolicy']);
  // This demo assumes a fully permitted tenant administrator, never the host audience.
  visible = new Map(Object.entries(profile.screens).flatMap(([id, presentation]) => {
    const s = source.screens[id];
    if (!s) throw new Error(`Unregistered screen: ${id}`);
    if (s.audience === 'host' || (s.policy && !policies.has(s.policy)) || !s.requires.every(id => business.modules.includes(id))) return [];
    return [[id, { ...s, label: presentation.label || s.label }]];
  }));
  nav = profile.navigation.flatMap(section => {
    const items = section.items.flatMap(item => {
      if (item.screen) return visible.has(item.screen) ? [{ ...visible.get(item.screen) }] : [];
      const children = item.items.flatMap(child => visible.has(child.screen) ? [{ ...visible.get(child.screen) }] : []);
      return children.length ? [{ ...item, children }] : [];
    });
    return items.length ? [{ ...section, items }] : [];
  });
  if (!visible.has(scene)) scene = visible.has(business.scene) ? business.scene : visible.keys().next().value;
}

function navigation() {
  const approvalCount = business.metrics.find(metric => metric[0] === 'Awaiting approval')?.[1];
  const button = item => `<button class="nav-item ${item.id === scene ? 'selected' : ''}" data-screen="${escape(item.id)}" ${item.id === scene ? 'aria-current="page"' : ''} title="${escape(item.label)}">${icon(screenIcon(item.id))}<span>${escape(item.label)}</span>${item.id === 'approvals' && approvalCount ? '<span class="nav-count">'+escape(approvalCount)+'</span>' : ''}</button>`;
  return nav.map(section => `<section class="nav-group"><div class="nav-label">${escape(section.label)}</div>${section.items.map(item => item.children ? `<details ${item.children.some(child => child.id === scene) ? 'open' : ''}><summary title="${escape(item.label)}">${icon(globalThis.AonikMenuIcons.group(item.id))}<span>${escape(item.label)}</span>${icon('chevron',12)}</summary><div class="subnav">${item.children.map(button).join('')}</div></details>` : button(item)).join('')}</section>`).join('');
}

function chart() {
  const c = business.chart;
  const points = c.series.map((n, i) => `${52 + i * 22.2},${188 - n / Math.max(...c.series) * 146}`).join(' ');
  return `<section class="panel chart-panel"><div class="panel-head"><div><h2>${escape(c.title)}</h2><p>${escape(c.note)}</p></div><span class="legend"><i></i>${scene.startsWith('ai.') ? 'AI activity' : 'This month'}</span></div><div class="chart-value">${escape(c.value)}</div><svg class="chart" viewBox="0 0 730 232" role="img" aria-label="${escape(c.title)}: fictional daily trend for October 2026"><defs><linearGradient id="area" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="var(--primary)" stop-opacity=".18"/><stop offset="100%" stop-color="var(--primary)" stop-opacity=".01"/></linearGradient></defs>${[42,91,140,189].map((y,i) => `<line x1="48" y1="${y}" x2="716" y2="${y}" stroke="var(--border)" stroke-dasharray="3 5"/><text x="0" y="${y+4}" fill="var(--muted-foreground)" font-size="10">${['100%','75%','50%','25%'][i]}</text>`).join('')}<polygon points="52,190 ${points} 696,190" fill="url(#area)"/><polyline points="${points}" fill="none" stroke="var(--primary)" stroke-width="2.5" stroke-linejoin="round" stroke-linecap="round"/>${c.labels.map((label,i) => `<text x="${48 + i*108}" y="222" fill="var(--muted-foreground)" font-size="10">${label}</text>`).join('')}</svg><div class="chart-foot">${icon('trend',14)}<span>Fixed demo series · relative daily activity</span><span>31 days</span></div></section>`;
}

function attention() {
  return `<section class="panel attention"><div class="panel-head"><div><h2>Needs your attention</h2><p>A little context for your next decision.</p></div><span class="count">${business.attention.length}</span></div>${business.attention.map(([title, text, tone]) => `<div class="attention-row"><div class="attention-icon ${tone}">${icon(tone === 'success' ? 'check' : tone === 'warning' ? 'bell' : 'info',16)}</div><div><h3>${escape(title)}</h3><p>${escape(text)}</p></div></div>`).join('')}<button class="text-button" data-action="context">Review workspace context ${icon('arrow',14)}</button></section>`;
}

function renderTable() {
  const rows = table.rows.filter(row => row.join(' ').toLowerCase().includes(query.toLowerCase()) && (filter === 'all' || /approval|payment|Overdue|Draft|Review required/.test(row.join(' '))));
  const target = document.querySelector('#data-rows');
  if (target) target.innerHTML = rows.length ? rows.map(row => `<tr tabindex="0" data-row="${table.rows.indexOf(row)}" aria-label="View ${escape(row[0])}">${row.map((cell,i) => `<td>${i === row.length-1 ? badge(cell) : i === 0 ? `<span class="record-id">${escape(cell)}</span>` : escape(cell)}</td>`).join('')}<td class="row-arrow">${icon('chevron',13)}</td></tr>`).join('') : `<tr><td colspan="${table.columns.length+1}" class="empty">No records match your search.</td></tr>`;
  const count = document.querySelector('#row-count');
  if (count) count.textContent = `${rows.length} of ${table.rows.length} sample records`;
}

function dataPanel() {
  return `<section class="panel table-panel"><div class="panel-head"><div><h2>${escape(table.title)}</h2><p>${escape(table.note)}</p></div><button class="btn btn-outline btn-sm" data-action="export">${icon('download',14)} Export CSV</button></div><div class="table-tools"><label class="table-search">${icon('search',15)}<input aria-label="Search records" placeholder="Search ${escape(table.title.toLowerCase())}…" value="${escape(query)}" id="record-search"></label><button class="btn btn-outline btn-sm" data-action="filter" aria-pressed="${filter !== 'all'}">${icon('filter',14)} ${filter === 'all' ? 'All statuses' : 'Needs review'}</button><span id="row-count"></span></div><div class="table-scroll"><table><thead><tr>${table.columns.map(c => `<th scope="col">${escape(c)}</th>`).join('')}<th scope="col"><span class="sr-only">Details</span></th></tr></thead><tbody id="data-rows"></tbody></table></div><div class="table-foot"><span>Fictional sample data</span><span>October 2026 · GBP amounts are illustrative</span></div></section>`;
}

function sceneTable() {
  if (detailTables[scene]) return detailTables[scene];
  if (scene === business.scene || scene === 'home' || scene === 'commerce.overview' || scene === 'ai.usage') return business.table;
  if (scene === 'customers') return { ...businesses['arke-kids'].table, note: 'Fictional customer contacts for this demo workspace.' };
  if (scene === 'approvals') return { title: 'Approvals', note: 'Proposals await a human decision. This design preview does not execute actions.', columns: ['Proposal','Agent','Risk','AI run','Status'], rows: [ ['PROP-DEMO-108','Domain agent','High','RUN-DEMO-1042','Awaiting approval'], ['PROP-DEMO-107','Domain agent','Medium','RUN-DEMO-1041','Awaiting approval'], ['PROP-DEMO-106','Domain agent','High','RUN-DEMO-1040','Awaiting approval'] ] };
  return null;
}

function render() {
  document.documentElement.dataset.theme = theme;
  document.title = `Aonik · ${business.name} · ${visible.get(scene).label}`;
  const label = visible.get(scene).label;
  table = sceneTable();
  root.innerHTML = `${!isCapture ? `<header class="studio-toolbar"><a href="index.html" class="studio-brand">AONIK <span>Design studio</span></a><label>Business<select id="business-select" aria-label="Business type">${Object.entries(businesses).map(([key,b]) => `<option value="${key}" ${key===params.get('business') ? 'selected' : ''}>${escape(b.name)}</option>`).join('')}</select></label><button class="btn btn-outline btn-sm" data-action="theme">${icon('sun',14)} ${theme === 'light' ? 'Dark' : 'Light'} theme</button><a class="btn btn-primary btn-sm" href="${url(params.get('business'), scene, true)}">Screenshot view ${icon('arrow',14)}</a><span class="studio-note">${escape(business.description)}</span></header>` : ''}
  <div class="app-shell ${collapsed ? 'collapsed' : ''}"><aside class="sidebar"><div class="tenant"><div class="tenant-mark">${escape(business.initials)}<i></i></div><div class="tenant-name"><b>${escape(business.tenant)}</b><span>Business workspace</span></div><button class="icon-button" data-action="context" aria-label="Workspace context">${icon('down',14)}</button></div><button class="workspace-link" data-action="palette">${icon('panel')}<span>Workspace</span><kbd>⌘ K</kbd></button><nav aria-label="Business navigation">${navigation()}</nav><div class="sidebar-bottom"><div class="powered"><span class="mini-mark">a<i></i></span><span>Powered by <b>aonik</b></span></div><button class="user-tile" data-action="context"><span class="avatar">AM</span><span><b>Alex Morgan</b><small>Workspace administrator</small></span>${icon('down',14)}</button></div></aside>
  <main class="main"><header class="topbar"><button class="icon-button" data-action="collapse" aria-label="Toggle sidebar" aria-expanded="${!collapsed}">${icon('panel',18)}</button><span class="top-divider"></span><span class="breadcrumb">Workspace ${icon('chevron',11)} <b>${escape(label)}</b></span><div class="top-actions"><button class="search-shortcut" data-action="palette">${icon('search',15)}<span>Search anything…</span><kbd>⌘ K</kbd></button><span class="demo-badge">Demo workspace</span><button class="icon-button" data-action="context" aria-label="Workspace notifications">${icon('bell',18)}</button><button class="ask-button" data-action="agent">${icon('spark',15)} Ask Aonik</button></div></header>
  <div class="content"><div class="page-heading"><div><div class="eyebrow-line">${escape(business.name)}<span>/</span> October 2026</div><h1>${escape(label === 'My Space' ? 'Your business, at a glance.' : label)}</h1><p>${escape(business.subtitle)}</p></div><button class="btn btn-outline date-button" data-action="period">${icon('calendar',15)} 01 – 31 Oct 2026 ${icon('down',12)}</button></div>
  <section class="metrics" aria-label="Monthly summary">${business.metrics.map(([name,value,delta,note]) => `<article class="metric"><div class="metric-label">${escape(name)}${icon(metricIcons[name],14)}</div><div class="metric-value">${escape(value)}${delta ? `<span class="delta">${icon('trend',12)}${escape(delta)}</span>` : ''}</div><p>${escape(note)}</p></article>`).join('')}</section>
  <div class="overview-grid">${chart()}${attention()}</div>
  ${table ? dataPanel() : `<section class="panel unavailable"><h2>${escape(label)}</h2><p>This is an existing navigation destination. The populated marketing scenes are listed in the workspace search.</p><button class="btn btn-primary" data-screen="${business.scene}">Return to showcase</button></section>`}
  <section class="agent-strip"><div class="agent-orb">${icon('spark',22)}</div><div><div class="agent-kicker">${escape(business.agent.name)} <span>·</span> ${escape(business.agent.run)}</div><h2>${escape(business.agent.title)}</h2><p>${escape(business.agent.text)}</p></div><button class="btn btn-outline" data-action="agent">${escape(business.agent.action)} ${icon('arrow',14)}</button></section>
  <footer class="content-footer"><span><i></i> All data is fictional · Design preview</span><span>Agents propose. Systems execute.</span></footer></div></main></div><dialog id="detail-dialog" aria-labelledby="dialog-title"></dialog>`;
  renderTable();
  document.querySelector('#business-select')?.addEventListener('change', event => location.href = url(event.target.value));
  document.querySelector('#record-search')?.addEventListener('input', event => { query = event.target.value; renderTable(); });
  root.querySelectorAll('[data-screen]').forEach(button => button.addEventListener('click', () => navigate(button.dataset.screen)));
  document.querySelector('#data-rows')?.addEventListener('click', event => { const row = event.target.closest('[data-row]'); if (row) showRow(Number(row.dataset.row)); });
  document.querySelector('#data-rows')?.addEventListener('keydown', event => { if (event.key === 'Enter' && event.target.dataset.row) showRow(Number(event.target.dataset.row)); });
  root.querySelectorAll('[data-action]').forEach(button => button.addEventListener('click', () => action(button.dataset.action)));
  window.__showcaseReady = true;
}

function navigate(id) {
  if (!visible.has(id)) return;
  scene = id; query = ''; filter = 'all';
  history.replaceState(null, '', url(params.get('business'), scene));
  render();
}

function dialog(title, content) {
  const d = document.querySelector('#detail-dialog');
  d.innerHTML = `<div class="dialog-head"><h2 id="dialog-title">${escape(title)}</h2><button class="icon-button" aria-label="Close dialog">${icon('close',18)}</button></div>${content}`;
  d.querySelector('.icon-button').addEventListener('click', () => d.close());
  d.showModal();
}

function showRow(index) {
  const row = table.rows[index];
  dialog(row[0], `<p class="dialog-description">Fictional record · ${escape(business.name)}</p><dl>${table.columns.map((column,i) => `<div><dt>${escape(column)}</dt><dd>${escape(row[i])}</dd></div>`).join('')}</dl>${table.columns[0] === 'Order' ? `<div class="reference-card"><h3>Intent & execution</h3><p>Order reference: ${escape(row[0])}</p><p>Payment intent: PI-DEMO-${index+1000}</p><p>${row.at(-1) === 'Completed' ? `Ledger evidence: JE-DEMO-${index+1000} · Balanced double-entry` : 'Ledger evidence: no completion posting shown'}</p><p>Demo references illustrate separate orchestration and financial records.</p></div>` : ''}<p class="dialog-description">Preview only. This screen does not write records or move money.</p>`);
}

function action(name) {
  if (name === 'collapse') { collapsed = !collapsed; render(); }
  if (name === 'theme') { theme = theme === 'light' ? 'dark' : 'light'; history.replaceState(null,'',url(params.get('business'),scene)); render(); }
  if (name === 'filter') { filter = filter === 'all' ? 'review' : 'all'; render(); }
  if (name === 'period') dialog('Reporting period', '<p class="dialog-description">Marketing scenes use a fixed period, 01–31 October 2026, so screenshots remain reproducible. Figures and trends are illustrative.</p>');
  if (name === 'context') dialog('Workspace context', `<p class="dialog-description">${escape(business.description)}</p><dl><div><dt>Business type key</dt><dd>${escape(params.get('business'))}</dd></div><div><dt>Admin profile</dt><dd>${escape(profile.id)}</dd></div><div><dt>Enabled modules</dt><dd>${escape(business.modules.join(', '))}</dd></div><div><dt>Audience</dt><dd>Tenant administrator · full demo permissions</dd></div></dl><p class="dialog-description">Menu entries come from the checked-in admin profile and registered screen requirements.</p>`);
  if (name === 'agent') dialog(business.agent.name, `<p class="dialog-description">${escape(business.agent.text)}</p><div class="reference-card"><h3>Audit reference</h3><p>${escape(business.agent.run)}</p><p>Route: resolved by AI route policy</p><p>Status: prepared for human review</p></div><p class="dialog-description">High-risk financial actions become durable proposals. Approval and dispatcher execution occur on the server. This design has no execution controls.</p>`);
  if (name === 'palette') {
    dialog('Go to a screen', `<label class="table-search palette-search">${icon('search')}<input id="palette-query" aria-label="Find a screen" placeholder="Find a screen…"></label><div class="palette-results">${[...visible.values()].filter(s => !s.path.includes(':')).map(s => `<button class="nav-item" data-destination="${escape(s.id)}">${icon(screenIcon(s.id))}<span>${escape(s.label)}</span>${sceneTableFor(s.id) ? '<small>Populated scene</small>' : ''}</button>`).join('')}</div>`);
    document.querySelectorAll('[data-destination]').forEach(button => button.addEventListener('click', () => navigate(button.dataset.destination)));
    document.querySelector('#palette-query').addEventListener('input', e => document.querySelectorAll('[data-destination]').forEach(b => b.hidden = !b.textContent.toLowerCase().includes(e.target.value.toLowerCase())));
    document.querySelector('#palette-query').focus();
  }
  if (name === 'export' && table) {
    const rows = [table.columns,...table.rows.filter(row => row.join(' ').toLowerCase().includes(query.toLowerCase()) && (filter === 'all' || /approval|payment|Overdue|Draft|Review required/.test(row.join(' '))))];
    const csv = rows.map(row => row.map(cell => `"${cell.replace(/"/g,'""')}"`).join(',')).join('\r\n');
    const objectUrl = URL.createObjectURL(new Blob([csv], {type:'text/csv;charset=utf-8'}));
    const link = document.createElement('a'); link.href = objectUrl; link.download = `${params.get('business')}-${scene}-fictional.csv`; link.click();
    setTimeout(() => URL.revokeObjectURL(objectUrl),1000);
  }
}
function sceneTableFor(id) { return id === business.scene || detailTables[id] || ['customers','approvals','ai.usage','home','commerce.overview'].includes(id); }

document.addEventListener('keydown', e => {
  if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); if (source) action('palette'); }
  if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'b') { e.preventDefault(); if (source) action('collapse'); }
});

try {
  const response = await fetch('showcase/profile-snapshot.json');
  if (!response.ok) throw new Error('Could not load profile metadata. Serve this folder over HTTP.');
  source = await response.json();
  const key = Object.hasOwn(businesses,params.get('business')) ? params.get('business') : 'base';
  params.set('business',key); business = { ...businesses[key] };
  const declared = source.packs[key]?.modules;
  business.modules = enabledModuleClosure(declared?.length ? declared : business.modules);
  profile = source.profiles[source.businessTypes[key]];
  scene = params.get('screen') || business.scene;
  resolveProfile(); render();
} catch (error) {
  root.innerHTML = `<div class="load-error"><h1>Showcase unavailable</h1><p>${escape(error.message)}</p><p>Run <code>python -m http.server 4173 --bind 127.0.0.1</code> in the starter-kit folder, then open <code>http://127.0.0.1:4173/showcase.html</code>.</p></div>`;
  console.error(error);
}
