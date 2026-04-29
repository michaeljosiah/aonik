// Workflow editor chrome — palette, inspector, header bar, bottom drawers
// (live test, run trace, version history). Pure presentational components
// driven by props from <ScreenWorkflowEditor/>.

// ─── Header bar ──────────────────────────────────────────────────
function EditorHeader({
  workflow, onClose,
  hasChanges, onSave, onDiscard,
  testOpen, setTestOpen, traceOpen, setTraceOpen, historyOpen, setHistoryOpen,
  validationErrors,
}) {
  return (
    <div style={{
      height: 52, flex: 'none',
      background: 'var(--surface)', borderBottom: '1px solid var(--border-light)',
      display: 'flex', alignItems: 'center', padding: '0 16px', gap: 12,
    }}>
      <button onClick={onClose} className="hover-halo" style={{
        padding: '6px 10px', borderRadius: 6,
        display: 'inline-flex', alignItems: 'center', gap: 6,
        fontSize: 12, color: 'var(--text-secondary)',
      }}>
        <Icon name="arrowleft" size={12}/> Back to Workflows
      </button>
      <span style={{ width: 1, height: 22, background: 'var(--border-light)' }}/>

      {/* Title block */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, minWidth: 0 }}>
        <span style={{
          width: 26, height: 26, borderRadius: 6, flex: 'none',
          background: workflow.ownerColor + '20', color: workflow.ownerColor,
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        }}>
          <Icon name="bolt" size={13}/>
        </span>
        <div style={{ minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>{workflow.name}</span>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>{workflow.id}</span>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10, color: 'var(--text-tertiary)', padding: '1px 6px', background: 'var(--surface-inset)', borderRadius: 3 }}>{workflow.version}</span>
            {hasChanges && (
              <span style={{ fontSize: 10, color: '#b4741e', padding: '1px 6px', background: '#b4741e18', borderRadius: 3, fontWeight: 500 }}>UNSAVED</span>
            )}
          </div>
        </div>
      </div>

      {/* Validation errors summary */}
      {validationErrors.length > 0 && (
        <div style={{
          marginLeft: 8,
          display: 'inline-flex', alignItems: 'center', gap: 6,
          fontSize: 11, padding: '3px 9px', borderRadius: 999,
          background: '#c4453618', color: '#c44536', fontWeight: 500,
        }}>
          <Icon name="alert" size={11}/> {validationErrors.length} issue{validationErrors.length === 1 ? '' : 's'}
        </div>
      )}

      <div style={{ flex: 1 }}/>

      {/* View toggles */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 2, padding: 2,
        background: 'var(--surface-inset)', borderRadius: 6,
      }}>
        {[
          { id: 'test',    label: 'Test',    icon: 'play',    open: testOpen,    set: setTestOpen },
          { id: 'trace',   label: 'Trace',   icon: 'eye',     open: traceOpen,   set: setTraceOpen },
          { id: 'history', label: 'History', icon: 'clock',   open: historyOpen, set: setHistoryOpen },
        ].map(b => (
          <button key={b.id} onClick={() => b.set(!b.open)}
            style={{
              padding: '5px 10px', borderRadius: 4,
              background: b.open ? 'var(--surface)' : 'transparent',
              border: 'none', cursor: 'pointer',
              fontSize: 11.5, fontWeight: 500,
              color: b.open ? 'var(--text-primary)' : 'var(--text-secondary)',
              display: 'inline-flex', alignItems: 'center', gap: 5,
              boxShadow: b.open ? '0 1px 2px rgba(0,0,0,0.06)' : 'none',
            }}>
            <Icon name={b.icon} size={11}/> {b.label}
          </button>
        ))}
      </div>

      {/* Actions */}
      <div style={{ display: 'flex', gap: 6 }}>
        {hasChanges && (
          <button onClick={onDiscard} className="btn btn-ghost btn-sm">Discard</button>
        )}
        <button className="btn btn-outline btn-sm"><Icon name="more" size={11}/></button>
        <button onClick={onSave} className="btn btn-primary btn-sm" disabled={!hasChanges && validationErrors.length === 0}>
          <Icon name="check" size={11}/> {hasChanges ? 'Save changes' : 'Saved'}
        </button>
      </div>
    </div>
  );
}

