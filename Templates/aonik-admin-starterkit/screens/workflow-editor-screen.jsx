// Workflow editor — top-level screen.
// Composes the header, palette, canvas, inspector, and bottom drawers
// (live test, run trace, version history) into a single full-page surface.
//
// Owns workflow state: nodes, edges, selection, view, validation, dirty flag.

// Default starting workflow — "Match & apply" laid out left-to-right.
// Coords are world-space (multiples of 16 to fit on the dot grid).
const DEFAULT_WORKFLOW = {
  id: 'match_and_apply',
  name: 'Match & apply',
  desc: 'Reconcile invoice → bank txn, draft an entry, surface for review when over policy ceiling.',
  version: 'v1.4',
  ownerColor: '#eb5c37',
  nodes: [
    { id: 'n1', kind: 'trigger',  label: 'On bank txn',         x:  64, y: 240,
      summary: 'banking.transaction.received',
      params: { source: 'banking.transaction.received', filter: 'amount > 0' } },
    { id: 'n2', kind: 'tool',     label: 'Find candidate invoices', x: 320, y: 240,
      summary: 'search_invoices',
      params: { tool: 'search_invoices', params: '{ "amount_eps": 0.01 }' } },
    { id: 'n3', kind: 'agent',    label: 'Score match',         x: 576, y: 240,
      summary: 'Billing · confidence ≥ 0.85',
      params: { agent: 'Billing', task: 'Score candidate invoices and pick best match. Cite reasoning.' } },
    { id: 'n4', kind: 'decision', label: 'Above ceiling?',      x: 832, y: 240,
      summary: 'amount > 50000',
      params: { expr: 'amount > 50000', yesLabel: 'Yes', noLabel: 'No' } },
    { id: 'n5', kind: 'human',    label: 'Treasury approval',   x: 1088, y: 144,
      summary: 'group: Treasury · 4h SLA',
      params: { group: 'Treasury', sla: '4h' } },
    { id: 'n6', kind: 'tool',     label: 'Draft journal entry', x: 1088, y: 336,
      summary: 'AR · 1200',
      params: { tool: 'draft_journal_entry', params: '{ "account": "1200" }' } },
    { id: 'n7', kind: 'notify',   label: 'Send receipt',        x: 1344, y: 240,
      summary: 'email · receipt_v2',
      params: { channel: 'email', template: 'receipt_v2' } },
    { id: 'n8', kind: 'end',      label: 'Match applied',       x: 1600, y: 240, params: {} },
  ],
  edges: [
    { id: 'e1', from: 'n1', to: 'n2' },
    { id: 'e2', from: 'n2', to: 'n3' },
    { id: 'e3', from: 'n3', to: 'n4' },
    { id: 'e4', from: 'n4', to: 'n5', fromIdx: 0, label: 'yes' },
    { id: 'e5', from: 'n4', to: 'n6', fromIdx: 1, label: 'no' },
    { id: 'e6', from: 'n5', to: 'n6' },
    { id: 'e7', from: 'n6', to: 'n7' },
    { id: 'e8', from: 'n7', to: 'n8' },
  ],
};

// A pre-seeded comment, off to the side
const DEFAULT_COMMENTS = [
  { id: 'c1', x: 1024, y: 60, author: 'Maria · Treasury',
    body: 'Approval ceiling raised from £25K → £50K on 12 Apr per CFO memo.' },
];

// Synthetic recent runs for the trace overlay
const DEFAULT_RUNS = [
  { id: 'run_8421', when: '2m ago',  status: 'success', duration: '2.4s',  by: 'auto · banking.transaction.received',
    sequence: ['n1','n2','n3','n4','n6','n7','n8'], total: 7 },
  { id: 'run_8418', when: '14m ago', status: 'success', duration: '2.5s',  by: 'auto · banking.transaction.received',
    sequence: ['n1','n2','n3','n4','n6','n7','n8'], total: 7 },
  { id: 'run_8412', when: '38m ago', status: 'held',    duration: '7m 14s', by: 'held · over ceiling',
    sequence: ['n1','n2','n3','n4','n5'], total: 7 },
];

