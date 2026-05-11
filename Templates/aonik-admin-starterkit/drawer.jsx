// Run-detail slide-over drawer.
// Header: job name + status + run timing.
// Body sections (varies by outcome):
//  - Agent proposal banner (only if job.agent + failing)
//  - Outcome summary (success or error)
//  - Step trace (multi-step status)
//  - Error details (type/message/stack) — for Failed/TimedOut
//  - Success outcome breakdown — for Succeeded/Skipped
//  - Run logs (collapsible)
//  - Input parameters (collapsible)
//  - Footer actions: retry / mark resolved / view full history

function DrawerSection({ title, action, children, defaultOpen = true, collapsible = false }) {
  const [open, setOpen] = React.useState(defaultOpen);
  return (
    <div style={{ borderTop: '1px solid var(--border-light)' }}>
      <button
        onClick={() => collapsible && setOpen(o => !o)}
        style={{
          width: '100%', border: 'none', background: 'transparent',
          padding: '14px 24px 10px', display: 'flex', alignItems: 'center',
          justifyContent: 'space-between', cursor: collapsible ? 'pointer' : 'default',
          fontFamily: 'inherit',
        }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          {collapsible && (
            <span style={{
              display: 'inline-flex', color: 'var(--text-tertiary)',
              transform: open ? 'rotate(90deg)' : 'rotate(0deg)',
              transition: 'transform 150ms ease',
            }}>
              <Icon name="chevron" size={12}/>
            </span>
          )}
          <span style={{ fontSize: 11, fontWeight: 600, color: 'var(--text-secondary)', letterSpacing: '0.06em', textTransform: 'uppercase' }}>{title}</span>
        </div>
        {action}
      </button>
      {open && <div style={{ padding: '0 24px 18px' }}>{children}</div>}
    </div>
  );
}

// Step trace row — pipeline-style indicator
function StepRow({ step, idx, last }) {
  const META = {
    ok:       { color: 'var(--success)',        icon: 'check', label: 'ok'      },
    failed:   { color: 'var(--danger)',         icon: 'close', label: 'failed'  },
    timedout: { color: 'var(--danger)',         icon: 'clock', label: 'timeout' },
    running:  { color: 'var(--brand-primary)',  icon: 'refresh', label: 'running' },
    retrying: { color: 'var(--warning)',        icon: 'refresh', label: 'retrying'},
    skipped:  { color: 'var(--text-tertiary)',  icon: 'minus', label: 'skipped' },
    pending:  { color: 'var(--text-tertiary)',  icon: 'dot',   label: 'pending' },
  };
  const meta = META[step.status] || META.pending;
  return (
    <div style={{ display: 'flex', gap: 12 }}>
      {/* gutter */}
      <div style={{ width: 22, flex: 'none', display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
        <div style={{
          width: 22, height: 22, borderRadius: 999,
          background: step.status === 'pending' ? 'transparent' : `${meta.color}1f`,
          color: meta.color,
          border: step.status === 'pending' ? '1px dashed var(--border)' : 'none',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          flex: 'none',
        }}>
          {step.status === 'running' || step.status === 'retrying'
            ? <span className="job-pulse" style={{ width: 8, height: 8 }}/>
            : <Icon name={meta.icon} size={12}/>}
        </div>
        {!last && <div style={{ flex: 1, width: 1, background: 'var(--border-light)', marginTop: 4 }}/>}
      </div>

      {/* body */}
      <div style={{ flex: 1, paddingBottom: 14, minWidth: 0 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, minWidth: 0 }}>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-primary)', fontWeight: 500 }}>
              {String(idx + 1).padStart(2, '0')}. {step.name}
            </span>
            <span style={{ fontSize: 10, fontWeight: 600, color: meta.color, textTransform: 'uppercase', letterSpacing: '0.04em' }}>
              {meta.label}
            </span>
          </div>
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)' }}>
            {step.durationMs ? fmtDuration(step.durationMs) : '—'}
          </span>
        </div>
        {step.message && (
          <div style={{ fontSize: 12, color: step.status === 'failed' || step.status === 'timedout' ? 'var(--danger)' : 'var(--text-secondary)', marginTop: 4, lineHeight: 1.5 }}>
            {step.message}
          </div>
        )}
      </div>
    </div>
  );
}

function ErrorBlock({ error }) {
  const [showStack, setShowStack] = React.useState(false);
  return (
    <div>
      <div style={{
        background: 'var(--danger-light)',
        border: '1px solid #cc2e2e30',
        borderRadius: 10, padding: '12px 14px', marginBottom: 12,
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6 }}>
          <Icon name="warn" size={14} color="var(--danger)"/>
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--danger)', fontWeight: 600 }}>
            {error.type}
          </span>
        </div>
        <div style={{ fontSize: 13, color: 'var(--text-primary)', lineHeight: 1.55 }}>
          {error.message}
        </div>
      </div>

      <button onClick={() => setShowStack(s => !s)} style={{
        border: '1px solid var(--border-light)', background: 'var(--surface)',
        padding: '6px 10px', borderRadius: 6, cursor: 'pointer',
        fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)',
        display: 'inline-flex', alignItems: 'center', gap: 6,
      }}>
        <Icon name={showStack ? 'chevdown' : 'chevron'} size={11}/>
        {showStack ? 'Hide' : 'Show'} stack trace
      </button>

      {showStack && (
        <pre style={{
          marginTop: 10, padding: 14,
          background: 'var(--surface-inset)', borderRadius: 8,
          border: '1px solid var(--border-light)',
          fontFamily: 'var(--font-mono)', fontSize: 11, lineHeight: 1.7,
          color: 'var(--text-primary)', overflow: 'auto',
          maxHeight: 280, whiteSpace: 'pre',
        }}>{error.stack}</pre>
      )}
    </div>
  );
}

