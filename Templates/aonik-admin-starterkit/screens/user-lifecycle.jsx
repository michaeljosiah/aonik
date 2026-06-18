// ─── User Lifecycle Closure — Spec 026 ─────────────────────────────
// Five Admin-UI surfaces that close the gap from "invite a user" to
// "delete a user" entirely in-product:
//
//   ScreenUsersLifecycle       — user list with Invited / Sessions-revoked
//                                status badges + per-row action menu
//   ScreenUserDetail           — user profile + Sessions tab + three actions
//                                (Resend invite — Revoke sessions — Delete)
//   ScreenUserInviteSent       — post-send confirmation dialog
//   ScreenUserDeleteDialog     — destructive type-email-to-confirm dialog
//   ScreenComplianceTombstones — deletion audit page for compliance review
//
// All screens follow the Aonik DNA: PageHeader — Card — Pill — DataTable
// — Icon, with DM Sans for body and JetBrains Mono for IDs, dates, JWT
// claims and ledger refs.

// ─── Shared data ──────────────────────────────────────────────────
const LIFECYCLE_USERS = [
  { id: '1', name: 'Oliver Chen',    email: 'oliver@primrose.co',    role: 'Platform Admin',     status: 'active',           tone: 'success', last: '2m ago',  mfa: true,  color: '#7b76b6', sessions: 2 },
  { id: '2', name: 'Maria Gomez',    email: 'maria@primrose.co',     role: 'Finance Manager',    status: 'active',           tone: 'success', last: '14m ago', mfa: true,  color: '#eb5c37', sessions: 3 },
  { id: '3', name: 'David Lynn',     email: 'david@primrose.co',     role: 'Analyst',            status: 'active',           tone: 'success', last: '1h ago',  mfa: true,  color: '#055a60', sessions: 1 },
  { id: '4', name: 'Kiran Desai',    email: 'kiran@primrose.co',     role: 'Operations',         status: 'sessions-revoked', tone: 'warning', last: '3h ago',  mfa: true,  color: '#3ab795', sessions: 0, revokedBy: 'Oliver Chen', revokedAt: '12 May 14:02' },
  { id: '5', name: 'Raj Patel',      email: 'raj@primrose.co',       role: 'Compliance Officer', status: 'active',           tone: 'success', last: '5h ago',  mfa: true,  color: '#0097a9', sessions: 2 },
  { id: '6', name: 'Amara Okonkwo',  email: 'amara@primrose.co',     role: 'Analyst',            status: 'invited',          tone: 'pending', last: 'never',   mfa: false, color: '#e8a838', sessions: 0, invitedAt: '12 May 09:14', expiresAt: '15 May 09:14' },
  { id: '7', name: 'Jaya Lim',       email: 'jaya@primrose.co',      role: 'Read-only',          status: 'active',           tone: 'success', last: '2d ago',  mfa: false, color: '#5facbd', sessions: 1 },
  { id: '8', name: 'Lukas Becker',   email: 'lukas@primrose.co',     role: 'Operations',         status: 'invited',          tone: 'pending', last: 'never',   mfa: false, color: '#d18f5b', sessions: 0, invitedAt: '11 May 16:20', expiresAt: '14 May 16:20' },
  { id: '9', name: 'Thandiwe Moyo',  email: 'thandiwe@primrose.co',  role: 'Read-only',          status: 'suspended',        tone: 'danger',  last: '1w ago',  mfa: true,  color: '#888',    sessions: 0 },
  { id:'10', name: 'Cara Esposito',  email: 'cara@primrose.co',      role: 'Analyst',            status: 'deactivated',      tone: 'muted',   last: '3w ago',  mfa: true,  color: '#9aa3ad', sessions: 0 },
];

// Mock active sessions for the Sessions tab
const MARIA_SESSIONS = [
  { id: 'sess-9821', device: 'monitor', label: 'Chrome 134 — macOS Sonoma',  loc: 'London, UK',   ip: '82.41.218.144', iat: '12 May 13:48 UTC', lastSeen: '14 minutes ago', current: true  },
  { id: 'sess-9803', device: 'laptop',  label: 'Safari 17 — macOS Sonoma',   loc: 'London, UK',   ip: '82.41.218.144', iat: '11 May 09:02 UTC', lastSeen: '1 day ago',      current: false },
  { id: 'sess-9774', device: 'mobile',  label: 'Aonik iOS — iPhone 15 Pro',  loc: 'Reading, UK',  ip: '91.140.62.18',  iat: '08 May 22:10 UTC', lastSeen: '4 days ago',     current: false },
];

// Mock tombstones for the compliance page
const TOMBSTONES = [
  { id: 'tomb-7b21', original: 'Henry Walsh',      emailRedacted: 'h***@primrose.co',     role: 'Analyst',           deletedBy: 'Oliver Chen',  at: '08 May 2026 — 16:42', reason: 'Employment ended — contractor offboard' },
  { id: 'tomb-7b20', original: 'Priya Shah',       emailRedacted: 'p***@primrose.co',     role: 'Operations',        deletedBy: 'Oliver Chen',  at: '02 May 2026 — 11:08', reason: 'GDPR right-to-be-forgotten request' },
  { id: 'tomb-7b1f', original: 'Marcus Reed',      emailRedacted: 'm***@primrose.co',     role: 'Read-only',         deletedBy: 'Maria Gomez',  at: '28 Apr 2026 — 09:24', reason: 'Created in error — duplicate of existing user' },
  { id: 'tomb-7b1e', original: 'Sofia Mendes',     emailRedacted: 's***@primrose.co',     role: 'Compliance Officer',deletedBy: 'Oliver Chen',  at: '14 Apr 2026 — 14:17', reason: 'Employment ended' },
  { id: 'tomb-7b1d', original: 'Daniel Brooks',    emailRedacted: 'd***@primrose.co',     role: 'Analyst',           deletedBy: 'Oliver Chen',  at: '02 Apr 2026 — 10:55', reason: 'GDPR erasure request from data subject' },
  { id: 'tomb-7b1c', original: 'Aisha Bello',      emailRedacted: 'a***@primrose.co',     role: 'Operations',        deletedBy: 'Maria Gomez',  at: '21 Mar 2026 — 17:31', reason: 'Contractor end-of-engagement' },
];

