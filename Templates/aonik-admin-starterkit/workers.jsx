// Background Workers — table view with KPIs and per-row sparklines.
// Reads window.JOBS / window.FLEET from mock-data.js.

// ── helpers ────────────────────────────────────────────────────────────
const fmtRelative = (iso) => {
  if (!iso) return '—';
  const now = Date.now();
  const t = new Date(iso).getTime();
  const diff = now - t;
  const future = diff < 0;
  const sec = Math.floor(Math.abs(diff) / 1000);
  if (sec < 60) return future ? 'in <1m' : 'just now';
  const min = Math.floor(sec / 60);
  if (min < 60) return future ? `in ${min}m` : `${min}m ago`;
  const hr = Math.floor(min / 60);
  if (hr < 24) return future ? `in ${hr}h` : `${hr}h ago`;
  const day = Math.floor(hr / 24);
  return future ? `in ${day}d` : `${day}d ago`;
};

const fmtDuration = (ms) => {
  if (ms == null) return '—';
  if (ms < 1000) return `${ms}ms`;
  if (ms < 60_000) return `${(ms / 1000).toFixed(1)}s`;
  const m = Math.floor(ms / 60_000);
  const s = Math.floor((ms % 60_000) / 1000);
  return s ? `${m}m ${s}s` : `${m}m`;
};

const cronHumanize = (cron) => {
  if (!cron) return 'No schedule';
  // tiny in-house humanizer (real app uses cronstrue)
  const parts = cron.trim().split(/\s+/);
  if (parts.length < 6) return cron;
  const [, min, hr, dom, , dow] = parts;
  if (min === '*/15' && hr === '*') return 'Every 15 min';
  if (min === '*/5'  && hr === '*') return 'Every 5 min';
  if (min === '30'   && hr === '*') return 'Hourly · :30';
  if (min === '0'    && hr === '*/1') return 'Every hour';
  if (min === '0'    && hr === '23' && dom === '*') return 'Daily · 23:00';
  if (min === '0'    && hr === '4'  && dom === '*') return 'Daily · 04:00';
  if (min === '0'    && hr === '3'  && dom === '*') return 'Daily · 03:00';
  if (min === '0'    && hr === '1'  && dom === '*') return 'Daily · 01:00';
  if (min === '0'    && hr === '2'  && dom === '1') return 'Monthly · day 1, 02:00';
  if (min === '0'    && hr === '8'  && dow === 'MON') return 'Weekly · Mon 08:00';
  if (min === '0'    && hr === '6'  && dow === 'MON') return 'Weekly · Mon 06:00';
  return cron;
};

// status meta — drives both the row pill and the dot color
const STATUS_META = {
  Succeeded: { label: 'Healthy',   tone: 'success', dotColor: 'var(--success)' },
  Failed:    { label: 'Failed',    tone: 'danger',  dotColor: 'var(--danger)' },
  Running:   { label: 'Running',   tone: 'tint',    dotColor: 'var(--brand-primary)' },
  Retrying:  { label: 'Retrying',  tone: 'warning', dotColor: 'var(--warning)' },
  TimedOut:  { label: 'Timed out', tone: 'danger',  dotColor: 'var(--danger)' },
  Skipped:   { label: 'Skipped',   tone: 'default', dotColor: 'var(--text-tertiary)' },
  Queued:    { label: 'Queued',    tone: 'default', dotColor: 'var(--text-tertiary)' },
};

const RUN_DOT = {
  Succeeded: 'var(--success)',
  Failed:    'var(--danger)',
  TimedOut:  'var(--danger)',
  Skipped:   'var(--text-tertiary)',
};

function StatusPill({ outcome, status }) {
  // Disabled / Paused take precedence
  if (status === 'Paused')   return <Pill tone="warning" dot>Paused</Pill>;
  if (status === 'Disabled') return <Pill tone="default" dot>Disabled</Pill>;
  const meta = STATUS_META[outcome] || STATUS_META.Queued;
  return <Pill tone={meta.tone} dot>
    {outcome === 'Running' && <span className="job-pulse"/>}
    {meta.label}
  </Pill>;
}