function SuccessBlock({ success }) {
  return (
    <div>
      <div style={{
        background: 'var(--success-light)',
        border: '1px solid #4caf5030',
        borderRadius: 10, padding: '12px 14px',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: success.message ? 6 : 0 }}>
          <Icon name="check" size={14} color="var(--success)"/>
          <span style={{ fontSize: 12, color: 'var(--success)', fontWeight: 600 }}>
            Run completed cleanly
          </span>
        </div>
        {success.message && (
          <div style={{ fontSize: 13, color: 'var(--text-primary)', lineHeight: 1.55 }}>
            {success.message}
          </div>
        )}
      </div>

      {(success.recordsProcessed != null || success.totalValue || success.flagged != null) && (
        <div style={{
          display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))',
          gap: 10, marginTop: 12,
        }}>
          {success.recordsProcessed != null && (
            <Stat label="Records" value={success.recordsProcessed.toLocaleString()}/>
          )}
          {success.totalValue && <Stat label="Total value" value={success.totalValue}/>}
          {success.flagged != null && <Stat label="Flagged" value={success.flagged}/>}
        </div>
      )}

      {success.breakdown && (
        <div style={{
          marginTop: 12,
          background: 'var(--surface-inset)',
          border: '1px solid var(--border-light)',
          borderRadius: 8, overflow: 'hidden',
        }}>
          {success.breakdown.map((row, i) => (
            <div key={i} style={{
              display: 'flex', justifyContent: 'space-between', alignItems: 'center',
              padding: '10px 14px',
              borderTop: i === 0 ? 'none' : '1px solid var(--border-light)',
              fontSize: 12,
            }}>
              <span style={{ color: 'var(--text-secondary)' }}>{row.label}</span>
              <span style={{ display: 'inline-flex', gap: 14, alignItems: 'baseline' }}>
                <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-primary)', fontWeight: 600 }}>
                  {row.value.toLocaleString()}
                </span>
                {row.amount && (
                  <span style={{ fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)', fontSize: 11 }}>
                    {row.amount}
                  </span>
                )}
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function Stat({ label, value }) {
  return (
    <div style={{
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 8, padding: '10px 12px',
    }}>
      <div style={{ fontSize: 10, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.06em', fontWeight: 600 }}>{label}</div>
      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 18, fontWeight: 600, color: 'var(--text-primary)', marginTop: 2 }}>{value}</div>
    </div>
  );
}

function LogsBlock({ logs }) {
  const colorFor = (lvl) => ({
    error: 'var(--danger)', warn: '#a87800', info: 'var(--text-secondary)',
  })[lvl] || 'var(--text-secondary)';
  return (
    <div style={{
      background: '#0f1115', borderRadius: 8, padding: '10px 12px',
      maxHeight: 240, overflow: 'auto',
      fontFamily: 'var(--font-mono)', fontSize: 11, lineHeight: 1.7,
    }}>
      {logs.map((l, i) => (
        <div key={i} style={{ display: 'grid', gridTemplateColumns: '90px 56px 1fr', gap: 10, color: '#cdd2da' }}>
          <span style={{ color: '#6b7078' }}>{l.t}</span>
          <span style={{
            color: colorFor(l.level), textTransform: 'uppercase', fontWeight: 600,
          }}>{l.level}</span>
          <span style={{ color: l.level === 'error' ? '#ff8a8a' : l.level === 'warn' ? '#ffd061' : '#cdd2da' }}>
            {l.msg}
          </span>
        </div>
      ))}
    </div>
  );
}

function ParamsBlock({ params }) {
  return (
    <pre style={{
      margin: 0, padding: 12,
      background: 'var(--surface-inset)', borderRadius: 8,
      border: '1px solid var(--border-light)',
      fontFamily: 'var(--font-mono)', fontSize: 11, lineHeight: 1.65,
      color: 'var(--text-primary)', overflow: 'auto',
      maxHeight: 240, whiteSpace: 'pre',
    }}>{JSON.stringify(params, null, 2)}</pre>
  );
}

function AgentProposalBanner({ agent, jobName }) {
  return (
    <div style={{
      margin: '0 24px 18px', padding: 14,
      background: 'var(--surface)',
      border: '1px solid var(--border-light)',
      borderLeft: '3px solid var(--brand-secondary)',
      borderRadius: 10,
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
        <Avatar name={agent.name} size={22} color="var(--brand-primary-10)" textColor="var(--brand-primary)"/>
        <span style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-primary)' }}>{agent.name} Agent</span>
        <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-secondary)' }}>conf · {agent.confidence.toFixed(2)}</span>
        <span style={{ marginLeft: 'auto', fontSize: 10, fontWeight: 600, color: 'var(--pending)', textTransform: 'uppercase', letterSpacing: '0.06em' }}>Proposal</span>
      </div>
      <div style={{ fontSize: 13, color: 'var(--text-primary)', lineHeight: 1.55, marginBottom: 6 }}>
        {agent.summary}
      </div>
      <div style={{
        background: 'var(--surface-inset)', borderRadius: 6,
        padding: '8px 10px', fontFamily: 'var(--font-mono)', fontSize: 11,
        color: 'var(--text-primary)', marginBottom: 8,
      }}>
        <span style={{ color: 'var(--success)' }}>+ </span>
        {agent.action}
      </div>
      <div style={{ fontSize: 11, color: 'var(--text-secondary)', lineHeight: 1.5, marginBottom: 10 }}>
        {agent.reasoning}
      </div>
      <div style={{ display: 'flex', gap: 6 }}>
        <button className="btn btn-secondary btn-sm">Apply</button>
        <button className="btn btn-outline btn-sm">Review</button>
        <button className="btn btn-ghost btn-sm">Dismiss</button>
      </div>
    </div>
  );
}

// ── The drawer ─────────────────────────────────────────────────────────
function RunDrawer({ job, onClose, onAction, agentLayer }) {
  const open = !!job;
  // lock body scroll while open
  React.useEffect(() => {
    if (!open) return;
    const onKey = (e) => { if (e.key === 'Escape') onClose(); };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  if (!open) return null;
  const run = job.lastRun;
  const isFailed   = job.lastOutcome === 'Failed' || job.lastOutcome === 'TimedOut';
  const isRunning  = job.lastOutcome === 'Running' || job.lastOutcome === 'Retrying';
  const isSucc     = job.lastOutcome === 'Succeeded' || job.lastOutcome === 'Skipped';

  return (
    <>
      {/* dim backdrop */}
      <div onClick={onClose} style={{
        position: 'fixed', inset: 0, zIndex: 90,
        background: 'rgba(15, 17, 21, 0.45)',
        animation: 'drawerFadeIn 180ms ease forwards',
      }}/>

      {/* drawer */}
      <aside style={{
        position: 'fixed', top: 0, right: 0, bottom: 0, zIndex: 91,
        width: 'min(640px, 92vw)',
        background: 'var(--surface)',
        borderLeft: '1px solid var(--border-light)',
        boxShadow: 'var(--shadow-lg)',
        display: 'flex', flexDirection: 'column',
        animation: 'drawerSlideIn 220ms cubic-bezier(0.2, 0.8, 0.2, 1) forwards',
      }}>
        {/* Header */}
        <div style={{ padding: '20px 24px 14px', borderBottom: '1px solid var(--border-light)' }}>
          <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 14 }}>
            <div style={{ minWidth: 0 }}>
              <div style={{ fontSize: 11, fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)', marginBottom: 4 }}>
                {job.groupName} · {job.jobName}
              </div>
              <h2 style={{ fontFamily: 'var(--font-brand)', fontSize: 19, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em', margin: '0 0 8px' }}>
                {job.displayName}
              </h2>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
                <StatusPill outcome={job.lastOutcome} status={job.status}/>
                <span style={{ fontSize: 12, color: 'var(--text-tertiary)' }}>·</span>
                <span style={{ fontSize: 12, color: 'var(--text-secondary)' }}>
                  {run?.triggeredBy === 'Manual' ? 'Triggered manually' : 'Triggered by schedule'}
                </span>
              </div>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
              <button className="hover-halo" onClick={() => onAction('history', job)} title="Full history">
                <Icon name="clock" size={14}/>
              </button>
              <button className="hover-halo" onClick={onClose} title="Close">
                <Icon name="close" size={14}/>
              </button>
            </div>
          </div>

          {/* timing strip */}
          {run && (
            <div style={{
              display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)',
              gap: 1, marginTop: 14, background: 'var(--border-light)',
              borderRadius: 8, overflow: 'hidden',
            }}>
              <TimingCell label="Started"   value={run.startedAt ? new Date(run.startedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' }) : '—'} sub={run.startedAt ? fmtRelative(run.startedAt) : ''}/>
              <TimingCell label="Ended"     value={run.endedAt ? new Date(run.endedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' }) : '—'} sub={run.endedAt ? fmtRelative(run.endedAt) : isRunning ? 'in progress' : ''}/>
              <TimingCell label="Duration"  value={fmtDuration(run.durationMs)} sub={isRunning ? 'so far' : ''}/>
            </div>
          )}

          <div style={{ marginTop: 10, fontSize: 11, fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)' }}>
            run · {run?.runId || '—'}
          </div>
        </div>

        {/* Scrollable body */}
        <div style={{ flex: 1, overflow: 'auto', paddingTop: 6 }}>
          {/* Agent proposal — only when failing AND agent layer enabled */}
          {agentLayer && job.agent && isFailed && (
            <div style={{ paddingTop: 14 }}>
              <AgentProposalBanner agent={job.agent} jobName={job.jobName}/>
            </div>
          )}

          {/* Outcome summary section */}
          <DrawerSection title="Outcome">
            {isFailed && run?.error && <ErrorBlock error={run.error}/>}
            {isSucc && run?.success && <SuccessBlock success={run.success}/>}
            {isSucc && !run?.success && (
              <div style={{ fontSize: 13, color: 'var(--text-secondary)', lineHeight: 1.55 }}>
                {job.lastOutcomeSummary || 'Run completed.'}
              </div>
            )}
            {isRunning && (
              <div style={{
                background: 'var(--brand-primary-10)',
                border: '1px solid var(--brand-primary-20)',
                borderRadius: 10, padding: '12px 14px',
                fontSize: 13, color: 'var(--text-primary)',
                display: 'flex', alignItems: 'center', gap: 10,
              }}>
                <span className="job-pulse"/>
                <span>{job.lastOutcomeSummary || 'Run in progress…'}</span>
              </div>
            )}
            {!run && (
              <div style={{ fontSize: 13, color: 'var(--text-secondary)', lineHeight: 1.55 }}>
                {job.lastOutcomeSummary || 'No run history available.'}
              </div>
            )}
          </DrawerSection>

          {/* Steps */}
          {run?.steps && (
            <DrawerSection title={`Steps · ${run.steps.length}`}>
              <div>
                {run.steps.map((s, i) => (
                  <StepRow key={i} step={s} idx={i} last={i === run.steps.length - 1}/>
                ))}
              </div>
            </DrawerSection>
          )}

          {/* Logs */}
          {run?.logs && (
            <DrawerSection title={`Run logs · ${run.logs.length} lines`} collapsible defaultOpen={false}
              action={<span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)' }}>stdout</span>}>
              <LogsBlock logs={run.logs}/>
            </DrawerSection>
          )}

          {/* Params */}
          {run?.params && (
            <DrawerSection title="Input parameters" collapsible defaultOpen={false}>
              <ParamsBlock params={run.params}/>
            </DrawerSection>
          )}

          <div style={{ height: 24 }}/>
        </div>

        {/* Footer */}
        <div style={{
          padding: '12px 24px',
          borderTop: '1px solid var(--border-light)',
          background: 'var(--surface-inset)',
          display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10,
        }}>
          <div style={{ display: 'flex', gap: 6 }}>
            {isFailed && (
              <button className="btn btn-primary btn-sm" onClick={() => onAction('retry', job)}>
                <Icon name="refresh" size={12}/> Retry run
              </button>
            )}
            {!isFailed && !isRunning && (
              <button className="btn btn-primary btn-sm" onClick={() => onAction('run', job)}>
                <Icon name="zap" size={12}/> Run now
              </button>
            )}
            {isFailed && (
              <button className="btn btn-outline btn-sm" onClick={() => onAction('resolve', job)}>
                Mark resolved
              </button>
            )}
          </div>
          <button className="btn btn-ghost btn-sm" onClick={() => onAction('history', job)}>
            View all runs <Icon name="arrowright" size={12}/>
          </button>
        </div>
      </aside>
    </>
  );
}

function TimingCell({ label, value, sub }) {
  return (
    <div style={{ background: 'var(--surface)', padding: '8px 12px' }}>
      <div style={{ fontSize: 10, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.06em', fontWeight: 600 }}>{label}</div>
      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 13, color: 'var(--text-primary)', fontWeight: 500, marginTop: 2 }}>{value}</div>
      {sub && <div style={{ fontSize: 10, color: 'var(--text-tertiary)', marginTop: 1 }}>{sub}</div>}
    </div>
  );
}

Object.assign(window, { RunDrawer });