// Local filter bar — sidesteps the workers.jsx FilterBar override that
// shadows the kit's FilterBar by load order. Same DNA, different name.
function UlcFilterBar({ tabs = [], search = 'Filter…', extra }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10,
      padding: '10px 14px',
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 10,
    }}>
      <div style={{ display: 'flex', gap: 4, padding: 3, background: 'var(--surface-inset)', borderRadius: 8 }}>
        {tabs.map((t, i) => {
          const active = t.active;
          const countBg = active && t.tone === 'danger'  ? 'var(--danger-light)'
                        : active && t.tone === 'warning' ? 'var(--warning-light)'
                        : active && t.tone === 'pending' ? 'var(--pending-light)'
                        : 'var(--surface-inset)';
          const countFg = active && t.tone === 'danger'  ? 'var(--danger)'
                        : active && t.tone === 'warning' ? '#8a6d0a'
                        : active && t.tone === 'pending' ? 'var(--pending)'
                        : 'var(--text-tertiary)';
          return (
            <button key={i} style={{
              border: 'none', background: active ? 'var(--surface)' : 'transparent',
              padding: '5px 12px', borderRadius: 6, cursor: 'pointer',
              fontFamily: 'inherit', fontSize: 12, fontWeight: active ? 600 : 500,
              color: active ? 'var(--text-primary)' : 'var(--text-secondary)',
              boxShadow: active ? '0 1px 2px rgba(0,0,0,0.04)' : 'none',
              display: 'inline-flex', alignItems: 'center', gap: 6,
            }}>
              {t.label}
              {typeof t.count === 'number' && <span style={{
                fontFamily: 'var(--font-mono)', fontSize: 10, fontWeight: 600,
                padding: '1px 6px', borderRadius: 4,
                background: countBg, color: countFg,
              }}>{t.count}</span>}
            </button>
          );
        })}
      </div>
      <div style={{ width: 1, height: 20, background: 'var(--border-light)', margin: '0 4px' }}/>
      <div style={{ flex: 1, position: 'relative', minWidth: 180 }}>
        <span style={{ position: 'absolute', left: 10, top: '50%', transform: 'translateY(-50%)', color: 'var(--text-tertiary)' }}>
          <Icon name="search" size={13}/>
        </span>
        <input className="input" placeholder={search}
          style={{ paddingLeft: 30, height: 30, fontSize: 12, background: 'var(--surface-inset)', border: 'none', width: '100%' }}/>
      </div>
      {extra}
      <button className="btn btn-ghost btn-sm"><Icon name="filter" size={12}/> Filters</button>
    </div>
  );
}

// Status badge helper — maps lifecycle status to Pill props
function lifecycleStatusProps(status) {
  switch (status) {
    case 'active':           return { tone: 'success', dot: true, label: 'Active' };
    case 'invited':          return { tone: 'warning', dot: true, label: 'Invited' };
    case 'sessions-revoked': return { tone: 'pending', dot: true, label: 'Sessions revoked' };
    case 'suspended':        return { tone: 'danger',  dot: true, label: 'Suspended' };
    case 'deactivated':      return { tone: 'muted',   dot: true, label: 'Deactivated' };
    default:                 return { tone: 'muted',   dot: true, label: status };
  }
}

// ─── 1 — ScreenUsersLifecycle ──────────────────────────────────────
// Updated user list with per-row action menu. Hover any row to reveal
// the kebab; for an Invited user the "Resend invite" item is the
// primary action.
function ScreenUsersLifecycle() {
  const [hoverId, setHoverId] = React.useState(null);
  const [menuId, setMenuId] = React.useState(null);

  const cols = [
    { key: 'name', label: 'User', w: '1.6fr',
      render: r => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <Avatar name={r.name} size={30} color={r.color} textColor="#fff"/>
          <div>
            <div style={{ fontSize: 13, fontWeight: 500, color: 'var(--text-primary)' }}>{r.name}</div>
            <div style={{ fontSize: 11, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>{r.email}</div>
          </div>
        </div>
      ) },
    { key: 'role', label: 'Role', w: '1fr',
      render: r => (
        <span style={{
          fontSize: 11, fontWeight: 500, padding: '3px 8px', borderRadius: 4,
          background: 'var(--surface-inset)', color: 'var(--text-primary)',
          border: '1px solid var(--border-light)',
        }}>{r.role}</span>
      ) },
    { key: 'sessions', label: 'Sessions', w: '90px', mono: true, fontSize: 11,
      render: r => (
        <span style={{ color: r.sessions > 0 ? 'var(--text-primary)' : 'var(--text-tertiary)' }}>
          {r.sessions === 0 ? '—' : `${r.sessions} active`}
        </span>
      ) },
    { key: 'mfa', label: 'MFA', w: '60px',
      render: r => r.mfa
        ? <Icon name="shield" size={14} color="var(--success)"/>
        : <Icon name="warn"   size={14} color="var(--warning)"/> },
    { key: 'last', label: 'Last seen', w: '110px', mono: true, fontSize: 11,
      render: r => <span style={{ color: 'var(--text-secondary)' }}>{r.last}</span> },
    { key: 'status', label: 'Status', w: '150px',
      render: r => {
        const p = lifecycleStatusProps(r.status);
        return <Pill tone={p.tone} dot={p.dot}>{p.label}</Pill>;
      } },
    { key: 'actions', label: '', w: '52px',
      render: r => (
        <div style={{ position: 'relative', display: 'flex', justifyContent: 'flex-end' }}>
          <span
            className="hover-halo"
            onClick={(e) => { e.stopPropagation(); setMenuId(menuId === r.id ? null : r.id); }}
            style={{
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
              width: 28, height: 28, borderRadius: 6, cursor: 'pointer',
              opacity: hoverId === r.id || menuId === r.id ? 1 : 0.5,
            }}
          >
            <Icon name="ellipsis" size={14} color="var(--text-secondary)"/>
          </span>
          {menuId === r.id && <UserRowMenu user={r} onClose={() => setMenuId(null)}/>}
        </div>
      ) },
  ];

  // Inject hover-row behaviour by wrapping rows
  const rowsWithHover = LIFECYCLE_USERS.map(u => ({
    ...u,
    __onMouseEnter: () => setHoverId(u.id),
    __onMouseLeave: () => setHoverId(null),
  }));

  // Counts for the subtitle
  const counts = LIFECYCLE_USERS.reduce((m, u) => ({ ...m, [u.status]: (m[u.status] || 0) + 1 }), {});
  const subtitle = [
    `${LIFECYCLE_USERS.length} team members`,
    counts.active && `${counts.active} active`,
    counts.invited && `${counts.invited} pending invite`,
    counts['sessions-revoked'] && `${counts['sessions-revoked']} sessions revoked`,
    counts.suspended && `${counts.suspended} suspended`,
  ].filter(Boolean).join(' — ');

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader
        eyebrow="Finance — Access"
        title="Users"
        subtitle={subtitle}
        actions={<>
          <button className="btn btn-outline btn-sm"><Icon name="shield" size={12}/> Roles</button>
          <button className="btn btn-primary btn-sm"><Icon name="userplus" size={12}/> Invite user</button>
        </>}
      />

      {/* KPI strip — 4 even cards, lifecycle-aware */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 14 }}>
        {[
          { l: 'Active',            v: counts.active || 0,             tone: 'var(--success)' },
          { l: 'Pending invite',    v: counts.invited || 0,            tone: 'var(--warning)' },
          { l: 'Sessions revoked',  v: counts['sessions-revoked'] || 0,tone: 'var(--brand-primary)' },
          { l: 'Suspended / off',   v: (counts.suspended || 0) + (counts.deactivated || 0), tone: 'var(--danger)' },
        ].map((s, i) => (
          <div key={i} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 7, fontSize: 11, color: 'var(--text-secondary)' }}>
              <span style={{ width: 6, height: 6, borderRadius: 999, background: s.tone }}/>{s.l}
            </div>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 22, fontWeight: 600, color: 'var(--text-primary)', marginTop: 4 }}>{s.v}</div>
          </div>
        ))}
      </div>

      <UlcFilterBar
        tabs={[
          { label: 'All',               count: 10, active: true },
          { label: 'Active',            count: counts.active || 0 },
          { label: 'Invited',           count: counts.invited || 0, tone: 'warning' },
          { label: 'Sessions revoked',  count: counts['sessions-revoked'] || 0, tone: 'pending' },
          { label: 'Suspended',         count: counts.suspended || 0, tone: 'danger' },
          { label: 'Deactivated',       count: counts.deactivated || 0 },
        ]}
        search="Filter users by name, email, role…"
      />

      <UsersLifecycleTable cols={cols} rows={rowsWithHover}/>
    </div>
  );
}