// ── Sparkline: 20 small bars colored by run outcome ───────────────────
function RunHistory({ history, w = 116, h = 28 }) {
  if (!history || !history.length) return <span style={{fontFamily:'var(--font-mono)', fontSize:11, color:'var(--text-tertiary)'}}>—</span>;
  const max = Math.max(...history.map(r => r.durationMs || 1)) || 1;
  const bw = (w - (history.length - 1) * 2) / history.length;
  return (
    <svg width={w} height={h} viewBox={`0 0 ${w} ${h}`} style={{ display: 'block' }}>
      {history.map((r, i) => {
        const bh = Math.max(2, Math.round((r.durationMs || 1) / max * (h - 2)));
        const x  = i * (bw + 2);
        const y  = h - bh;
        const fill = RUN_DOT[r.outcome] || 'var(--brand-primary)';
        return <rect key={i} x={x} y={y} width={bw} height={bh}
          fill={fill}
          opacity={r.outcome === 'Succeeded' ? 0.55 : 0.95}
          rx={1}
        >
          <title>{`#${r.idx} · ${r.outcome} · ${fmtDuration(r.durationMs)}`}</title>
        </rect>;
      })}
    </svg>
  );
}

// ── KPI tile (slim) ────────────────────────────────────────────────────
function WorkerKPI({ label, value, sub, tone = 'default', icon }) {
  const accentBg = {
    danger:  'var(--danger-light)',
    warning: 'var(--warning-light)',
    success: 'var(--success-light)',
    tint:    'var(--brand-primary-10)',
    default: 'var(--surface-inset)',
  }[tone];
  const accentFg = {
    danger:  'var(--danger)',
    warning: '#8a6d0a',
    success: 'var(--success)',
    tint:    'var(--brand-primary)',
    default: 'var(--text-secondary)',
  }[tone];
  return (
    <div style={{
      flex: 1, minWidth: 0,
      background: 'var(--surface)',
      border: '1px solid var(--border-light)',
      borderRadius: 12,
      padding: '14px 16px',
      display: 'flex', alignItems: 'center', gap: 14,
    }}>
      <div style={{
        width: 36, height: 36, flex: 'none', borderRadius: 10,
        background: accentBg, color: accentFg,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
      }}>
        <Icon name={icon} size={18}/>
      </div>
      <div style={{ minWidth: 0, flex: 1 }}>
        <div style={{ fontSize: 11, color: 'var(--text-secondary)', fontWeight: 500, textTransform: 'uppercase', letterSpacing: '0.04em' }}>{label}</div>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginTop: 2 }}>
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: 22, fontWeight: 600, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>{value}</span>
          {sub && <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{sub}</span>}
        </div>
      </div>
    </div>
  );
}

// ── Filter bar ────────────────────────────────────────────────────────
function FilterBar({ filter, setFilter, search, setSearch, autoRefresh, setAutoRefresh, onRefresh }) {
  const tabs = [
    { id: 'all',     label: 'All',       count: window.JOBS.length },
    { id: 'failing', label: 'Failing',   count: window.JOBS.filter(j => j.lastOutcome === 'Failed' || j.lastOutcome === 'TimedOut').length, tone: 'danger' },
    { id: 'running', label: 'Active',    count: window.JOBS.filter(j => j.lastOutcome === 'Running' || j.lastOutcome === 'Retrying').length, tone: 'tint' },
    { id: 'paused',  label: 'Paused',    count: window.JOBS.filter(j => j.status === 'Paused' || j.status === 'Disabled').length },
  ];
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 14, padding: '14px 24px',
      borderBottom: '1px solid var(--border-light)', background: 'var(--surface)',
    }}>
      <div style={{ display: 'flex', gap: 4, padding: 3, background: 'var(--surface-inset)', borderRadius: 8, border: '1px solid var(--border-light)' }}>
        {tabs.map(t => {
          const active = filter === t.id;
          return (
            <button key={t.id} onClick={() => setFilter(t.id)} style={{
              border: 'none', background: active ? 'var(--surface)' : 'transparent',
              padding: '5px 12px', borderRadius: 6, cursor: 'pointer',
              fontFamily: 'inherit', fontSize: 12, fontWeight: active ? 600 : 500,
              color: active ? 'var(--text-primary)' : 'var(--text-secondary)',
              boxShadow: active ? '0 1px 2px rgb(0 0 0 / 0.04)' : 'none',
              display: 'inline-flex', alignItems: 'center', gap: 6,
            }}>
              {t.label}
              <span style={{
                fontFamily: 'var(--font-mono)', fontSize: 10, fontWeight: 600,
                padding: '1px 6px', borderRadius: 4,
                background: active && t.tone === 'danger' ? 'var(--danger-light)' :
                            active && t.tone === 'tint'   ? 'var(--brand-primary-10)' :
                            'var(--surface-inset)',
                color: active && t.tone === 'danger' ? 'var(--danger)' :
                       active && t.tone === 'tint'   ? 'var(--brand-primary)' :
                       'var(--text-tertiary)',
              }}>{t.count}</span>
            </button>
          );
        })}
      </div>

      <div style={{ position: 'relative', flex: 1, maxWidth: 360 }}>
        <span style={{ position: 'absolute', left: 10, top: '50%', transform: 'translateY(-50%)', color: 'var(--text-tertiary)' }}>
          <Icon name="search" size={14}/>
        </span>
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="input"
          placeholder="Search jobs by name…"
          style={{ paddingLeft: 32, height: 32, fontSize: 13 }}
        />
      </div>

      <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 8 }}>
        <label style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 12, color: 'var(--text-secondary)', cursor: 'pointer' }}>
          <input type="checkbox" checked={autoRefresh} onChange={(e) => setAutoRefresh(e.target.checked)}
            style={{ accentColor: 'var(--brand-primary)' }}/>
          Auto-refresh
        </label>
        <button className="btn btn-outline btn-sm" onClick={onRefresh} style={{ height: 30, padding: '0 10px' }}>
          <Icon name="refresh" size={12}/> Refresh
        </button>
      </div>
    </div>
  );
}