// ─── Left palette ────────────────────────────────────────────────
function EditorPalette({ collapsed, setCollapsed }) {
  const groups = [
    { name: 'Triggers',   kinds: ['trigger'] },
    { name: 'Actions',    kinds: ['tool', 'agent', 'notify', 'emit'] },
    { name: 'Logic',      kinds: ['decision', 'loop', 'wait'] },
    { name: 'Coordination', kinds: ['human', 'end'] },
  ];

  return (
    <div style={{
      width: collapsed ? 48 : 240, flex: 'none',
      background: 'var(--surface)', borderRight: '1px solid var(--border-light)',
      display: 'flex', flexDirection: 'column',
      transition: 'width .18s',
    }}>
      <div style={{
        padding: collapsed ? '12px 0' : '12px 14px',
        display: 'flex', alignItems: 'center',
        borderBottom: '1px solid var(--border-light)',
      }}>
        {!collapsed && (
          <span style={{ fontSize: 11, fontWeight: 600, color: 'var(--text-secondary)', letterSpacing: '0.06em', textTransform: 'uppercase', flex: 1 }}>
            Nodes
          </span>
        )}
        <button onClick={() => setCollapsed(!collapsed)} className="hover-halo"
          style={{ padding: 6, borderRadius: 4, margin: collapsed ? '0 auto' : 0 }}>
          <Icon name={collapsed ? 'chevronright' : 'chevronleft'} size={12} color="var(--text-tertiary)"/>
        </button>
      </div>

      {!collapsed && (
        <div style={{ flex: 1, overflowY: 'auto', padding: '8px 8px' }}>
          {groups.map(g => (
            <div key={g.name} style={{ marginBottom: 14 }}>
              <div style={{
                fontSize: 9.5, fontWeight: 600, color: 'var(--text-tertiary)',
                letterSpacing: '0.08em', textTransform: 'uppercase',
                padding: '6px 8px',
              }}>{g.name}</div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                {g.kinds.map(k => <PaletteItem key={k} kind={k}/>)}
              </div>
            </div>
          ))}
          <div style={{
            margin: '6px 8px 0', padding: 10,
            background: 'var(--brand-primary-10)', borderRadius: 6,
            fontSize: 11, color: 'var(--text-secondary)', lineHeight: 1.5,
            display: 'flex', gap: 8,
          }}>
            <Icon name="info" size={11} color="var(--brand-primary)"/>
            <span>Drag any node onto the canvas. Hold <b>Space</b> to pan.</span>
          </div>
        </div>
      )}
    </div>
  );
}