// Custom table wrapper that supports per-row mouseEnter / mouseLeave
// (DataTable does not pass row hover events through).
function UsersLifecycleTable({ cols, rows }) {
  const widthExpr = cols.map(c => c.w || '1fr').join(' ');
  return (
    <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, overflow: 'visible' }}>
      <div style={{
        display: 'grid', gridTemplateColumns: widthExpr,
        padding: '11px 16px', borderBottom: '1px solid var(--border-light)',
        background: 'var(--surface-inset)',
        fontSize: 11, fontWeight: 600, letterSpacing: '0.04em',
        textTransform: 'uppercase', color: 'var(--text-tertiary)',
      }}>
        {cols.map(c => <div key={c.key}>{c.label}</div>)}
      </div>
      {rows.map((r, i) => (
        <div
          key={r.id}
          onMouseEnter={r.__onMouseEnter}
          onMouseLeave={r.__onMouseLeave}
          style={{
            display: 'grid', gridTemplateColumns: widthExpr,
            padding: '12px 16px', alignItems: 'center', gap: 0,
            borderBottom: i < rows.length - 1 ? '1px solid var(--border-light)' : 'none',
            fontSize: 13, color: 'var(--text-primary)', cursor: 'pointer',
            transition: 'background-color 80ms ease',
          }}
        >
          {cols.map(c => <div key={c.key}>{c.render ? c.render(r) : r[c.key]}</div>)}
        </div>
      ))}
      <div style={{
        padding: '11px 16px', borderTop: '1px solid var(--border-light)',
        background: 'var(--surface-inset)',
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        fontSize: 11.5, color: 'var(--text-secondary)',
      }}>
        <span>Showing 1–10 of <strong style={{ color: 'var(--text-primary)' }}>10 users</strong></span>
        <span style={{ fontFamily: 'var(--font-mono)' }}>page 1 of 1</span>
      </div>
    </div>
  );
}

// Per-row action menu. Contextual: shows "Resend invite" only when invited,
// "Revoke sessions" only when there are active sessions, "Reinstate" when
// sessions are already revoked.
function UserRowMenu({ user, onClose }) {
  const items = [
    { icon: 'eye',       label: 'View details',           tone: 'default' },
    user.status === 'invited'         && { icon: 'send',       label: 'Resend invite',          tone: 'primary', primary: true },
    user.status === 'invited'         && { icon: 'copy',       label: 'Copy invite link',       tone: 'default' },
    user.status === 'active'          && { icon: 'ban',        label: 'Revoke active sessions', tone: 'default' },
    user.status === 'sessions-revoked'&& { icon: 'recycle',    label: 'Reinstate sessions',     tone: 'default' },
    user.status === 'active'          && { icon: 'lock',       label: 'Deactivate',             tone: 'default' },
    user.status === 'deactivated'     && { icon: 'recycle',    label: 'Reactivate',             tone: 'default' },
    { kind: 'divider' },
    { icon: 'trash',     label: 'Delete user…',           tone: 'danger' },
  ].filter(Boolean);

  return (
    <>
      <div style={{ position: 'fixed', inset: 0, zIndex: 9 }} onClick={onClose}/>
      <div style={{
        position: 'absolute', top: 32, right: 0, zIndex: 10,
        width: 220, background: 'var(--surface)',
        border: '1px solid var(--border-light)', borderRadius: 10,
        boxShadow: '0 18px 40px -10px rgba(0,0,0,0.18)',
        padding: 6, overflow: 'hidden',
      }}>
        {items.map((it, i) => {
          if (it.kind === 'divider') {
            return <div key={i} style={{ height: 1, background: 'var(--border-light)', margin: '4px 6px' }}/>;
          }
          const color = it.tone === 'danger' ? 'var(--danger)' : it.tone === 'primary' ? 'var(--brand-primary)' : 'var(--text-primary)';
          return (
            <div key={i} style={{
              display: 'flex', alignItems: 'center', gap: 10,
              padding: '7px 9px', borderRadius: 6, cursor: 'pointer',
              fontSize: 12.5, color, fontWeight: it.primary ? 600 : 500,
              background: it.primary ? 'var(--brand-primary-10)' : 'transparent',
            }}>
              <Icon name={it.icon} size={13} color={color}/>
              {it.label}
            </div>
          );
        })}
      </div>
    </>
  );
}