// ── Row action menu (kebab) ───────────────────────────────────────────
function RowMenu({ job, onClose, onAction }) {
  const items = [
    { id: 'run',     label: 'Run now',           icon: 'zap'      },
    { id: 'retry',   label: 'Retry last failed', icon: 'refresh', show: job.lastOutcome === 'Failed' || job.lastOutcome === 'TimedOut' },
    { id: 'pause',   label: 'Pause job',         icon: 'minus',   show: job.status === 'Active' },
    { id: 'resume',  label: 'Resume job',        icon: 'arrowright', show: job.status === 'Paused' },
    { id: 'history', label: 'View full history', icon: 'clock'    },
    { id: 'edit',    label: 'Edit schedule',     icon: 'calendar' },
    { id: 'copy',    label: 'Copy job ID',       icon: 'link'     },
  ].filter(i => i.show !== false);
  return (
    <div onClick={(e) => e.stopPropagation()} style={{
      position: 'absolute', top: '100%', right: 0, marginTop: 6,
      background: 'var(--surface-elevated)',
      border: '1px solid var(--border-light)',
      borderRadius: 10, boxShadow: 'var(--shadow-md)',
      minWidth: 200, padding: 4, zIndex: 20,
    }}>
      {items.map(i => (
        <button key={i.id} onClick={() => { onAction(i.id, job); onClose(); }} style={{
          display: 'flex', alignItems: 'center', gap: 10, width: '100%',
          border: 'none', background: 'transparent', cursor: 'pointer',
          padding: '7px 10px', borderRadius: 6, fontFamily: 'inherit', fontSize: 13,
          color: 'var(--text-primary)', textAlign: 'left',
        }}
        onMouseOver={(e) => e.currentTarget.style.background = 'var(--surface-inset)'}
        onMouseOut={(e) => e.currentTarget.style.background = 'transparent'}>
          <Icon name={i.icon} size={14} color="var(--text-secondary)"/>
          {i.label}
        </button>
      ))}
    </div>
  );
}