function PaletteItem({ kind }) {
  const k = NODE_KINDS[kind];
  const handleDragStart = (e) => {
    e.dataTransfer.setData('application/x-node-kind', kind);
    e.dataTransfer.effectAllowed = 'copy';
  };
  return (
    <div draggable onDragStart={handleDragStart}
      style={{
        display: 'flex', alignItems: 'center', gap: 10,
        padding: '7px 8px', borderRadius: 6,
        cursor: 'grab', userSelect: 'none',
        border: '1px solid transparent',
      }}
      onMouseEnter={(e) => { e.currentTarget.style.background = 'var(--surface-inset)'; e.currentTarget.style.borderColor = 'var(--border-light)'; }}
      onMouseLeave={(e) => { e.currentTarget.style.background = 'transparent'; e.currentTarget.style.borderColor = 'transparent'; }}>
      <span style={{
        width: 22, height: 22, borderRadius: 5, flex: 'none',
        background: k.tint, color: '#fff',
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
      }}>
        <Icon name={k.icon} size={11}/>
      </span>
      <div style={{ minWidth: 0, flex: 1 }}>
        <div style={{ fontSize: 12, fontWeight: 500, color: 'var(--text-primary)' }}>{k.label}</div>
        <div style={{ fontSize: 10, color: 'var(--text-tertiary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{k.desc}</div>
      </div>
      <Icon name="more" size={10} color="var(--text-tertiary)"/>
    </div>
  );
}

// ─── Right inspector ─────────────────────────────────────────────
function EditorInspector({ selection, nodes, edges, onUpdateNode, onDeleteNode, onDeleteEdge, validationErrors, workflow }) {
  // Single node selected → show node inspector
  // Single edge → edge inspector
  // Multi → bulk actions
  // None → workflow-level inspector
  if (selection.nodes.length === 1) {
    const node = nodes.find(n => n.id === selection.nodes[0]);
    if (!node) return <EmptyInspector/>;
    const errs = validationErrors.filter(v => v.nodeId === node.id);
    return <NodeInspector node={node} errors={errs} onUpdate={(p) => onUpdateNode(node.id, p)} onDelete={() => onDeleteNode(node.id)}/>;
  }
  if (selection.edges.length === 1) {
    const edge = edges.find(e => e.id === selection.edges[0]);
    if (!edge) return <EmptyInspector/>;
    return <EdgeInspector edge={edge} nodes={nodes} onDelete={() => onDeleteEdge(edge.id)}/>;
  }
  if (selection.nodes.length > 1) {
    return <MultiInspector count={selection.nodes.length} onDeleteAll={() => selection.nodes.forEach(onDeleteNode)}/>;
  }
  return <WorkflowInspector workflow={workflow} nodes={nodes} edges={edges} validationErrors={validationErrors}/>;
}

function InspectorShell({ title, eyebrow, kindTint, children }) {
  return (
    <div style={{
      width: 320, flex: 'none',
      background: 'var(--surface)', borderLeft: '1px solid var(--border-light)',
      display: 'flex', flexDirection: 'column', overflow: 'hidden',
    }}>
      <div style={{ padding: 16, borderBottom: '1px solid var(--border-light)' }}>
        {eyebrow && (
          <div style={{
            fontSize: 9.5, fontWeight: 600, color: kindTint || 'var(--text-tertiary)',
            letterSpacing: '0.08em', textTransform: 'uppercase', marginBottom: 4,
          }}>{eyebrow}</div>
        )}
        <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>{title}</div>
      </div>
      <div style={{ flex: 1, overflowY: 'auto', padding: 16, display: 'flex', flexDirection: 'column', gap: 16 }}>
        {children}
      </div>
    </div>
  );
}

function EmptyInspector() {
  return (
    <InspectorShell title="Nothing selected" eyebrow="Inspector">
      <div style={{ fontSize: 12, color: 'var(--text-secondary)', lineHeight: 1.5 }}>
        Select a node or an edge to edit its properties.
      </div>
    </InspectorShell>
  );
}

// Field components (low-level)
function FieldLabel({ children, hint }) {
  return (
    <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', display: 'flex', alignItems: 'center', gap: 6 }}>
      {children}
      {hint && <span style={{ fontWeight: 500, color: 'var(--text-tertiary)', textTransform: 'none', letterSpacing: 0 }}>· {hint}</span>}
    </div>
  );
}
function TextField({ label, value, onChange, mono, hint, placeholder }) {
  return (
    <div>
      <FieldLabel hint={hint}>{label}</FieldLabel>
      <input value={value || ''} onChange={e => onChange(e.target.value)} placeholder={placeholder}
        style={{
          width: '100%', boxSizing: 'border-box', marginTop: 6,
          fontFamily: mono ? 'var(--font-mono)' : 'inherit', fontSize: 12.5,
          padding: '8px 10px',
          background: 'var(--surface)', border: '1px solid var(--border-light)',
          borderBottom: '2px solid var(--border-light)', borderRadius: 6,
          color: 'var(--text-primary)',
        }}/>
    </div>
  );
}
function TextArea({ label, value, onChange, hint, rows = 3, mono }) {
  return (
    <div>
      <FieldLabel hint={hint}>{label}</FieldLabel>
      <textarea value={value || ''} onChange={e => onChange(e.target.value)} rows={rows}
        style={{
          width: '100%', boxSizing: 'border-box', marginTop: 6, resize: 'vertical',
          fontFamily: mono ? 'var(--font-mono)' : 'inherit', fontSize: 12,
          padding: '8px 10px', lineHeight: 1.5,
          background: 'var(--surface)', border: '1px solid var(--border-light)',
          borderRadius: 6, color: 'var(--text-primary)',
        }}/>
    </div>
  );
}
function Select({ label, value, onChange, options, hint }) {
  return (
    <div>
      <FieldLabel hint={hint}>{label}</FieldLabel>
      <select value={value} onChange={e => onChange(e.target.value)}
        style={{
          width: '100%', boxSizing: 'border-box', marginTop: 6,
          fontSize: 12.5, padding: '8px 10px',
          background: 'var(--surface)', border: '1px solid var(--border-light)',
          borderRadius: 6, color: 'var(--text-primary)',
        }}>
        {options.map(o => <option key={o.value || o} value={o.value || o}>{o.label || o}</option>)}
      </select>
    </div>
  );
}

// Node inspector — kind-specific param fields
function NodeInspector({ node, errors, onUpdate, onDelete }) {
  const k = NODE_KINDS[node.kind];

  const updateParam = (key, val) => onUpdate({ params: { ...node.params, [key]: val } });

  return (
    <InspectorShell title={node.label} eyebrow={k.label} kindTint={k.tint}>
      {/* Validation errors */}
      {errors.length > 0 && (
        <div style={{
          padding: '10px 12px',
          background: '#c4453610', border: '1px solid #c4453640', borderRadius: 6,
          display: 'flex', flexDirection: 'column', gap: 6,
        }}>
          {errors.map((e, i) => (
            <div key={i} style={{ fontSize: 11.5, color: '#a3392b', display: 'flex', gap: 6, alignItems: 'flex-start', lineHeight: 1.5 }}>
              <Icon name="alert" size={11}/> <span>{e.message}</span>
            </div>
          ))}
        </div>
      )}

      {/* Common fields */}
      <TextField label="Name" value={node.label} onChange={v => onUpdate({ label: v })}/>
      <TextArea label="Notes" hint="optional" value={node.notes} onChange={v => onUpdate({ notes: v })} rows={2}/>

      {/* Kind-specific */}
      {node.kind === 'trigger' && (
        <>
          <TextField label="Source" mono value={node.params.source} onChange={v => updateParam('source', v)}/>
          <TextField label="Filter" mono hint="optional" value={node.params.filter} onChange={v => updateParam('filter', v)} placeholder="amount > 0"/>
        </>
      )}
      {node.kind === 'tool' && (
        <>
          <Select label="Tool" value={node.params.tool} onChange={v => updateParam('tool', v)}
            options={['search_invoices','list_bank_transactions','match_invoice_to_txn','apply_match','draft_journal_entry','send_email','fetch_fx_fix','draft_forward_contract','screen_counterparty','lock_period','aggregate_spend','lookup_customer']}/>
          <TextArea label="Parameters" mono hint="JSON" value={node.params.params} onChange={v => updateParam('params', v)} rows={4}/>
        </>
      )}
      {node.kind === 'agent' && (
        <>
          <Select label="Agent" value={node.params.agent} onChange={v => updateParam('agent', v)}
            options={['Billing','Ledger','FX','Compliance','Close','Dunning','Insights','Orchestrator']}/>
          <TextArea label="Task brief" value={node.params.task} onChange={v => updateParam('task', v)} rows={3}/>
        </>
      )}
      {node.kind === 'decision' && (
        <>
          <TextField label="Condition" mono value={node.params.expr} onChange={v => updateParam('expr', v)}/>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
            <TextField label="Yes label" value={node.params.yesLabel} onChange={v => updateParam('yesLabel', v)}/>
            <TextField label="No label"  value={node.params.noLabel}  onChange={v => updateParam('noLabel', v)}/>
          </div>
        </>
      )}
      {node.kind === 'human' && (
        <>
          <Select label="Approval group" value={node.params.group} onChange={v => updateParam('group', v)}
            options={['Treasury','Finance','Compliance','Anyone']}/>
          <TextField label="SLA" hint="time before escalation" value={node.params.sla} onChange={v => updateParam('sla', v)}/>
        </>
      )}
      {node.kind === 'wait' && (
        <TextField label="Duration" mono hint="e.g. 7d, 4h, 30m" value={node.params.duration} onChange={v => updateParam('duration', v)}/>
      )}
      {node.kind === 'notify' && (
        <>
          <Select label="Channel" value={node.params.channel} onChange={v => updateParam('channel', v)}
            options={['email','sms','slack','push']}/>
          <TextField label="Template" mono value={node.params.template} onChange={v => updateParam('template', v)}/>
        </>
      )}
      {node.kind === 'emit' && (
        <TextField label="Event name" mono value={node.params.event} onChange={v => updateParam('event', v)}/>
      )}
      {node.kind === 'loop' && (
        <>
          <TextField label="Iterate over" mono value={node.params.over} onChange={v => updateParam('over', v)}/>
          <TextField label="Max iterations" mono value={node.params.maxIterations} onChange={v => updateParam('maxIterations', v)}/>
        </>
      )}

      {/* Footer: connections + delete */}
      <div style={{ marginTop: 'auto', paddingTop: 12, borderTop: '1px solid var(--border-light)', display: 'flex', flexDirection: 'column', gap: 10 }}>
        <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase' }}>Node ID</div>
        <div style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)' }}>{node.id}</div>
        <button onClick={onDelete} className="btn btn-outline btn-sm"
          style={{ color: '#c44536', borderColor: '#c4453640' }}>
          <Icon name="trash" size={11}/> Delete node
        </button>
      </div>
    </InspectorShell>
  );
}

function EdgeInspector({ edge, nodes, onDelete }) {
  const a = nodes.find(n => n.id === edge.from);
  const b = nodes.find(n => n.id === edge.to);
  return (
    <InspectorShell title="Connection" eyebrow="Edge">
      <div style={{ padding: 12, background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 6 }}>
        <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', fontWeight: 600 }}>From</div>
        <div style={{ fontSize: 12.5, fontWeight: 500, color: 'var(--text-primary)', marginTop: 2 }}>{a?.label}</div>
        <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', letterSpacing: '0.06em', textTransform: 'uppercase', fontWeight: 600, marginTop: 10 }}>To</div>
        <div style={{ fontSize: 12.5, fontWeight: 500, color: 'var(--text-primary)', marginTop: 2 }}>{b?.label}</div>
      </div>
      {edge.label && (
        <div>
          <FieldLabel>Label</FieldLabel>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-primary)', marginTop: 6 }}>{edge.label}</div>
        </div>
      )}
      <button onClick={onDelete} className="btn btn-outline btn-sm" style={{ color: '#c44536', borderColor: '#c4453640' }}>
        <Icon name="trash" size={11}/> Remove connection
      </button>
    </InspectorShell>
  );
}