// ─── 2 — ScreenUserDetail ──────────────────────────────────────────
// User detail page. Sessions tab is shown by default — it's the
// most novel surface introduced by spec 026.
function ScreenUserDetail() {
  const [tab, setTab] = React.useState('sessions');
  const u = LIFECYCLE_USERS.find(x => x.id === '2'); // Maria Gomez

  const TABS = [
    { id: 'profile',  label: 'Profile',  icon: 'user' },
    { id: 'roles',    label: 'Roles',    icon: 'shield' },
    { id: 'sessions', label: 'Sessions', icon: 'monitor', badge: u.sessions },
    { id: 'audit',    label: 'Audit',    icon: 'activity' },
  ];

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      {/* Header — full bleed identity block */}
      <PageHeader
        eyebrow="Finance — Access — Users"
        title={u.name}
        subtitle={<span style={{ display: 'inline-flex', alignItems: 'center', gap: 10 }}>
          <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>{u.email}</span>
          <span style={{ color: 'var(--border)' }}>—</span>
          <Pill tone="tint" size="sm">{u.role}</Pill>
          <Pill {...lifecycleStatusProps(u.status)} size="sm">{lifecycleStatusProps(u.status).label}</Pill>
          {u.mfa && <Pill tone="success" dot size="sm">MFA</Pill>}
        </span>}
        actions={<>
          <button className="btn btn-outline btn-sm"><Icon name="edit"   size={12}/> Edit profile</button>
          <button className="btn btn-outline btn-sm"><Icon name="ban"    size={12}/> Revoke sessions</button>
          <button className="btn btn-danger  btn-sm"><Icon name="trash"  size={12}/> Delete user…</button>
        </>}
      />

      {/* Identity card — avatar + key facts at a glance */}
      <div style={{
        display: 'grid', gridTemplateColumns: 'auto 1fr auto', gap: 24,
        padding: '20px 22px', background: 'var(--surface)',
        border: '1px solid var(--border-light)', borderRadius: 12,
        alignItems: 'center',
      }}>
        <Avatar name={u.name} size={72} color={u.color} textColor="#fff"/>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 18 }}>
          {[
            { l: 'User ID',     v: `usr_${u.id.padStart(6, '0')}`,                                                  mono: true },
            { l: 'IdP subject', v: 'auth0|65f2…b104',                                                               mono: true },
            { l: 'Joined',      v: '14 Jan 2025' },
            { l: 'Last sign-in',v: u.last,                                                                          mono: true },
          ].map((f, i) => (
            <div key={i}>
              <div style={{ fontSize: 10.5, letterSpacing: '0.08em', textTransform: 'uppercase', color: 'var(--text-tertiary)', fontWeight: 600, marginBottom: 4 }}>{f.l}</div>
              <div style={{ fontSize: 13, fontWeight: 500, color: 'var(--text-primary)', fontFamily: f.mono ? 'var(--font-mono)' : 'inherit' }}>{f.v}</div>
            </div>
          ))}
        </div>
      </div>

      {/* Tabs */}
      <div style={{
        display: 'flex', gap: 4, borderBottom: '1px solid var(--border-light)',
        marginBottom: -10,
      }}>
        {TABS.map(t => {
          const active = tab === t.id;
          return (
            <div key={t.id} onClick={() => setTab(t.id)} style={{
              display: 'inline-flex', alignItems: 'center', gap: 8,
              padding: '10px 14px', cursor: 'pointer',
              borderBottom: active ? '2px solid var(--brand-primary)' : '2px solid transparent',
              color: active ? 'var(--brand-primary)' : 'var(--text-secondary)',
              fontWeight: active ? 600 : 500, fontSize: 13,
              marginBottom: -1,
            }}>
              <Icon name={t.icon} size={13} color={active ? 'var(--brand-primary)' : 'var(--text-secondary)'}/>
              {t.label}
              {t.badge ? <span style={{
                fontFamily: 'var(--font-mono)', fontSize: 10, fontWeight: 700,
                padding: '0 6px', borderRadius: 999, lineHeight: '16px',
                background: active ? 'var(--brand-primary)' : 'var(--surface-inset)',
                color: active ? '#fff' : 'var(--text-tertiary)',
              }}>{t.badge}</span> : null}
            </div>
          );
        })}
      </div>

      {/* Tab body */}
      {tab === 'sessions' && <UserDetailSessions sessions={MARIA_SESSIONS}/>}
      {tab === 'profile'  && <UserDetailProfile  user={u}/>}
      {tab === 'roles'    && <UserDetailRoles/>}
      {tab === 'audit'    && <UserDetailAudit/>}
    </div>
  );
}

function UserDetailSessions({ sessions }) {
  const [showInfo, setShowInfo] = React.useState(false);

  return (
    <div style={{ marginTop: 18 }}>
      <Card
        title="Active sessions"
        subtitle="Bearer tokens currently valid — pulled from recent iat claims"
        action={
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <SessionsInfoPopover open={showInfo} onToggle={() => setShowInfo(v => !v)} onClose={() => setShowInfo(false)}/>
            <button className="btn btn-outline btn-sm"><Icon name="ban" size={12}/> Revoke all</button>
          </div>
        }
      >
        <div style={{
          display: 'flex', flexDirection: 'column', gap: 14,
          margin: '12px 6px 6px',
        }}>
          {sessions.map(s => (
            <div key={s.id} style={{
              display: 'grid', gridTemplateColumns: '44px 1fr auto', gap: 18,
              alignItems: 'center', padding: '18px 20px',
              background: s.current ? 'var(--brand-primary-10)' : 'var(--surface)',
              border: `1px solid ${s.current ? 'var(--brand-primary-30, rgba(5,90,96,0.25))' : 'var(--border-light)'}`,
              borderRadius: 12,
              boxShadow: s.current ? 'none' : '0 1px 2px rgba(0,0,0,0.02)',
            }}>
              <div style={{
                width: 44, height: 44, borderRadius: 10,
                background: s.current ? 'var(--brand-primary)' : 'var(--surface-inset)',
                color: s.current ? '#fff' : 'var(--text-secondary)',
                display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
              }}><Icon name={s.device} size={20}/></div>
              <div style={{ minWidth: 0 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10, fontSize: 13.5, fontWeight: 600, color: 'var(--text-primary)' }}>
                  <span>{s.label}</span>
                  {s.current && <span style={{
                    fontFamily: 'var(--font-mono)', fontSize: 9.5, fontWeight: 700,
                    letterSpacing: '0.06em', textTransform: 'uppercase',
                    padding: '3px 7px', borderRadius: 4,
                    background: 'var(--brand-primary)', color: '#fff',
                  }}>This session</span>}
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginTop: 7, fontSize: 11.5, color: 'var(--text-secondary)', fontFamily: 'var(--font-mono)' }}>
                  <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}>
                    <Icon name="mappin" size={10} color="var(--text-tertiary)"/>{s.loc}
                  </span>
                  <span>{s.ip}</span>
                  <span style={{ color: 'var(--text-tertiary)' }}>—</span>
                  <span>iat {s.iat}</span>
                </div>
                <div style={{ fontSize: 11, color: 'var(--text-tertiary)', marginTop: 5 }}>
                  Last seen {s.lastSeen} — token id <span style={{ fontFamily: 'var(--font-mono)' }}>{s.id}</span>
                </div>
              </div>
              <button className="btn btn-ghost btn-sm" style={{ color: 'var(--danger)' }}>
                <Icon name="ban" size={12}/> Revoke
              </button>
            </div>
          ))}
        </div>
      </Card>
    </div>
  );
}