// ── Single row ────────────────────────────────────────────────────────
function JobRow({ job, onOpen, onAction }) {
  const [menuOpen, setMenuOpen] = React.useState(false);
  const isFailing = job.lastOutcome === 'Failed' || job.lastOutcome === 'TimedOut';
  const hasAgent  = !!job.agent;

  return (
    <tr
      onClick={() => onOpen(job)}
      style={{
        cursor: 'pointer',
        background: 'var(--surface)',
        borderBottom: '1px solid var(--border-light)',
      }}
      onMouseOver={(e) => e.currentTarget.style.background = 'var(--surface-inset)'}
      onMouseOut={(e) => e.currentTarget.style.background = 'var(--surface)'}
    >
      {/* status dot column */}
      <td style={{ width: 6, padding: 0, borderLeft: isFailing ? '3px solid var(--danger)' : '3px solid transparent' }}/>

      {/* Job name + group */}
      <td style={{ padding: '14px 12px 14px 14px' }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: 8 }}>
            {job.displayName}
            {hasAgent && isFailing && (
              <span title={`${job.agent.name} agent has a proposal`} style={{
                display: 'inline-flex', alignItems: 'center', gap: 3,
                fontSize: 10, fontWeight: 600, padding: '1px 6px', borderRadius: 999,
                background: 'var(--pending-light)', color: 'var(--pending)',
              }}>
                <Icon name="sparkles" size={10}/> Proposal
              </span>
            )}
          </div>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)' }}>
            {job.groupName} · {job.jobName}
          </div>
        </div>
      </td>

      {/* Schedule */}
      <td style={{ padding: '14px 12px', whiteSpace: 'nowrap' }}>
        <div style={{ fontSize: 12, color: 'var(--text-primary)' }}>{cronHumanize(job.cronExpression)}</div>
        <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)', marginTop: 2 }}>{job.cronExpression || '—'}</div>
      </td>

      {/* Status */}
      <td style={{ padding: '14px 12px' }}>
        <StatusPill outcome={job.lastOutcome} status={job.status}/>
      </td>

      {/* Last run */}
      <td style={{ padding: '14px 12px', whiteSpace: 'nowrap' }}>
        <div style={{ fontSize: 12, color: 'var(--text-primary)' }}>{fmtRelative(job.previousFireTimeUtc)}</div>
        <div style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)', marginTop: 2 }}>
          {job.lastDurationMs != null ? fmtDuration(job.lastDurationMs) : 'in progress'}
        </div>
      </td>

      {/* Avg duration */}
      <td style={{ padding: '14px 12px', whiteSpace: 'nowrap', fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-secondary)' }}>
        {fmtDuration(Math.round((job.history || []).reduce((a, r) => a + (r.durationMs || 0), 0) / Math.max(1, (job.history || []).length)))}
      </td>

      {/* Next run */}
      <td style={{ padding: '14px 12px', whiteSpace: 'nowrap' }}>
        <div style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{fmtRelative(job.nextFireTimeUtc)}</div>
      </td>

      {/* Sparkline */}
      <td style={{ padding: '14px 12px' }}>
        <RunHistory history={job.history}/>
      </td>

      {/* Actions */}
      <td style={{ padding: '14px 14px 14px 8px', textAlign: 'right', whiteSpace: 'nowrap' }}>
        <div style={{ display: 'inline-flex', alignItems: 'center', gap: 2, position: 'relative' }}
          onClick={(e) => e.stopPropagation()}>
          <button className="hover-halo" onClick={() => onAction('run', job)} title="Run now">
            <Icon name="zap" size={14}/>
          </button>
          {job.status === 'Active' ? (
            <button className="hover-halo" onClick={() => onAction('pause', job)} title="Pause">
              <Icon name="minus" size={14}/>
            </button>
          ) : (
            <button className="hover-halo" onClick={() => onAction('resume', job)} title="Resume">
              <Icon name="arrowright" size={14}/>
            </button>
          )}
          <button className="hover-halo" onClick={() => setMenuOpen(o => !o)} title="More">
            <Icon name="more" size={14}/>
          </button>
          {menuOpen && <RowMenu job={job} onClose={() => setMenuOpen(false)} onAction={onAction}/>}
        </div>
      </td>
    </tr>
  );
}

