// Aonik menu icon source. Original 24px vectors, 1.75px strokes, rounded caps/joins.
// Shared by the native showcase, the React design canvas and the SVG asset generator.
(() => {
  const paths = {
    "family": '<circle cx="8" cy="6" r="3"/><circle cx="17" cy="8" r="2"/><path d="M2 21v-5a6 6 0 0 1 12 0v5m2-7a5 5 0 0 1 6 5v2"/>',
    "children": '<circle cx="12" cy="8" r="4"/><path d="M5 21v-2a7 7 0 0 1 14 0v2M9 7h.01M15 7h.01m-5 3c1 1 3 1 4 0M12 4l2-2"/>',
    "worlds": '<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3c-5 5-5 13 0 18 5-5 5-13 0-18M5 7h14M5 17h14"/>',
    "editions": '<path d="M3 4h7l2 2 2-2h7v16h-7l-2 2-2-2H3zM12 6v16M6 9h3m-3 4h3m6-4h3m-3 4h3"/>',
    "auto-story": '<path d="M4 5h6l2 2 2-2h6v15h-6l-2 2-2-2H4zM12 7v15M18 2v5m-2.5-2.5h5"/>',
    "credits": '<ellipse cx="10" cy="6" rx="7" ry="3"/><path d="M3 6v5c0 2 3 3 7 3m7-8v4M3 11v5c0 2 3 3 7 3"/><circle cx="17" cy="17" r="5"/><path d="M17 14v6m-2-3h4"/>',
    "print": '<path d="M6 8V3h12v5M6 17H3V8h18v9h-3M6 13h12v9H6zM17 11h.01M9 17h6m-6 3h4"/>',
    "submissions": '<path d="M5 3h10l4 4v14H5zM14 3v5h5M12 17V10m-3 3 3-3 3 3"/>',
    "catalogue": '<rect x="3" y="3" width="7" height="18" rx="1"/><rect x="14" y="3" width="7" height="18" rx="1"/><path d="M6 7h1m-1 4h1m10-4h1m-1 4h1M3 17h7m4 0h7"/>',
    "release": '<path d="M5 21V3m0 1h14l-3 5 3 5H5"/><path d="m10 8 2 2 3-4"/>',
    "community": '<circle cx="7" cy="7" r="3"/><path d="M1 21v-3a6 6 0 0 1 10-4M14 3h9v9h-5l-4 3V3zM17 6h3m-3 3h3M15 21h6"/>',

    support: '<path d="M4 13v-3a8 8 0 0 1 16 0v3M4 12H2v7h4v-7zM20 12h2v7h-4v-7zM20 19a3 3 0 0 1-3 3h-4"/>',
    email: '<rect x="3" y="5" width="18" height="14" rx="2"/><path d="m3 7 9 6 9-6"/>',
    inbox: '<path d="m5 4-3 10v6h20v-6L19 4zM2 14h6l2 3h4l2-3h6"/>',
    knowledge: '<path d="M12 5C9 3 5 3 2 4v15c3-1 7-1 10 1 3-2 7-2 10-1V4c-3-1-7-1-10 1zM12 5v15M5 8h3m-3 4h3m8-4h3m-3 4h3"/>',
    search: '<circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 5 5"/>',
    home: '<path d="m3 10 9-7 9 7M5 9v12h5v-7h4v7h5V9"/>',
    workspace: '<rect x="3" y="3" width="18" height="18" rx="2"/><path d="M3 8h18M9 8v13"/>',
    overview: '<rect x="3" y="3" width="7" height="10" rx="1.5"/><rect x="14" y="3" width="7" height="6" rx="1.5"/><rect x="3" y="17" width="7" height="4" rx="1.5"/><rect x="14" y="13" width="7" height="8" rx="1.5"/>',
    orders: '<path d="M6 3h12v18l-3-2-3 2-3-2-3 2zM9 8h6M9 12h6M9 16h3"/>',
    'new-order': '<path d="M13 3H6v18h12v-7M9 8h3M9 12h3M17 3v6m-3-3h6"/>',
    'transfer-orders': '<path d="M6 3h9l3 3v4M6 3v18h4M9 7h3M9 11h3M13 14h8m-3-3 3 3-3 3M21 20h-8m3-3-3 3 3 3"/>',
    beneficiaries: '<circle cx="8" cy="7" r="3"/><path d="M2 21v-3a6 6 0 0 1 10-4M14 11h8v10h-8zM16 15h4m-4 3h2"/>',
    'mobile-money': '<rect x="5" y="2" width="12" height="20" rx="2"/><path d="M9 5h4m-2 14h.01M10 11h11m-3-3 3 3-3 3"/>',
    customers: '<circle cx="9" cy="7" r="3"/><path d="M3 21v-3a6 6 0 0 1 12 0v3m1-17a3 3 0 0 1 0 6m3 11v-3a6 6 0 0 0-2-4"/>',
    enquiries: '<rect x="3" y="4" width="18" height="14" rx="2"/><path d="m3 6 9 6 9-6M8 18l-3 3v-3"/>',
    approvals: '<rect x="5" y="5" width="14" height="16" rx="2"/><rect x="9" y="3" width="6" height="4" rx="1"/><path d="m8 14 3 3 5-6"/>',
    'bill-payments': '<rect x="3" y="5" width="18" height="14" rx="2"/><path d="M3 10h18m-8 4h5m-9 0h.01"/>',
    billers: '<path d="M4 21V5h10v16M14 9h6v12M2 21h20M8 9h2m-2 4h2m-2 4h2m9-4h.01M17 17h.01"/>',
    categories: '<path d="M3 3h8l10 10-8 8L3 11z"/><circle cx="7.5" cy="7.5" r="1"/>',
    remittances: '<circle cx="12" cy="12" r="9"/><path d="M4 9h11m-3-3 3 3-3 3m8 3H9m3-3-3 3 3 3"/>',
    corridors: '<circle cx="5" cy="5" r="2"/><circle cx="19" cy="19" r="2"/><path d="M7 5h9a4 4 0 0 1 0 8H8a3 3 0 0 0 0 6h9"/>',
    partners: '<circle cx="5" cy="5" r="2"/><circle cx="19" cy="5" r="2"/><circle cx="12" cy="19" r="2"/><path d="M7 5h10M6 7l5 10m7-10-5 10"/>',
    fx: '<path d="M3 7h17m-4-4 4 4-4 4M21 17H4m4-4-4 4 4 4M7 3v1m10 16v1"/>',
    billing: '<path d="M3 6h18v14H3zM3 10h18M7 3v5m10-5v5M7 14h4m-4 3h7"/>',
    invoices: '<path d="M6 3h9l4 4v14H6zM14 3v5h5M9 12h7m-7 4h4"/>',
    accounts: '<path d="m3 8 9-5 9 5H3zm2 4v5m5-5v5m4-5v5m5-5v5M3 21h18M5 18h14"/>',
    collections: '<circle cx="9" cy="9" r="5"/><path d="M9 6v6m-2-4h3a1 1 0 0 1 0 2H8m7-1a6 6 0 1 1-6 6m7-2v6m-3-3 3 3 3-3"/>',
    ledger: '<path d="M5 3h14v18H7a2 2 0 0 1-2-2V3zm0 14h14M9 7h6m-6 4h6"/>',
    'chart-of-accounts': '<rect x="9" y="3" width="6" height="5" rx="1"/><rect x="3" y="16" width="6" height="5" rx="1"/><rect x="15" y="16" width="6" height="5" rx="1"/><path d="M12 8v4M6 16v-4h12v4"/>',
    journal: '<path d="M5 3h14v18H5zM9 3v18m3-13h4m-4 4h4m-4 4h4M3 7h3m-3 5h3m-3 5h3"/>',
    commerce: '<path d="M3 9 5 3h14l2 6M3 9a3 3 0 0 0 6 0 3 3 0 0 0 6 0 3 3 0 0 0 6 0M5 12v9h14v-9M9 21v-6h6v6"/>',
    products: '<path d="m3 7 9-4 9 4-9 4zm0 0v10l9 4 9-4V7M12 11v10M7.5 5l9 4"/>',
    menu: '<path d="M4 3v6a3 3 0 0 0 6 0V3M7 3v18m10 0V3c-4 2-4 10 0 10h3V3h-3"/>',
    'food-orders': '<path d="M5 7h14l1 14H4zM8 7V6a4 4 0 0 1 8 0v1M8 13h8m-8 4h5"/>',
    personalisation: '<path d="M3 6h8m6 0h4M3 12h3m6 0h9M3 18h11m6 0h1"/><circle cx="14" cy="6" r="3"/><circle cx="9" cy="12" r="3"/><circle cx="17" cy="18" r="3"/>',
    content: '<path d="M5 3h10l4 4v14H5zM14 3v5h5M8 12h8m-8 4h5"/>',
    'menu-content': '<path d="M5 3h14v18H5zM8 7h8m-8 4h8m-8 4h4m-4 3h3m5-3 2 2-2 2"/>',
    'box-plans': '<rect x="3" y="6" width="18" height="15" rx="2"/><path d="M3 11h18M8 3v5m8-5v5M8 15h3v3H8zm7 0h1m-1 3h1"/>',
    delivery: '<path d="M3 6h11v11H3zM14 10h4l3 4v3h-2m-5 0h-3"/><circle cx="7" cy="18" r="2"/><circle cx="17" cy="18" r="2"/>',
    carts: '<path d="M3 3h2l3 13h11l2-9H6"/><circle cx="9" cy="20" r="1"/><circle cx="18" cy="20" r="1"/>',
    merchandising: '<path d="M3 4h18v16H3zM3 9h18M9 9v11m5-7 1 2 2 .3-1.5 1.5.4 2-1.9-1-1.9 1 .4-2L11 15.3l2-.3z"/>',
    discounts: '<path d="M4 3h16v6a3 3 0 0 0 0 6v6H4v-6a3 3 0 0 0 0-6zM9 16l6-8"/><circle cx="9" cy="9" r="1"/><circle cx="15" cy="15" r="1"/>',
    'storefront-config': '<path d="m3 8 2-5h14l2 5M3 8a3 3 0 0 0 6 0 3 3 0 0 0 6 0 3 3 0 0 0 6 0M5 12v9h14v-9M8 16h8m-5-2v4"/>',
    packing: '<path d="M6 3h12v18H6zM9 8h6m-6 4 1 1 2-2m2 1h1m-6 4 1 1 2-2m2 1h1"/>',
    inventory: '<path d="m3 8 9-5 9 5-9 5zm0 5 9 5 9-5m-18 5 9 5 9-5"/>',
    kitchen: '<path d="M6 14a4 4 0 0 1 1-8 5 5 0 0 1 10 0 4 4 0 0 1 1 8v7H6zM6 17h12m-9-6v3m6-3v3"/>',
    ingredients: '<path d="M12 21V6m0 5c-4 0-6-2-6-5 4 0 6 2 6 5zm0 6c-4 0-6-2-6-5 4 0 6 2 6 5zm0-9c4 0 6-2 6-5-4 0-6 2-6 5zm0 6c4 0 6-2 6-5-4 0-6 2-6 5z"/>',
    recipes: '<path d="M4 4h7l2 2h7v15h-7l-2-2H4zM12 7v12M7 8v6m-2-4h4m7 0h1m-1 4h1"/>',
    planning: '<rect x="3" y="5" width="18" height="16" rx="2"/><path d="M7 3v4m10-4v4M3 10h18m-14 4h3m-3 3h6"/>',
    production: '<path d="M12 3c0 4 6 6 6 11a6 6 0 0 1-12 0c0-3 2-5 3-6 0 3 2 4 2 4s3-4 1-9z"/>',
    'low-stock': '<path d="m12 3 10 18H2zM12 9v5m0 3h.01"/>',
    suppliers: '<path d="M3 21V9l6-3v5l6-3v13M15 13l6-3v11M2 21h20M6 15h1m3 0h1m-5 3h1m3 0h1m7-3h1m-1 3h1"/>',
    'purchase-orders': '<path d="M6 3h12v18H6zM9 7h6m-6 4h6m-4 4v4m-2-2h4"/>',
    margin: '<path d="M3 21h18M6 17v-5m5 5V8m5 9V4m-11 4 4-4 4 2 7-4"/>',
    wallet: '<path d="M4 7V5a2 2 0 0 1 2-2h12v4M4 7h16v14H4zM16 12h5v5h-5zM18 14.5h.01"/>',
    savings: '<path d="M4 11a7 7 0 0 1 12-5l3-2v5l2 2v5h-3l-1 5h-3l-1-3H9l-1 3H5l-1-5-2-1v-4h2zM8 7h4m4 4h.01"/>',
    transfers: '<path d="M3 7h17m-4-4 4 4-4 4M21 17H4m4-4-4 4 4 4"/>',
    compliance: '<path d="m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6zM8 12l3 3 5-6"/>',
    documents: '<path d="M8 3h8l4 4v13H8zM15 3v5h5M4 7v15h11"/>',
    team: '<circle cx="8" cy="7" r="3"/><path d="M2 21v-3a6 6 0 0 1 12 0v3m3-16v6m-3-3h6m-3 6v7m-3-4h6"/>',
    users: '<circle cx="12" cy="7" r="3"/><path d="M5 21v-3a7 7 0 0 1 14 0v3"/>',
    roles: '<rect x="3" y="5" width="18" height="15" rx="2"/><path d="M8 3v4m8-4v4M12 10l1.2 2.4 2.8.4-2 2 .5 2.7-2.5-1.3-2.5 1.3.5-2.7-2-2 2.8-.4z"/>',
    permissions: '<circle cx="8" cy="9" r="5"/><path d="m12 13 8 8m-3-3 3-3m-6 0 3-3M6 8h.01"/>',
    'deleted-users': '<circle cx="9" cy="7" r="3"/><path d="M3 21v-3a6 6 0 0 1 12 0m2-6h5m-4 0v8h3v-8m-2-3h1"/>',
    agents: '<rect x="4" y="7" width="16" height="14" rx="3"/><path d="M12 3v4M2 12h2m16 0h2M9 12h.01m6 0h.01M9 17h6"/>',
    chat: '<path d="M21 15a3 3 0 0 1-3 3H9l-6 3V6a3 3 0 0 1 3-3h12a3 3 0 0 1 3 3zM7 8h10m-10 5h6"/>',
    models: '<rect x="6" y="6" width="12" height="12" rx="2"/><rect x="10" y="10" width="4" height="4" rx=".5"/><path d="M9 3v3m6-3v3M9 18v3m6-3v3M3 9h3m-3 6h3m12-6h3m-3 6h3"/>',
    tasks: '<rect x="5" y="5" width="14" height="16" rx="2"/><rect x="9" y="3" width="6" height="4" rx="1"/><path d="M8 11h2m3 0h3m-8 5h2m3 0h3"/>',
    policies: '<path d="m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6zM9 10h6m-6 4h6"/>',
    usage: '<path d="M3 21h18M6 17v-5m6 5V7m6 10V3"/>',
    routing: '<path d="M5 3v9a5 5 0 0 0 5 5h10m-4-4 4 4-4 4M5 7h15m-4-4 4 4-4 4"/>',
    playground: '<path d="M9 3h6m-5 0v7l-5 8a2 2 0 0 0 2 3h10a2 2 0 0 0 2-3l-5-8V3M8 15h8"/>',
    runs: '<circle cx="12" cy="12" r="9"/><path d="m10 8 6 4-6 4z"/>',
    workflows: '<rect x="3" y="3" width="6" height="5" rx="1"/><rect x="15" y="3" width="6" height="5" rx="1"/><rect x="9" y="16" width="6" height="5" rx="1"/><path d="M6 8v4h12V8m-6 4v4"/>',
    extensions: '<path d="M4 4h5a3 3 0 1 1 6 0h5v5a3 3 0 1 0 0 6v5h-5a3 3 0 1 0-6 0H4v-5a3 3 0 1 0 0-6z"/>',
    'content-blocks': '<rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><path d="M14 14h7m-7 4h7m-7 3h4"/>',
    'content-wizard': '<path d="m4 20 12-12 4 4L8 24m5-13 4 4M6 3v4M4 5h4m12-3v4m-2-2h4M3 12v4m-2-2h4" transform="translate(1 -1) scale(.95)"/>',
    media: '<rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="8" cy="8" r="2"/><path d="m3 17 5-5 4 4 4-7 5 8"/>',
    settings: '<path d="m9 3-1 3-3 1-2 4 2 2v3l4 2 1 3h4l1-3 4-2v-3l2-2-2-4-3-1-1-3z"/><circle cx="12" cy="12" r="3"/>',
    authentication: '<rect x="5" y="10" width="14" height="11" rx="2"/><path d="M8 10V7a4 4 0 0 1 8 0v3M12 14v3"/>',
    communication: '<path d="m3 11 18-8-6 18-4-7zM11 14 21 3"/>',
    'email-delivered': '<path d="M3 5h18v9M3 5l9 6 9-6M3 5v14h8m3-1 3 3 5-7"/>',
    'email-bounced': '<path d="M3 5h18v8M3 5l9 6 9-6M3 5v14h7m6-4-4 4 4 4m-4-4h6a3 3 0 0 0 3-3"/>',
    gateways: '<rect x="3" y="5" width="18" height="14" rx="2"/><path d="M3 10h18m-11 5h6m-3-2 3 2-3 2"/>',
    credentials: '<rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="9" cy="10" r="3"/><path d="m11 12 5 5m-2-2 2-2"/>',
    audit: '<path d="M5 3h10l4 4v14H5zM14 3v5h5M8 12h5m-5 4h3m3 0 1 1 3-3"/>',
    speech: '<rect x="9" y="3" width="6" height="12" rx="3"/><path d="M5 10v2a7 7 0 0 0 14 0v-2M12 19v3m-4 0h8"/>',
    jobs: '<path d="M3 8h18v13H3zM8 8V4h8v4"/><circle cx="12" cy="14" r="3"/><path d="M12 12v2l1 1"/>',
    tools: '<path d="M14 3a6 6 0 0 0-7 8L3 17a3 3 0 0 0 4 4l6-6a6 6 0 0 0 8-7l-4 4-4-1-1-4z"/>',
    notifications: '<path d="M6 8a6 6 0 0 1 12 0c0 6 3 6 3 9H3c0-3 3-3 3-9m4 13h4"/>',
    modules: '<path d="m3 7 4-2 4 2-4 2zm0 0v5l4 2 4-2V7m2 0 4-2 4 2-4 2zm0 0v5l4 2 4-2V7m-13 9 4-2 4 2-4 2zm0 0v5l4 2 4-2v-5"/>',
    tenants: '<path d="M4 21V3h10v18M14 9h6v12M2 21h20M8 7h2m-2 4h2m-2 4h2m-1 6v-3m8-5h.01m0 4h.01"/>',
    observability: '<path d="M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12z"/><circle cx="12" cy="12" r="3"/>',
    topology: '<circle cx="12" cy="12" r="3"/><circle cx="4" cy="4" r="2"/><circle cx="20" cy="4" r="2"/><circle cx="4" cy="20" r="2"/><circle cx="20" cy="20" r="2"/><path d="m6 6 4 4m4 4 4 4m0-12-4 4m-4 4-4 4"/>',
    traces: '<circle cx="6" cy="4" r="2"/><circle cx="6" cy="20" r="2"/><circle cx="18" cy="16" r="2"/><path d="M6 6v12m0-9h6a6 6 0 0 1 6 5"/>',
    logs: '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="m6 8 4 4-4 4m7 0h5"/>',
    numbering: '<path d="M9 3 7 21M17 3l-2 18M3 9h18M2 15h18"/>',
    transactions: '<path d="M4 5h16v14H4zM8 9h8m-3-2 3 2-3 2m3 4H8m3-2-3 2 3 2"/>',
    connection: '<path d="M8 3v5m8-5v5M6 8h12v3a6 6 0 0 1-12 0zM12 17v4"/>'
  };
  const screenIcons = {};
  const assign = (icon, ids) => ids.split(' ').forEach(id => { screenIcons[id] = icon; });
  assign('home', 'home');
  assign('workspace', 'workspace');
  assign('chat', 'ai.chat ai.chat-agent');
  assign('models', 'ai.models');
  assign('agents', 'ai.agents ai.agent-detail');
  assign('tasks', 'ai.tasks tasks');
  assign('traces', 'ai.traces ai.trace-detail observability.traces');
  assign('routing', 'ai.routing');
  assign('playground', 'ai.playground');
  assign('enquiries', 'contact-enquiries contact-enquiry-detail');
  assign('customers', 'customers customer-detail');
  assign('compliance', 'compliance');
  assign('documents', 'documents document-new document-detail');
  assign('users', 'access.users access.user-detail');
  assign('roles', 'access.roles');
  assign('permissions', 'access.permissions');
  assign('deleted-users', 'tombstones');
  assign('tenants', 'tenants tenant-new tenant-detail');
  assign('notifications', 'alerts alert-detail settings.notification-templates');
  assign('settings', 'settings settings.general settings.global');
  assign('authentication', 'settings.authentication');
  assign('communication', 'settings.communication');
  assign('gateways', 'settings.payment-gateways');
  assign('credentials', 'settings.credential-bundles');
  assign('audit', 'settings.audit-logs observability.audit');
  assign('speech', 'settings.speech');
  assign('jobs', 'settings.background-jobs settings.background-jobs-detail');
  assign('tools', 'settings.system-tools');
  assign('modules', 'settings.modules');
  assign('content-blocks', 'cms.content-blocks cms.content-new cms.content-detail');
  assign('content-wizard', 'cms.content-wizard');
  assign('media', 'cms.media');
  assign('observability', 'observability.overview');
  assign('topology', 'observability.topology');
  assign('logs', 'observability.logs');
  assign('accounts', 'finance.accounts');
  assign('transactions', 'finance.account-transactions');
  assign('connection', 'finance.account-connection');
  assign('orders', 'finance.orders-root finance.orders finance.order-edit');
  assign('new-order', 'finance.order-new');
  assign('beneficiaries', 'finance.beneficiaries');
  assign('transfer-orders', 'finance.transfer-new finance.transfer-detail');
  assign('invoices', 'finance.invoices finance.invoice-new finance.invoice-detail');
  assign('ledger', 'finance.ledger');
  assign('chart-of-accounts', 'finance.ledger-accounts');
  assign('journal', 'finance.journal-entries');
  assign('products', 'finance.catalog commerce.products commerce.product-detail');
  assign('corridors', 'finance.countries');
  assign('categories', 'finance.categories');
  assign('billers', 'finance.billers finance.biller-detail');
  assign('bill-payments', 'finance.biller-services finance.biller-service');
  assign('partners', 'finance.partners finance.partner-detail');
  assign('numbering', 'settings.autonumbering');
  assign('fx', 'settings.fx-rates');
  assign('overview', 'commerce.overview');
  assign('discounts', 'commerce.discounts');
  assign('personalisation', 'commerce.personalisation');
  assign('content', 'commerce.content');
  assign('box-plans', 'commerce.box-plans');
  assign('delivery', 'commerce.delivery');
  assign('merchandising', 'commerce.merchandising');
  assign('storefront-config', 'commerce.storefront-config');
  assign('orders', 'commerce.orders commerce.order-detail');
  assign('packing', 'commerce.order-packing');
  assign('carts', 'commerce.carts');
  assign('approvals', 'approvals');
  assign('runs', 'ai.runs');
  assign('policies', 'ai.policies');
  assign('usage', 'ai.usage');
  assign('workflows', 'ai.workflows ai.workflow-detail');
  assign('extensions', 'ai.extensions');

  assign('overview','arke.overview');
  assign('family','arke.families');
  assign('children','arke.children');
  assign('policies','arke.consent');
  assign('worlds','arke.worlds');
  assign('editions','arke.editions');
  assign('jobs','arke.generation');
  assign('auto-story','arke.auto-stories');
  assign('compliance','arke.safety');
  assign('billing','arke.subscriptions');
  assign('categories','arke.plans');
  assign('credits','arke.credits');
  assign('print','arke.print');
  assign('submissions','arke.submissions arke.submission-detail');
  assign('catalogue','arke.catalogue');
  assign('release','arke.releases');
  assign('community','arke.community');
  assign('support','arke.support');
  assign('usage','arke.reports');
  const groups = { 'arke-people':'family', 'arke-worlds':'worlds', 'arke-production':'jobs', 'arke-billing':'billing', 'arke-orders':'print', 'arke-publishing':'catalogue', 'arke-service':'support',  'bill-payments':'bill-payments', remittances:'remittances', billing:'billing', commerce:'commerce', team:'team', content:'content-blocks', 'ai-agents':'agents', observability:'observability', 'host-settings':'settings' };
  const businessOverrides = {
    'food-commerce': { 'commerce.products':'menu', 'commerce.product-detail':'menu', 'commerce.content':'menu-content', 'commerce.orders':'food-orders', 'commerce.order-detail':'food-orders' },
    remittance: { 'finance.orders':'transfer-orders', 'finance.orders-root':'transfer-orders', 'finance.order-edit':'transfer-orders' }
  };
  const legacy = {
    myspace:'home', orders:'orders', 'orders-list':'orders', 'orders-new':'new-order', 'order-items':'transactions', customers:'customers', approvals:'approvals',
    'bill-payments':'bill-payments', billers:'billers', 'biller-categories':'categories', 'bill-history':'transactions', remittances:'remittances', corridors:'corridors', network:'partners', pricing:'fx', 'remit-history':'transactions',
    billing:'billing', invoices:'invoices', accounts:'accounts', collections:'collections', ledger:'ledger', ledgers:'ledger', 'accounts-chart':'chart-of-accounts', journal:'journal',
    commerce:'commerce', 'cm-overview':'overview', 'cm-catalog':'products', 'cm-inventory':'inventory', 'cm-orders':'orders', 'cm-discounts':'discounts',
    'cm-make':'kitchen', 'cm-ingredients':'ingredients', 'cm-recipes':'recipes', 'cm-planning':'planning', 'cm-production':'production', 'cm-lowstock':'low-stock', 'cm-suppliers':'suppliers', 'cm-pos':'purchase-orders', 'cm-margin':'margin',
    'cm-storefront':'commerce', 'cs-options':'personalisation', 'cs-content':'content', 'cs-boxplan':'box-plans', 'cs-delivery':'delivery', 'cs-merch':'merchandising', 'cs-config':'storefront-config',
    'personal-finance':'wallet', wallets:'wallet', savings:'savings', transfers:'transfers', compliance:'compliance', ai:'agents', playground:'playground', agents:'agents', aitasks:'tasks', policies:'policies', usage:'usage',
    obs:'observability', 'obs-overview':'observability', 'obs-traces':'traces', 'obs-logs':'logs', 'obs-audit':'audit', settings:'settings', tenants:'tenants', system:'tools',
    dashboard:'overview', bank:'accounts', payouts:'transfers', reports:'usage', team:'team'
  };
  const requireIcon = name => {
    if (!Object.hasOwn(paths, name)) throw new Error(`Missing menu icon: ${name}`);
    return name;
  };
  Object.values(screenIcons).forEach(requireIcon);
  Object.values(groups).forEach(requireIcon);
  Object.values(legacy).forEach(requireIcon);
  Object.values(businessOverrides).forEach(map => { Object.values(map).forEach(requireIcon); Object.freeze(map); });
  globalThis.AonikMenuIcons = Object.freeze({
    paths:Object.freeze(paths), screenIcons:Object.freeze(screenIcons), groups:Object.freeze(groups),
    businessOverrides:Object.freeze(businessOverrides), legacy:Object.freeze(legacy),
    screen: (id, business) => requireIcon(businessOverrides[business]?.[id] ?? screenIcons[id]),
    group: id => requireIcon(groups[id]),
    legacyItem: id => `menu-${requireIcon(legacy[id])}`
  });
})();