// Click-to-open popover surfacing the Revoke playbook + cache TTL details.
// Replaces the old right-rail Card layout so the sessions list breathes
// across the full width of the tab body.
function SessionsInfoPopover({ open, onToggle, onClose }) {
  return (
    <div style={{ position: 'relative' }}>
      <button
        onClick={onToggle}
        className="hover-halo"
        title="How session revocation works"
        style={{
          width: 30, height: 30, borderRadius: 7,
          border: '1px solid var(--border-light)',
          background: open ? 'var(--brand-primary-10)' : 'var(--surface)',
          color: open ? 'var(--brand-primary)' : 'var(--text-secondary)',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          cursor: 'pointer',
          transition: 'background 120ms ease, color 120ms ease',
        }}
      >
        <Icon name="help" size={14} color="currentColor"/>
      </button>
      {open && (
        <>
          <div style={{ position: 'fixed', inset: 0, zIndex: 9 }} onClick={onClose}/>
          <div style={{
            position: 'absolute', top: 38, right: 0, zIndex: 10,
            width: 320, background: 'var(--surface)',
            border: '1px solid var(--border-light)', borderRadius: 12,
            boxShadow: '0 18px 40px -10px rgba(0,0,0,0.22), 0 2px 6px rgba(0,0,0,0.06)',
            padding: '16px 18px',
          }}>
            {/* Arrow / pointer */}
            <div style={{
              position: 'absolute', top: -7, right: 12,
              width: 12, height: 12,
              background: 'var(--surface)',
              borderLeft: '1px solid var(--border-light)',
              borderTop: '1px solid var(--border-light)',
              transform: 'rotate(45deg)',
            }}/>

            <div style={{ fontSize: 10.5, letterSpacing: '0.08em', textTransform: 'uppercase', color: 'var(--text-tertiary)', fontWeight: 600, marginBottom: 8 }}>
              Revoke playbook
            </div>
            <ol style={{ margin: '0 0 14px', paddingLeft: 18, fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.65 }}>
              <li>Row added to <code style={{ fontSize: 11 }}>AnkUserSessionBlocklist</code></li>
              <li>FusionCache invalidated across all replicas</li>
              <li>Next request returns <code style={{ fontSize: 11 }}>401</code> within ≤ 30 s</li>
              <li>Audit entry <code style={{ fontSize: 11 }}>user.sessions-revoked</code></li>
            </ol>

            <div style={{
              paddingTop: 12, borderTop: '1px solid var(--border-light)',
              fontSize: 10.5, letterSpacing: '0.08em', textTransform: 'uppercase',
              color: 'var(--text-tertiary)', fontWeight: 600, marginBottom: 8,
            }}>
              Cache TTL
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 7 }}>
              {[
                { l: 'Refresh window',     v: '30 s' },
                { l: 'Jitter',             v: '5 s' },
                { l: 'Blocklist max-age',  v: '14 days' },
                { l: 'JWT max lifetime',   v: '14 days' },
              ].map((r, i) => (
                <div key={i} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, color: 'var(--text-secondary)' }}>
                  <span>{r.l}</span>
                  <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-primary)', fontWeight: 600 }}>{r.v}</span>
                </div>
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  );
}

function UserDetailProfile({ user }) {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '1fr 320px', gap: 20, marginTop: 18 }}>
      <Card title="Profile" subtitle="Edit name, email, phone and locale">
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14, marginTop: 4 }}>
          <UlcField label="Display name"  value={user.name}/>
          <UlcField label="Email"          value={user.email} mono/>
          <UlcField label="Phone"          value="+44 7700 900218" mono/>
          <UlcField label="Locale"         value="en-GB — GMT"/>
          <UlcField label="Department"     value="Treasury"/>
          <UlcField label="Reports to"     value="Oliver Chen"/>
        </div>
      </Card>
      <Card title="Linked identities">
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 6 }}>
          <LinkedIdentityRow provider="Auth0"    sub="auth0|65f2…b104" verified/>
          <LinkedIdentityRow provider="Microsoft Entra" sub="—" verified={false}/>
          <LinkedIdentityRow provider="SAML"     sub="—" verified={false}/>
        </div>
      </Card>
    </div>
  );
}

function UlcField({ label, value, mono }) {
  return (
    <div>
      <div style={{ fontSize: 10.5, letterSpacing: '0.06em', textTransform: 'uppercase', color: 'var(--text-tertiary)', fontWeight: 600, marginBottom: 5 }}>{label}</div>
      <div style={{
        padding: '8px 11px', border: '1px solid var(--border-light)', borderRadius: 7,
        background: 'var(--surface)', fontSize: 13, color: 'var(--text-primary)',
        fontFamily: mono ? 'var(--font-mono)' : 'inherit',
      }}>{value}</div>
    </div>
  );
}

function LinkedIdentityRow({ provider, sub, verified }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10,
      padding: '10px 12px', border: '1px solid var(--border-light)', borderRadius: 8,
      background: 'var(--surface)',
    }}>
      <Icon name="key" size={14} color="var(--text-secondary)"/>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{provider}</div>
        <div style={{ fontSize: 11, fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>{sub}</div>
      </div>
      {verified
        ? <Pill tone="success" dot size="sm">linked</Pill>
        : <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>—</span>}
    </div>
  );
}

function UserDetailRoles() {
  const roles = [
    { name: 'Finance Manager',   scope: 'tenant',  perms: 42, granted: '14 Jan 2025',  by: 'Oliver Chen' },
    { name: 'Treasury Approver', scope: 'workspace', perms: 8,  granted: '02 Mar 2025', by: 'Maria Gomez' },
  ];
  return (
    <Card title="Roles & permissions" subtitle={`${roles.length} roles — 50 effective permissions`} action={<button className="btn btn-outline btn-sm"><Icon name="plus" size={12}/> Assign role</button>}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginTop: 6 }}>
        {roles.map(r => (
          <div key={r.name} style={{
            display: 'grid', gridTemplateColumns: '1fr auto auto auto',
            alignItems: 'center', gap: 16, padding: '12px 14px',
            background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 9,
          }}>
            <div>
              <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{r.name}</div>
              <div style={{ fontSize: 11, color: 'var(--text-secondary)' }}>scope — {r.scope}</div>
            </div>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11.5, color: 'var(--text-secondary)' }}>{r.perms} perms</span>
            <span style={{ fontSize: 11.5, color: 'var(--text-tertiary)' }}>by {r.by} — {r.granted}</span>
            <button className="btn btn-ghost btn-sm"><Icon name="trash" size={12}/></button>
          </div>
        ))}
      </div>
    </Card>
  );
}