// ── The page ──────────────────────────────────────────────────────────
function WorkersScreen({ onOpen, onAction }) {
  const [filter, setFilter] = React.useState('all');
  const [search, setSearch] = React.useState('');
  const [autoRefresh, setAutoRefresh] = React.useState(true);

  const filtered = React.useMemo(() => {
    let arr = window.JOBS;
    if (filter === 'failing') arr = arr.filter(j => j.lastOutcome === 'Failed' || j.lastOutcome === 'TimedOut');
    else if (filter === 'running') arr = arr.filter(j => j.lastOutcome === 'Running' || j.lastOutcome === 'Retrying');
    else if (filter === 'paused')  arr = arr.filter(j => j.status === 'Paused' || j.status === 'Disabled');
    if (search) arr = arr.filter(j => (j.displayName + j.jobName).toLowerCase().includes(search.toLowerCase()));
    return arr;
  }, [filter, search]);

  const F = window.FLEET;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      {/* Page header */}
      <div style={{ padding: '24px 24px 18px', borderBottom: '1px solid var(--border-light)' }}>
        <div style={{ display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between', marginBottom: 16, gap: 16 }}>
          <div>
            <div style={{ fontSize: 11, fontWeight: 600, letterSpacing: '0.08em', textTransform: 'uppercase', color: 'var(--brand-primary)' }}>
              Platform · Scheduler
            </div>
            <h1 style={{ fontFamily: 'var(--font-brand)', fontSize: 24, fontWeight: 700, letterSpacing: '-0.01em', margin: '4px 0 4px', color: 'var(--text-primary)' }}>
              Background workers
            </h1>
            <div style={{ fontSize: 13, color: 'var(--text-secondary)', maxWidth: 720 }}>
              Every Quartz-scheduled pipeline in this workspace. Click any row to inspect the latest run output, logs, and parameters.
            </div>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 11, fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>
              <span style={{ width: 6, height: 6, borderRadius: 999, background: 'var(--success)' }}/>
              scheduler · running · 8 threads
            </span>
          </div>
        </div>

        {/* KPIs */}
        <div style={{ display: 'flex', gap: 12 }}>
          <WorkerKPI label="Total jobs"    value={F.total}              sub="registered" icon="bot"  tone="default"/>
          <WorkerKPI label="Failing"        value={F.failing}            sub="last run"   icon="warn" tone="danger"/>
          <WorkerKPI label="In flight"     value={F.running}            sub="now"        icon="refresh" tone="tint"/>
          <WorkerKPI label="Paused"        value={F.paused}             sub="manual"     icon="minus" tone="warning"/>
          <WorkerKPI label="Success rate"  value={`${F.successRate}%`}  sub={`of ${F.totalRuns} runs · 24h`} icon="trend" tone="success"/>
        </div>
      </div>

      {/* Filter bar */}
      <FilterBar
        filter={filter} setFilter={setFilter}
        search={search} setSearch={setSearch}
        autoRefresh={autoRefresh} setAutoRefresh={setAutoRefresh}
        onRefresh={() => {}}
      />

      {/* Table */}
      <div style={{ flex: 1, overflow: 'auto', padding: '0 24px 24px' }}>
        <div style={{
          marginTop: 16,
          background: 'var(--surface)',
          border: '1px solid var(--border-light)',
          borderRadius: 12, overflow: 'hidden',
        }}>
          <table style={{ width: '100%', borderCollapse: 'separate', borderSpacing: 0 }}>
            <thead>
              <tr style={{ background: 'var(--surface-inset)' }}>
                <th style={thStyle()}/>
                <th style={thStyle({ paddingLeft: 14 })}>Job</th>
                <th style={thStyle()}>Schedule</th>
                <th style={thStyle()}>Status</th>
                <th style={thStyle()}>Last run</th>
                <th style={thStyle()}>Avg</th>
                <th style={thStyle()}>Next run</th>
                <th style={thStyle()}>Last 20 runs</th>
                <th style={thStyle({ textAlign: 'right', paddingRight: 14 })}/>
              </tr>
            </thead>
            <tbody>
              {filtered.map(job => (
                <JobRow key={job.jobName} job={job} onOpen={onOpen} onAction={onAction}/>
              ))}
            </tbody>
          </table>
          {filtered.length === 0 && (
            <div style={{ padding: 40, textAlign: 'center', color: 'var(--text-tertiary)', fontSize: 13 }}>
              No jobs match this filter.
            </div>
          )}
        </div>

        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 14 }}>
          <span style={{ fontSize: 11, fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>
            {filtered.length} of {window.JOBS.length} jobs · last refreshed just now
          </span>
          <span style={{ fontSize: 11, fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>
            scheduler instance · pri-quartz-1
          </span>
        </div>
      </div>
    </div>
  );
}

function thStyle(over = {}) {
  return {
    fontSize: 11,
    fontWeight: 600,
    letterSpacing: '0.04em',
    textTransform: 'uppercase',
    color: 'var(--text-tertiary)',
    textAlign: 'left',
    padding: '10px 12px',
    borderBottom: '1px solid var(--border-light)',
    whiteSpace: 'nowrap',
    ...over,
  };
}

Object.assign(window, { WorkersScreen, fmtRelative, fmtDuration, cronHumanize, StatusPill, RUN_DOT });