// Validation — derive errors from the graph state
function computeValidation(nodes, edges) {
  const errs = [];
  const inDeg = {}, outDeg = {};
  nodes.forEach(n => { inDeg[n.id] = 0; outDeg[n.id] = 0; });
  edges.forEach(e => { outDeg[e.from] = (outDeg[e.from] || 0) + 1; inDeg[e.to] = (inDeg[e.to] || 0) + 1; });

  nodes.forEach(n => {
    const k = NODE_KINDS[n.kind];
    if (k.inputs > 0 && inDeg[n.id] === 0 && n.kind !== 'trigger') {
      errs.push({ nodeId: n.id, message: `${n.label}: no incoming connection.` });
    }
    if (k.outputs > 0 && outDeg[n.id] === 0 && n.kind !== 'end') {
      errs.push({ nodeId: n.id, message: `${n.label}: dangling output — connect to the next step or an End node.` });
    }
    if (n.kind === 'decision' && outDeg[n.id] < 2) {
      errs.push({ nodeId: n.id, message: `${n.label}: decision needs both yes and no branches wired.` });
    }
  });
  if (!nodes.some(n => n.kind === 'trigger')) {
    errs.push({ nodeId: null, message: 'Workflow has no trigger node.' });
  }
  return errs;
}

function ScreenWorkflowEditor() {
  // ── Workflow state ──
  const [wf,    setWf]    = React.useState(DEFAULT_WORKFLOW);
  const [view,  setView]  = React.useState({ scale: 0.8, tx: 60, ty: 80 });
  const [selection, setSelection] = React.useState({ nodes: ['n3'], edges: [] });
  const [hoveredEdge, setHoveredEdge] = React.useState(null);
  const [comments] = React.useState(DEFAULT_COMMENTS);
  const [hasChanges, setHasChanges] = React.useState(false);

  // ── Panel toggles ──
  const [paletteCollapsed, setPaletteCollapsed] = React.useState(false);
  const [testOpen, setTestOpen] = React.useState(true);   // open by default in mock
  const [traceOpen, setTraceOpen] = React.useState(false);
  const [historyOpen, setHistoryOpen] = React.useState(false);

  // ── Trace state ──
  const [trace, setTrace] = React.useState(null);
  // trace shape: { runId, current: nodeId, completed: [nodeIds] }
  const startTrace = (runId = DEFAULT_RUNS[0].id, atStep = 1) => {
    const run = DEFAULT_RUNS.find(r => r.id === runId);
    if (!run) return;
    const completed = run.sequence.slice(0, atStep);
    const current   = run.sequence[atStep] || run.sequence[run.sequence.length - 1];
    setTrace({ runId, current, completed });
    setTraceOpen(true);
  };
  const stepTrace = (delta) => {
    if (!trace) return;
    const run = DEFAULT_RUNS.find(r => r.id === trace.runId);
    if (!run) return;
    const next = Math.max(0, Math.min(run.sequence.length, trace.completed.length + delta));
    setTrace({
      runId: trace.runId,
      current: run.sequence[next] || run.sequence[run.sequence.length - 1],
      completed: run.sequence.slice(0, next),
    });
  };
  React.useEffect(() => { if (!traceOpen) setTrace(null); }, [traceOpen]);

  // ── Mutations ──
  const dirty = () => setHasChanges(true);

  const onMoveNode = (id, x, y) => {
    setWf(w => ({ ...w, nodes: w.nodes.map(n => n.id === id ? { ...n, x, y } : n) }));
    dirty();
  };
  const onUpdateNode = (id, patch) => {
    setWf(w => ({ ...w, nodes: w.nodes.map(n => n.id === id ? { ...n, ...patch, params: { ...n.params, ...(patch.params || {}) } } : n) }));
    dirty();
  };
  const onDeleteNode = (id) => {
    setWf(w => ({
      ...w,
      nodes: w.nodes.filter(n => n.id !== id),
      edges: w.edges.filter(e => e.from !== id && e.to !== id),
    }));
    setSelection({ nodes: [], edges: [] });
    dirty();
  };
  const onAddEdge = ({ from, to, fromIdx }) => {
    // Prevent duplicate edge between same from/fromIdx → to
    const key = `${from}-${fromIdx || 0}-${to}`;
    setWf(w => {
      const exists = w.edges.some(e => `${e.from}-${e.fromIdx || 0}-${e.to}` === key);
      if (exists) return w;
      const newId = 'e' + Math.random().toString(36).slice(2, 7);
      // Auto-label decision/loop branches
      const fromNode = w.nodes.find(n => n.id === from);
      let label;
      if (fromNode?.kind === 'decision') label = (fromIdx || 0) === 0 ? fromNode.params.yesLabel : fromNode.params.noLabel;
      else if (fromNode?.kind === 'loop') label = (fromIdx || 0) === 0 ? 'body' : 'done';
      return { ...w, edges: [...w.edges, { id: newId, from, to, fromIdx: fromIdx || 0, label }] };
    });
    dirty();
  };
  const onDeleteEdge = (id) => {
    setWf(w => ({ ...w, edges: w.edges.filter(e => e.id !== id) }));
    setSelection({ nodes: [], edges: [] });
    dirty();
  };
  const onDropPaletteItem = (kind, x, y) => {
    const k = NODE_KINDS[kind];
    const id = 'n' + Math.random().toString(36).slice(2, 7);
    const node = {
      id, kind, x, y,
      label: k.label,
      summary: '',
      params: { ...k.defaults },
    };
    setWf(w => ({ ...w, nodes: [...w.nodes, node] }));
    setSelection({ nodes: [id], edges: [] });
    dirty();
  };

  // ── Validation ──
  const validationErrors = React.useMemo(() => computeValidation(wf.nodes, wf.edges), [wf.nodes, wf.edges]);

  // ── Versions (mock) ──
  const versions = [
    { id: 'v1.4', tag: 'v1.4', when: 'today',     by: 'Maria',  byColor: '#eb5c37',
      message: 'Raised approval ceiling £25K → £50K. Added Treasury approval branch.' },
    { id: 'v1.3', tag: 'v1.3', when: '8d ago',   by: 'Aonik',   byColor: '#055a60',
      message: 'Auto-link to receipt template after journal entry posted.' },
    { id: 'v1.2', tag: 'v1.2', when: '21d ago',  by: 'Rafa',    byColor: '#7b76b6',
      message: 'Switch matcher from regex to fuzzy + score.' },
    { id: 'v1.1', tag: 'v1.1', when: '2 mo ago', by: 'Aonik',   byColor: '#055a60',
      message: 'Initial draft auto-generated from playbook.' },
  ];

  // ── Keyboard: delete to remove selection ──
  React.useEffect(() => {
    const onKey = (e) => {
      if (e.key === 'Delete' || e.key === 'Backspace') {
        // Don't intercept when a form field has focus
        const tag = document.activeElement?.tagName;
        if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return;
        if (selection.nodes.length) { selection.nodes.forEach(onDeleteNode); }
        if (selection.edges.length) { selection.edges.forEach(onDeleteEdge); }
      }
      if (e.key === 'Escape') setSelection({ nodes: [], edges: [] });
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [selection]);

  return (
    <div style={{
      width: '100%', height: '100%',
      display: 'flex', flexDirection: 'column',
      background: 'var(--background)', overflow: 'hidden',
    }}>
      <EditorHeader
        workflow={wf}
        onClose={() => {}}
        hasChanges={hasChanges}
        onSave={() => setHasChanges(false)}
        onDiscard={() => { setWf(DEFAULT_WORKFLOW); setHasChanges(false); }}
        testOpen={testOpen} setTestOpen={setTestOpen}
        traceOpen={traceOpen} setTraceOpen={(v) => { setTraceOpen(v); if (v && !trace) startTrace(); }}
        historyOpen={historyOpen} setHistoryOpen={setHistoryOpen}
        validationErrors={validationErrors}/>

      {traceOpen && (
        <TraceBar
          trace={trace} runs={DEFAULT_RUNS}
          onPick={(id) => startTrace(id, 1)}
          onStep={stepTrace}
          onClose={() => setTraceOpen(false)}/>
      )}

      <div style={{ flex: 1, display: 'flex', minHeight: 0 }}>
        <EditorPalette collapsed={paletteCollapsed} setCollapsed={setPaletteCollapsed}/>

        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
          <WorkflowCanvas
            nodes={wf.nodes} edges={wf.edges}
            view={view} setView={setView}
            selection={selection} setSelection={setSelection}
            hoveredEdge={hoveredEdge} setHoveredEdge={setHoveredEdge}
            onMoveNode={onMoveNode}
            onAddEdge={onAddEdge}
            onDropPaletteItem={onDropPaletteItem}
            comments={comments}
            trace={trace}
            validationErrors={validationErrors}/>
          {testOpen && (
            <TestPanel workflow={wf}
              onClose={() => setTestOpen(false)}
              onStartRun={() => startTrace()}/>
          )}
        </div>

        <EditorInspector
          selection={selection}
          nodes={wf.nodes} edges={wf.edges}
          onUpdateNode={onUpdateNode}
          onDeleteNode={onDeleteNode}
          onDeleteEdge={onDeleteEdge}
          validationErrors={validationErrors}
          workflow={wf}/>

        {historyOpen && (
          <HistoryPanel versions={versions}
            onClose={() => setHistoryOpen(false)}
            onRestore={() => {}}/>
        )}
      </div>
    </div>
  );
}

Object.assign(window, { ScreenWorkflowEditor });