function UserDetailAudit() {
  const entries = [
    { t: '12 May 13:48', a: 'user.signed-in',        d: 'IdP claim verified — iat 1715520480'  },
    { t: '11 May 09:02', a: 'user.signed-in',        d: 'Safari 17 — macOS'                    },
    { t: '08 May 22:10', a: 'user.signed-in',        d: 'Aonik iOS — iPhone 15 Pro'            },
    { t: '02 May 16:30', a: 'role.assigned',         d: 'Treasury Approver granted by O. Chen' },
    { t: '14 Apr 11:08', a: 'profile.updated',       d: 'Phone changed — +44 7700 900218'      },
    { t: '14 Jan 09:14', a: 'user.invite-accepted',  d: 'Joined via invite from O. Chen'       },
  ];
  return (
    <Card title="Audit trail" subtitle="Last 90 days — scoped to this user">
      <div style={{ display: 'flex', flexDirection: 'column', marginTop: 4 }}>
        {entries.map((e, i, arr) => (
          <div key={i} style={{
            display: 'grid', gridTemplateColumns: '120px 200px 1fr',
            alignItems: 'center', gap: 16, padding: '11px 4px',
            borderBottom: i < arr.length - 1 ? '1px solid var(--border-light)' : 'none',
            fontSize: 12.5,
          }}>
            <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>{e.t}</span>
            <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--brand-primary)', fontWeight: 600 }}>{e.a}</span>
            <span style={{ color: 'var(--text-secondary)' }}>{e.d}</span>
          </div>
        ))}
      </div>
    </Card>
  );
}

// ─── 3 — ScreenUserInviteSent ──────────────────────────────────────
// Post-send confirmation dialog. The backdrop is the user list, dimmed.
function ScreenUserInviteSent() {
  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <UsersListBackdrop/>
      <div style={{ position: 'absolute', inset: 0, background: 'rgba(15, 20, 28, 0.42)', backdropFilter: 'blur(2px)' }}/>

      {/* Dialog */}
      <div style={{
        position: 'absolute', top: '50%', left: '50%', transform: 'translate(-50%, -50%)',
        width: 480, background: 'var(--surface)',
        borderRadius: 14, boxShadow: '0 24px 60px -10px rgba(0,0,0,0.35)',
        border: '1px solid var(--border-light)',
        display: 'flex', flexDirection: 'column',
      }}>
        {/* Header */}
        <div style={{ padding: '22px 24px 0', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 14 }}>
          <div style={{
            width: 56, height: 56, borderRadius: '50%',
            background: 'rgba(106,191,110,0.14)',
            color: 'var(--success)',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          }}><Icon name="check" size={28}/></div>
          <div style={{ textAlign: 'center' }}>
            <div style={{ fontFamily: 'var(--font-brand)', fontSize: 20, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Invite sent</div>
            <div style={{ fontSize: 13.5, color: 'var(--text-secondary)', marginTop: 4 }}>
              Amara will receive an email from Aonik in the next few seconds.
            </div>
          </div>
        </div>

        {/* Body */}
        <div style={{ padding: '20px 24px', display: 'flex', flexDirection: 'column', gap: 14 }}>
          {/* Recipient card */}
          <div style={{
            padding: '14px 16px', background: 'var(--surface-inset)',
            border: '1px solid var(--border-light)', borderRadius: 10,
            display: 'flex', alignItems: 'center', gap: 12,
          }}>
            <Avatar name="Amara Okonkwo" size={36} color="#e8a838" textColor="#fff"/>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ fontSize: 13.5, fontWeight: 600, color: 'var(--text-primary)' }}>Amara Okonkwo</div>
              <div style={{ fontSize: 11.5, fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>amara@primrose.co</div>
            </div>
            <Pill tone="warning" dot size="sm">Invited</Pill>
          </div>

          {/* Detail rows */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8, fontSize: 12.5 }}>
            <DialogDetailRow label="Role granted"     value={<Pill tone="tint" size="sm">Analyst</Pill>}/>
            <DialogDetailRow label="Invited by"       value="Oliver Chen"/>
            <DialogDetailRow label="Sent at"          value="12 May 2026 — 09:14 UTC" mono/>
            <DialogDetailRow label="Link expires"     value={<span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
              <Icon name="clock" size={11} color="var(--warning)"/>
              <span style={{ fontFamily: 'var(--font-mono)' }}>15 May 09:14 UTC</span>
              <span style={{ color: 'var(--text-tertiary)' }}>— in 72 h</span>
            </span>}/>
          </div>

          {/* Copy-link helper */}
          <div style={{
            padding: '10px 12px', display: 'flex', alignItems: 'center', gap: 10,
            background: 'rgba(232, 168, 56, 0.08)',
            border: '1px solid rgba(232, 168, 56, 0.35)', borderRadius: 9,
            fontSize: 12, color: 'var(--text-secondary)',
          }}>
            <Icon name="info" size={13} color="var(--brand-secondary)"/>
            <span style={{ flex: 1 }}>If the email doesn't arrive, you can copy the invite link and share it directly.</span>
            <button className="btn btn-ghost btn-sm"><Icon name="copy" size={11}/> Copy link</button>
          </div>
        </div>

        {/* Footer */}
        <div style={{
          padding: '14px 24px', borderTop: '1px solid var(--border-light)',
          background: 'var(--surface-inset)',
          display: 'flex', justifyContent: 'space-between', alignItems: 'center',
          borderBottomLeftRadius: 14, borderBottomRightRadius: 14,
        }}>
          <button className="btn btn-ghost btn-sm"><Icon name="send" size={12}/> Resend later</button>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-outline btn-sm">View user</button>
            <button className="btn btn-primary btn-sm">Done</button>
          </div>
        </div>
      </div>
    </div>
  );
}

function DialogDetailRow({ label, value, mono }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
      <span style={{ color: 'var(--text-tertiary)' }}>{label}</span>
      <span style={{ color: 'var(--text-primary)', fontWeight: 500, fontFamily: mono ? 'var(--font-mono)' : 'inherit' }}>{value}</span>
    </div>
  );
}