function MultiInspector({ count, onDeleteAll }) {
  return (
    <InspectorShell title={`${count} nodes selected`} eyebrow="Multi-select">
      <div style={{ fontSize: 12, color: 'var(--text-secondary)', lineHeight: 1.5 }}>
        Drag any node to move them together. Or run a bulk action below.
      </div>
      <button className="btn btn-outline btn-sm">
        <Icon name="copy" size={11}/> Duplicate
      </button>
      <button className="btn btn-outline btn-sm">
        <Icon name="package" size={11}/> Group as sub-flow
      </button>
      <button onClick={onDeleteAll} className="btn btn-outline btn-sm" style={{ color: '#c44536', borderColor: '#c4453640' }}>
        <Icon name="trash" size={11}/> Delete {count} nodes
      </button>
    </InspectorShell>
  );
}

function WorkflowInspector({ workflow, nodes, edges, validationErrors }) {
  const counts = nodes.reduce((acc, n) => { acc[n.kind] = (acc[n.kind] || 0) + 1; return acc; }, {});
  return (
    <InspectorShell title={workflow.name} eyebrow="Workflow">
      <TextField label="Name" value={workflow.name} onChange={() => {}}/>
      <TextArea label="Description" value={workflow.desc} onChange={() => {}} rows={3}/>

      <div>
        <FieldLabel>Composition</FieldLabel>
        <div style={{ marginTop: 8, display: 'flex', flexDirection: 'column', gap: 4 }}>
          <div style={{ fontSize: 11.5, color: 'var(--text-secondary)' }}>
            <span style={{ fontFamily: 'var(--font-mono)' }}>{nodes.length}</span> node{nodes.length === 1 ? '' : 's'} ·{' '}
            <span style={{ fontFamily: 'var(--font-mono)' }}>{edges.length}</span> connection{edges.length === 1 ? '' : 's'}
          </div>
          {Object.entries(counts).sort().map(([k, c]) => (
            <div key={k} style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 11.5, color: 'var(--text-secondary)' }}>
              <span style={{ width: 10, height: 10, borderRadius: 2, background: NODE_KINDS[k].tint, opacity: 0.8 }}/>
              <span style={{ flex: 1 }}>{NODE_KINDS[k].label}</span>
              <span style={{ fontFamily: 'var(--font-mono)' }}>{c}</span>
            </div>
          ))}
        </div>
      </div>

      {validationErrors.length > 0 && (
        <div style={{ background: '#c4453610', border: '1px solid #c4453640', borderRadius: 6, padding: 12 }}>
          <div style={{ fontSize: 10.5, fontWeight: 600, color: '#c44536', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 6 }}>
            {validationErrors.length} issue{validationErrors.length === 1 ? '' : 's'}
          </div>
          {validationErrors.slice(0, 5).map((e, i) => (
            <div key={i} style={{ fontSize: 11.5, color: '#a3392b', lineHeight: 1.5, marginTop: 4 }}>· {e.message}</div>
          ))}
        </div>
      )}
    </InspectorShell>
  );
}