// Dimmed user list rendered behind invite-sent + delete dialogs
function UsersListBackdrop() {
  return (
    <div style={{ padding: '24px 32px', opacity: 0.5, pointerEvents: 'none' }}>
      <PageHeader
        eyebrow="Finance — Access"
        title="Users"
        subtitle="10 team members — 5 active — 2 pending invite — 1 sessions revoked"
        actions={<>
          <button className="btn btn-outline btn-sm"><Icon name="shield" size={12}/> Roles</button>
          <button className="btn btn-primary btn-sm"><Icon name="userplus" size={12}/> Invite user</button>
        </>}
      />
      <div style={{ marginTop: 18, background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, overflow: 'hidden' }}>
        {LIFECYCLE_USERS.slice(0, 6).map((u, i, arr) => {
          const p = lifecycleStatusProps(u.status);
          return (
            <div key={u.id} style={{
              display: 'grid', gridTemplateColumns: '1.4fr 1fr 100px 130px',
              alignItems: 'center', gap: 14, padding: '13px 16px',
              borderBottom: i < arr.length - 1 ? '1px solid var(--border-light)' : 'none',
            }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <Avatar name={u.name} size={28} color={u.color} textColor="#fff"/>
                <div>
                  <div style={{ fontSize: 13, fontWeight: 500 }}>{u.name}</div>
                  <div style={{ fontSize: 11, fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>{u.email}</div>
                </div>
              </div>
              <span style={{ fontSize: 11.5, color: 'var(--text-secondary)' }}>{u.role}</span>
              <span style={{ fontSize: 11, fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>{u.last}</span>
              <Pill tone={p.tone} dot size="sm">{p.label}</Pill>
            </div>
          );
        })}
      </div>
    </div>
  );
}

// ─── 4 — ScreenUserDeleteDialog ────────────────────────────────────
// Destructive type-email-to-confirm dialog. Backdrop is the user
// detail page, dimmed.
function ScreenUserDeleteDialog() {
  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <UserDetailBackdrop/>
      <div style={{ position: 'absolute', inset: 0, background: 'rgba(15, 20, 28, 0.5)', backdropFilter: 'blur(3px)' }}/>

      {/* Dialog */}
      <div style={{
        position: 'absolute', top: '50%', left: '50%', transform: 'translate(-50%, -50%)',
        width: 540, background: 'var(--surface)',
        borderRadius: 14, boxShadow: '0 24px 60px -10px rgba(0,0,0,0.45)',
        border: '1px solid var(--border-light)',
        display: 'flex', flexDirection: 'column',
      }}>
        {/* Header */}
        <div style={{
          padding: '20px 24px 16px', borderBottom: '1px solid var(--border-light)',
          display: 'flex', alignItems: 'center', gap: 14,
        }}>
          <div style={{
            width: 42, height: 42, borderRadius: 10,
            background: 'rgba(217, 122, 108, 0.14)',
            color: 'var(--danger)',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            flexShrink: 0,
          }}><Icon name="trash" size={20}/></div>
          <div style={{ flex: 1 }}>
            <div style={{ fontSize: 16, fontWeight: 700, color: 'var(--text-primary)', fontFamily: 'var(--font-brand)', letterSpacing: '-0.005em' }}>Delete Maria Gomez</div>
            <div style={{ fontSize: 12.5, color: 'var(--text-secondary)', marginTop: 2 }}>
              This permanently removes the user. The action cannot be undone.
            </div>
          </div>
          <span className="hover-halo" style={{ width: 28, height: 28, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', borderRadius: 6 }}>
            <Icon name="close" size={14}/>
          </span>
        </div>

        {/* What happens */}
        <div style={{ padding: '18px 24px 4px' }}>
          <div style={{ fontSize: 11, letterSpacing: '0.08em', textTransform: 'uppercase', color: 'var(--text-tertiary)', fontWeight: 600, marginBottom: 10 }}>
            What happens
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 9 }}>
            {[
              { icon: 'userx',     text: 'User record removed from AnkUsers — cannot be reactivated' },
              { icon: 'ban',       text: 'All 3 active sessions revoked instantly (FusionCache invalidated)' },
              { icon: 'globe',     text: 'IdP user deleted via Auth0 management API — email freed for reuse' },
              { icon: 'tombstone', text: 'Audit-log PII redacted to a tombstone for compliance integrity' },
            ].map((row, i) => (
              <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 10, fontSize: 12.5, color: 'var(--text-secondary)' }}>
                <div style={{
                  width: 22, height: 22, borderRadius: 6,
                  background: 'rgba(217, 122, 108, 0.10)',
                  color: 'var(--danger)',
                  display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                  flexShrink: 0,
                }}><Icon name={row.icon} size={12}/></div>
                {row.text}
              </div>
            ))}
          </div>
        </div>

        {/* Inputs */}
        <div style={{ padding: '18px 24px 4px', display: 'flex', flexDirection: 'column', gap: 14 }}>
          <div>
            <div style={{ fontSize: 11, letterSpacing: '0.06em', textTransform: 'uppercase', color: 'var(--text-tertiary)', fontWeight: 600, marginBottom: 6 }}>
              Type <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-primary)', textTransform: 'none', letterSpacing: 0 }}>maria@primrose.co</span> to confirm
            </div>
            <input
              type="text"
              defaultValue="maria@primrose.co"
              style={{
                width: '100%', padding: '10px 12px',
                border: '1px solid var(--success)',
                borderRadius: 8, fontSize: 13.5,
                fontFamily: 'var(--font-mono)',
                background: 'rgba(106, 191, 110, 0.06)',
                color: 'var(--text-primary)',
                outline: 'none',
                boxShadow: '0 0 0 3px rgba(106, 191, 110, 0.12)',
              }}
            />
            <div style={{ fontSize: 11, color: 'var(--success)', marginTop: 5, display: 'inline-flex', alignItems: 'center', gap: 5 }}>
              <Icon name="check" size={10}/> Email matches
            </div>
          </div>

          <div>
            <div style={{ fontSize: 11, letterSpacing: '0.06em', textTransform: 'uppercase', color: 'var(--text-tertiary)', fontWeight: 600, marginBottom: 6 }}>
              Reason <span style={{ textTransform: 'none', letterSpacing: 0, color: 'var(--text-tertiary)' }}>— will be recorded on the tombstone</span>
            </div>
            <textarea
              rows={3}
              defaultValue="Contractor end-of-engagement — 12 May 2026. Approved by CFO."
              style={{
                width: '100%', padding: '10px 12px',
                border: '1px solid var(--border-light)',
                borderRadius: 8, fontSize: 13,
                fontFamily: 'var(--font-sans)',
                background: 'var(--surface)', color: 'var(--text-primary)',
                outline: 'none', resize: 'vertical',
              }}
            />
            <div style={{ fontSize: 11, color: 'var(--text-tertiary)', marginTop: 5 }}>62 / 500 characters — minimum 10</div>
          </div>
        </div>

        {/* Footer */}
        <div style={{
          padding: '14px 24px', borderTop: '1px solid var(--border-light)',
          background: 'var(--surface-inset)',
          display: 'flex', justifyContent: 'space-between', alignItems: 'center',
          marginTop: 14,
          borderBottomLeftRadius: 14, borderBottomRightRadius: 14,
        }}>
          <div style={{ fontSize: 11.5, color: 'var(--text-tertiary)', display: 'inline-flex', alignItems: 'center', gap: 6 }}>
            <Icon name="alertc" size={12} color="var(--warning)"/>
            GDPR Article 17 — audit log will retain only a tombstone ref
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-ghost btn-sm">Cancel</button>
            <button className="btn btn-danger btn-sm">
              <Icon name="trash" size={12}/> Permanently delete user
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

// Dimmed UserDetail page rendered behind the delete dialog
function UserDetailBackdrop() {
  const u = LIFECYCLE_USERS.find(x => x.id === '2');
  return (
    <div style={{ padding: '24px 32px', opacity: 0.45, pointerEvents: 'none' }}>
      <PageHeader
        eyebrow="Finance — Access — Users"
        title={u.name}
        subtitle={u.email}
        actions={<>
          <button className="btn btn-outline btn-sm"><Icon name="edit" size={12}/> Edit</button>
          <button className="btn btn-danger  btn-sm"><Icon name="trash" size={12}/> Delete user…</button>
        </>}
      />
      <div style={{
        marginTop: 18, padding: '20px 22px', background: 'var(--surface)',
        border: '1px solid var(--border-light)', borderRadius: 12,
        display: 'flex', alignItems: 'center', gap: 18,
      }}>
        <Avatar name={u.name} size={64} color={u.color} textColor="#fff"/>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          <div style={{ fontSize: 17, fontWeight: 700 }}>{u.name}</div>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-tertiary)' }}>{u.email}</div>
          <div style={{ display: 'flex', gap: 8, marginTop: 4 }}>
            <Pill tone="tint" size="sm">{u.role}</Pill>
            <Pill tone="success" dot size="sm">Active</Pill>
          </div>
        </div>
      </div>
    </div>
  );
}

// ─── 5 — ScreenComplianceTombstones ────────────────────────────────
// Compliance review of historical deletions. PII is already redacted,
// only operator/reason/date remain.
function ScreenComplianceTombstones() {
  const cols = [
    { key: 'id', label: 'Tombstone', w: '130px',
      render: r => <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--brand-primary)', fontWeight: 600 }}>{r.id}</span> },
    { key: 'original', label: 'Original user — redacted', w: '1.4fr',
      render: r => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <div style={{
            width: 30, height: 30, borderRadius: '50%',
            background: 'var(--surface-inset)', border: '1px dashed var(--border)',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
            color: 'var(--text-tertiary)',
          }}><Icon name="tombstone" size={14}/></div>
          <div>
            <div style={{ fontSize: 13, fontWeight: 500, color: 'var(--text-primary)' }}>{r.original}</div>
            <div style={{ fontSize: 11, fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>{r.emailRedacted}</div>
          </div>
        </div>
      ) },
    { key: 'role', label: 'Role at delete', w: '1fr',
      render: r => <span style={{ fontSize: 11.5, color: 'var(--text-secondary)' }}>{r.role}</span> },
    { key: 'deletedBy', label: 'Deleted by', w: '1fr',
      render: r => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <Avatar name={r.deletedBy} size={22} color={agentColor(r.deletedBy) + '22'} textColor={agentColor(r.deletedBy)}/>
          <span style={{ fontSize: 12.5, color: 'var(--text-primary)' }}>{r.deletedBy}</span>
        </div>
      ) },
    { key: 'at', label: 'When', w: '160px', mono: true, fontSize: 11.5,
      render: r => <span style={{ color: 'var(--text-secondary)' }}>{r.at}</span> },
    { key: 'reason', label: 'Reason', w: '1.6fr',
      render: r => <span style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{r.reason}</span> },
  ];

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 20 }}>
      <PageHeader
        eyebrow="Operations — Compliance"
        title="Deleted users"
        subtitle="Tombstones for GDPR Article 17 erasures — retained 7 years — PII redacted on creation"
        actions={<>
          <button className="btn btn-outline btn-sm"><Icon name="download" size={12}/> Export CSV</button>
          <button className="btn btn-outline btn-sm"><Icon name="activity" size={12}/> Audit log</button>
        </>}
      />

      {/* KPI strip */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 14 }}>
        {[
          { l: 'Tombstones — all time', v: '218',  tone: 'var(--brand-primary)' },
          { l: 'Last 30 days',          v: '6',    tone: 'var(--success)' },
          { l: 'GDPR erasure requests', v: '4',    tone: 'var(--warning)' },
          { l: 'Avg time to delete',    v: '1.8h', tone: 'var(--brand-secondary)' },
        ].map((s, i) => (
          <div key={i} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 7, fontSize: 11, color: 'var(--text-secondary)' }}>
              <span style={{ width: 6, height: 6, borderRadius: 999, background: s.tone }}/>{s.l}
            </div>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 22, fontWeight: 600, color: 'var(--text-primary)', marginTop: 4 }}>{s.v}</div>
          </div>
        ))}
      </div>

      <UlcFilterBar
        tabs={[
          { label: 'All',               count: 218, active: true },
          { label: 'GDPR erasure',      count: 92,  tone: 'pending' },
          { label: 'Offboard',          count: 104 },
          { label: 'Created in error',  count: 22 },
        ]}
        search="Filter by operator, reason, tombstone id…"
        extra={<button className="btn btn-ghost btn-sm"><Icon name="clock" size={12}/> Last 90 days</button>}
      />

      <DataTable cols={cols} rows={TOMBSTONES} footer={<TableFooter showing="1–6" total="218 tombstones" page={1} pages={37}/>}/>

      {/* Footer note */}
      <div style={{
        padding: '14px 18px',
        background: 'var(--surface-inset)', border: '1px solid var(--border-light)',
        borderRadius: 10,
        display: 'flex', alignItems: 'center', gap: 12, fontSize: 12, color: 'var(--text-secondary)',
      }}>
        <Icon name="info" size={14} color="var(--brand-primary)"/>
        <span>
          Tombstones are immutable. Original user-ids are preserved for foreign-key remapping in the audit log; all
          other PII (name, email, phone, IdP subject) was redacted at the moment of deletion.
        </span>
      </div>
    </div>
  );
}

Object.assign(window, {
  ScreenUsersLifecycle,
  ScreenUserDetail,
  ScreenUserInviteSent,
  ScreenUserDeleteDialog,
  ScreenComplianceTombstones,
});