// ─── Bottom panels ───────────────────────────────────────────────
// Live test panel — input form, "Run test" button, output stream
function TestPanel({ workflow, onClose, onStartRun }) {
  const [input, setInput] = React.useState(JSON.stringify({
    txn_id: 'tx_9f2c1a',
    amount: 12480.00,
    currency: 'GBP',
    counterparty: 'Primrose Logistics',
    memo: 'INV-2041',
  }, null, 2));
  const [running, setRunning] = React.useState(false);
  const [logs, setLogs] = React.useState([
    { t: 'idle', msg: 'Ready. Edit input and run a test trace through the canvas.' },
  ]);

  const run = () => {
    setRunning(true);
    setLogs([{ t: 'info', msg: 'Starting test run · ' + new Date().toLocaleTimeString() }]);
    onStartRun();
    // Fake stream
    let i = 0;
    const stages = [
      { t: 'tool',    msg: 'tool · search_invoices · 142ms · 1 match found' },
      { t: 'agent',   msg: 'agent · Billing · scoring confidence 0.94' },
      { t: 'decision',msg: 'decision · amount > 50000? → no · proceeding' },
      { t: 'ledger',  msg: 'tool · draft_journal_entry · DR 1200 / CR 4000' },
      { t: 'notify',  msg: 'notify · email · receipt sent to ar@primrose.io' },
      { t: 'ok',      msg: 'Run complete · 2.4s · success' },
    ];
    const tick = () => {
      if (i >= stages.length) { setRunning(false); return; }
      setLogs(l => [...l, stages[i]]);
      i++;
      setTimeout(tick, 600);
    };
    setTimeout(tick, 400);
  };

  return (
    <div style={{
      height: 280, flex: 'none',
      background: 'var(--surface)', borderTop: '1px solid var(--border-light)',
      display: 'flex', overflow: 'hidden',
    }}>
      {/* Left: input */}
      <div style={{ width: 360, flex: 'none', borderRight: '1px solid var(--border-light)', padding: 14, display: 'flex', flexDirection: 'column' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 10 }}>
          <Icon name="play" size={12} color="var(--brand-primary)"/>
          <span style={{ fontSize: 11.5, fontWeight: 600, color: 'var(--text-primary)' }}>Test input</span>
          <div style={{ flex: 1 }}/>
          <Select label="" value="banking.transaction.received" onChange={() => {}}
            options={['banking.transaction.received','invoice.overdue','manual']}/>
        </div>
        <textarea value={input} onChange={e => setInput(e.target.value)}
          style={{
            flex: 1, fontFamily: 'var(--font-mono)', fontSize: 11.5,
            padding: 10, lineHeight: 1.5, resize: 'none',
            background: 'var(--surface-inset)', border: '1px solid var(--border-light)',
            borderRadius: 6, color: 'var(--text-primary)',
          }}/>
        <button onClick={run} disabled={running} className="btn btn-primary btn-sm" style={{ marginTop: 10, justifyContent: 'center' }}>
          {running ? <><Icon name="refresh" size={11}/> Running…</> : <><Icon name="play" size={11}/> Run test</>}
        </button>
      </div>

      {/* Right: log stream */}
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
        <div style={{
          padding: '10px 14px', borderBottom: '1px solid var(--border-light)',
          display: 'flex', alignItems: 'center', gap: 8,
        }}>
          <span style={{ fontSize: 11.5, fontWeight: 600, color: 'var(--text-primary)' }}>Run output</span>
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)' }}>{logs.length} events</span>
          <div style={{ flex: 1 }}/>
          <button onClick={() => setLogs([])} className="btn btn-ghost btn-sm"><Icon name="trash" size={11}/> Clear</button>
          <button onClick={onClose} className="btn btn-ghost btn-sm"><Icon name="close" size={11}/></button>
        </div>
        <div style={{ flex: 1, overflowY: 'auto', padding: 12, background: 'var(--surface-inset)' }}>
          {logs.map((l, i) => (
            <div key={i} style={{
              fontFamily: 'var(--font-mono)', fontSize: 11.5, lineHeight: 1.6,
              color: l.t === 'ok' ? 'var(--success, #1f7a5e)' : l.t === 'idle' ? 'var(--text-tertiary)' : 'var(--text-primary)',
              display: 'flex', gap: 10, alignItems: 'flex-start',
            }}>
              <span style={{ color: 'var(--text-tertiary)', flex: 'none' }}>{String(i).padStart(2, '0')}</span>
              <span>{l.msg}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

// History sidebar — versions list
function HistoryPanel({ versions, onClose, onRestore }) {
  return (
    <div style={{
      width: 280, flex: 'none',
      background: 'var(--surface)', borderLeft: '1px solid var(--border-light)',
      display: 'flex', flexDirection: 'column', overflow: 'hidden',
    }}>
      <div style={{
        padding: '12px 14px', borderBottom: '1px solid var(--border-light)',
        display: 'flex', alignItems: 'center', gap: 8,
      }}>
        <Icon name="clock" size={12} color="var(--text-secondary)"/>
        <span style={{ fontSize: 11.5, fontWeight: 600, color: 'var(--text-primary)' }}>Version history</span>
        <div style={{ flex: 1 }}/>
        <button onClick={onClose} className="hover-halo" style={{ padding: 4 }}>
          <Icon name="close" size={11} color="var(--text-tertiary)"/>
        </button>
      </div>
      <div style={{ flex: 1, overflowY: 'auto', padding: 8 }}>
        {versions.map((v, i) => (
          <div key={v.id} style={{
            padding: '10px 12px', cursor: 'pointer',
            background: i === 0 ? 'var(--brand-primary-10)' : 'transparent',
            border: '1px solid ' + (i === 0 ? 'var(--brand-primary)' : 'transparent'),
            borderRadius: 6, marginBottom: 4,
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, fontWeight: 600, color: i === 0 ? 'var(--brand-primary)' : 'var(--text-primary)' }}>{v.tag}</span>
              {i === 0 && <Pill tone="brand" size="sm">current</Pill>}
              <div style={{ flex: 1 }}/>
              <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>{v.when}</span>
            </div>
            <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 4, lineHeight: 1.45 }}>{v.message}</div>
            <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 4, display: 'flex', alignItems: 'center', gap: 6 }}>
              <Avatar name={v.by[0]} size={14} color={v.byColor || '#9aa3ad'} textColor="#fff"/>
              {v.by}
            </div>
            {i !== 0 && (
              <button onClick={() => onRestore(v.id)} className="btn btn-ghost btn-sm" style={{ marginTop: 6, padding: '3px 8px', fontSize: 10.5 }}>
                <Icon name="refresh" size={10}/> Restore
              </button>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

// Trace overlay control bar (sits below header when trace open)
function TraceBar({ trace, runs, onPick, onStep, onClose }) {
  const run = runs.find(r => r.id === trace?.runId) || runs[0];
  return (
    <div style={{
      flex: 'none', background: 'var(--surface-inset)',
      borderBottom: '1px solid var(--border-light)',
      padding: '8px 14px',
      display: 'flex', alignItems: 'center', gap: 12,
    }}>
      <div style={{
        display: 'inline-flex', alignItems: 'center', gap: 6,
        padding: '3px 9px', borderRadius: 999,
        background: '#3ab79518', color: '#1f7a5e', fontWeight: 500, fontSize: 11,
      }}>
        <span style={{ width: 6, height: 6, borderRadius: 999, background: '#3ab795', animation: 'pulse 1.6s infinite' }}/>
        Replaying
      </div>
      <select value={trace?.runId || ''} onChange={(e) => onPick(e.target.value)}
        style={{ fontSize: 12, padding: '4px 8px', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 6 }}>
        {runs.map(r => <option key={r.id} value={r.id}>{r.id} · {r.when} · {r.status}</option>)}
      </select>
      <span style={{ fontSize: 11.5, color: 'var(--text-secondary)' }}>
        Step <span style={{ fontFamily: 'var(--font-mono)' }}>{(trace?.completed.length || 0) + 1}</span> of <span style={{ fontFamily: 'var(--font-mono)' }}>{run?.total || 0}</span>
      </span>
      <button onClick={() => onStep(-1)} className="btn btn-ghost btn-sm"><Icon name="chevronleft" size={11}/></button>
      <button onClick={() => onStep(1)} className="btn btn-ghost btn-sm">Next <Icon name="chevronright" size={11}/></button>
      <div style={{ flex: 1 }}/>
      <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{run?.duration} · started by {run?.by}</span>
      <button onClick={onClose} className="hover-halo" style={{ padding: 4 }}>
        <Icon name="close" size={11} color="var(--text-tertiary)"/>
      </button>
    </div>
  );
}

Object.assign(window, {
  EditorHeader, EditorPalette, EditorInspector,
  TestPanel, HistoryPanel, TraceBar,
});
