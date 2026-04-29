// Workflow editor — full-page canvas with drag, pan, zoom, port-wiring,
// marquee select, snap-to-grid, palette drop, inline validation, run-trace
// overlay, live test panel, comments, version history.
//
// Visual language: clean n8n / Linear-style. Soft grid, low-contrast nodes,
// tinted accent per node-kind, beziers for edges.
//
// File split:
//   workflow-editor.jsx  — this file: canvas + nodes + edges + interactions
//   workflow-editor-chrome.jsx — header bar, palette, inspector, bottom drawers
//   workflow-editor-screen.jsx — top-level <ScreenWorkflowEditor/> that wires
//                                state and renders both halves
//
// All state lives in the screen component and is threaded down via props.
// Keeping it lifted means the chrome can edit selections + the canvas can
// render trace overlays without us reaching into refs.

// ─── Palette / node-kind catalog ─────────────────────────────────
// Each kind has: visual tint, icon, default I/O, default param shape.
// Used by the canvas (rendering), the palette (drag source), and the
// inspector (which params to show).
const NODE_KINDS = {
  trigger: {
    label: 'Trigger', tint: '#055a60', icon: 'bolt',
    desc: 'Where the workflow starts',
    inputs: 0, outputs: 1,
    defaults: { source: 'banking.transaction.received', filter: '' },
  },
  tool: {
    label: 'Tool call', tint: '#0097a9', icon: 'wrench',
    desc: 'Invoke a registered tool',
    inputs: 1, outputs: 1,
    defaults: { tool: 'search_invoices', params: '{}' },
  },
  agent: {
    label: 'Sub-agent', tint: '#7b76b6', icon: 'sparkles',
    desc: 'Hand off to another agent',
    inputs: 1, outputs: 1,
    defaults: { agent: 'Billing', task: 'Score match candidates' },
  },
  decision: {
    label: 'Decision', tint: '#b4741e', icon: 'gitfork',
    desc: 'Branch on a condition',
    inputs: 1, outputs: 2, // yes / no
    defaults: { expr: 'amount > 50000', yesLabel: 'Yes', noLabel: 'No' },
  },
  human: {
    label: 'Human approval', tint: '#c44536', icon: 'users',
    desc: 'Pause for a person to decide',
    inputs: 1, outputs: 1,
    defaults: { group: 'Treasury', sla: '4h' },
  },
  wait: {
    label: 'Wait', tint: '#5facbd', icon: 'clock',
    desc: 'Delay for a fixed duration',
    inputs: 1, outputs: 1,
    defaults: { duration: '7d' },
  },
  notify: {
    label: 'Notify', tint: '#3ab795', icon: 'send',
    desc: 'Email, SMS, or Slack message',
    inputs: 1, outputs: 1,
    defaults: { channel: 'email', template: 'receipt_v2' },
  },
  emit: {
    label: 'Emit event', tint: '#d4a843', icon: 'bolt',
    desc: 'Fire an event back into the bus',
    inputs: 1, outputs: 1,
    defaults: { event: 'workflow.completed' },
  },
  loop: {
    label: 'Loop', tint: '#a35dac', icon: 'refresh',
    desc: 'Iterate over a collection',
    inputs: 1, outputs: 2, // body / done
    defaults: { over: 'invoices', maxIterations: 100 },
  },
  end: {
    label: 'End', tint: '#1f7a5e', icon: 'check',
    desc: 'Workflow completes',
    inputs: 1, outputs: 0,
    defaults: {},
  },
};

// ─── Geometry constants ──────────────────────────────────────────
const NODE_W = 220;
const NODE_H = 76;
const PORT_R = 6;
const GRID = 16;          // canvas grid spacing
const SNAP = 8;           // snap-to grid (half a grid cell)
const HEADER_H = 30;      // node header band height

// Reusable: snap a coord to grid
const snap = (v) => Math.round(v / SNAP) * SNAP;

// Port positions (relative to node x,y)
const inPortPos = (n)  => ({ x: n.x,           y: n.y + NODE_H / 2 });
const outPortPos = (n, idx = 0, total = 1) => {
  if (total <= 1) return { x: n.x + NODE_W, y: n.y + NODE_H / 2 };
  // Decision/loop: 2 outputs split vertically
  const offset = (idx === 0 ? -1 : 1) * (NODE_H / 4);
  return { x: n.x + NODE_W, y: n.y + NODE_H / 2 + offset };
};

// Bezier path between two points (smooth horizontal)
function bezierPath(x1, y1, x2, y2) {
  const dx = Math.max(40, Math.abs(x2 - x1) * 0.4);
  return `M ${x1} ${y1} C ${x1 + dx} ${y1}, ${x2 - dx} ${y2}, ${x2} ${y2}`;
}

// ─── Canvas ──────────────────────────────────────────────────────
// Receives nodes + edges + view state; renders the SVG diagram and
// translates user input into onChange callbacks.
function WorkflowCanvas({
  nodes, edges, view, setView,
  selection, setSelection,
  hoveredEdge, setHoveredEdge,
  onMoveNode, onAddEdge, onDropPaletteItem,
  trace,           // null or { runId, current: nodeId, completed: [nodeIds] }
  comments = [],
  validationErrors = [],
  showGrid = true,
  showMinimap = true,
}) {
  const svgRef = React.useRef(null);
  const [drag, setDrag] = React.useState(null);
  // drag shape:
  //   { kind: 'pan',     start: {x,y}, view0 }
  //   { kind: 'node',    ids: [], start: {x,y}, origins: {[id]: {x,y}} }
  //   { kind: 'wire',    fromNode, fromIdx, fromPt: {x,y}, to: {x,y} }
  //   { kind: 'marquee', start: {x,y}, end: {x,y} }

  // Convert client coords → world coords (account for pan + zoom)
  const clientToWorld = (cx, cy) => {
    const r = svgRef.current.getBoundingClientRect();
    return {
      x: (cx - r.left - view.tx) / view.scale,
      y: (cy - r.top  - view.ty) / view.scale,
    };
  };

  // Wheel zoom (centered on cursor)
  const handleWheel = (e) => {
    e.preventDefault();
    const r = svgRef.current.getBoundingClientRect();
    const mx = e.clientX - r.left, my = e.clientY - r.top;
    const delta = -e.deltaY * 0.0018;
    const nextScale = Math.max(0.35, Math.min(2.0, view.scale * (1 + delta)));
    // Anchor zoom on cursor: keep world point under cursor steady
    const wx = (mx - view.tx) / view.scale;
    const wy = (my - view.ty) / view.scale;
    setView({ ...view, scale: nextScale, tx: mx - wx * nextScale, ty: my - wy * nextScale });
  };

  // Pointer down on canvas background → pan or marquee
  const handleBgDown = (e) => {
    if (e.button !== 0) return;
    const isSpace = window.__editorSpaceDown;
    if (isSpace || e.button === 1) {
      setDrag({ kind: 'pan', start: { x: e.clientX, y: e.clientY }, view0: view });
    } else {
      const w = clientToWorld(e.clientX, e.clientY);
      setDrag({ kind: 'marquee', start: w, end: w });
      if (!e.shiftKey) setSelection({ nodes: [], edges: [] });
    }
  };

  // Pointer down on a node → select + start drag
  const handleNodeDown = (e, node) => {
    e.stopPropagation();
    let nextSel = selection;
    if (e.shiftKey) {
      const has = selection.nodes.includes(node.id);
      nextSel = { nodes: has ? selection.nodes.filter(i => i !== node.id) : [...selection.nodes, node.id], edges: [] };
    } else if (!selection.nodes.includes(node.id)) {
      nextSel = { nodes: [node.id], edges: [] };
    }
    setSelection(nextSel);

    const ids = nextSel.nodes.length ? nextSel.nodes : [node.id];
    const origins = {};
    ids.forEach(id => { const n = nodes.find(x => x.id === id); if (n) origins[id] = { x: n.x, y: n.y }; });
    setDrag({ kind: 'node', ids, start: { x: e.clientX, y: e.clientY }, origins });
  };

  // Pointer down on an output port → start a wire
  const handleOutPortDown = (e, node, outIdx) => {
    e.stopPropagation();
    const total = NODE_KINDS[node.kind].outputs;
    const fromPt = outPortPos(node, outIdx, total);
    const w = clientToWorld(e.clientX, e.clientY);
    setDrag({ kind: 'wire', fromNode: node.id, fromIdx: outIdx, fromPt, to: w });
  };

  // Pointer up on an input port → finalise wire
  const handleInPortUp = (e, node) => {
    if (drag && drag.kind === 'wire' && drag.fromNode !== node.id) {
      onAddEdge({ from: drag.fromNode, fromIdx: drag.fromIdx, to: node.id });
    }
    setDrag(null);
    e.stopPropagation();
  };

  // Pointer move + up — global, on the SVG
  const handleMove = (e) => {
    if (!drag) return;
    if (drag.kind === 'pan') {
      const dx = e.clientX - drag.start.x;
      const dy = e.clientY - drag.start.y;
      setView({ ...drag.view0, tx: drag.view0.tx + dx, ty: drag.view0.ty + dy });
    } else if (drag.kind === 'node') {
      const dx = (e.clientX - drag.start.x) / view.scale;
      const dy = (e.clientY - drag.start.y) / view.scale;
      drag.ids.forEach(id => {
        const o = drag.origins[id];
        onMoveNode(id, snap(o.x + dx), snap(o.y + dy));
      });
    } else if (drag.kind === 'wire') {
      setDrag({ ...drag, to: clientToWorld(e.clientX, e.clientY) });
    } else if (drag.kind === 'marquee') {
      setDrag({ ...drag, end: clientToWorld(e.clientX, e.clientY) });
    }
  };
  const handleUp = () => {
    if (drag && drag.kind === 'marquee') {
      const x0 = Math.min(drag.start.x, drag.end.x), x1 = Math.max(drag.start.x, drag.end.x);
      const y0 = Math.min(drag.start.y, drag.end.y), y1 = Math.max(drag.start.y, drag.end.y);
      const hits = nodes.filter(n =>
        n.x + NODE_W >= x0 && n.x <= x1 && n.y + NODE_H >= y0 && n.y <= y1
      ).map(n => n.id);
      if (hits.length) setSelection({ nodes: hits, edges: [] });
    }
    setDrag(null);
  };

  // Track Space key for pan-mode (n8n-style)
  React.useEffect(() => {
    const dn = (e) => { if (e.code === 'Space') window.__editorSpaceDown = true; };
    const up = (e) => { if (e.code === 'Space') window.__editorSpaceDown = false; };
    window.addEventListener('keydown', dn);
    window.addEventListener('keyup',   up);
    return () => { window.removeEventListener('keydown', dn); window.removeEventListener('keyup', up); };
  }, []);

  // Drop from palette
  const handleDragOver = (e) => { e.preventDefault(); e.dataTransfer.dropEffect = 'copy'; };
  const handleDrop = (e) => {
    e.preventDefault();
    const kind = e.dataTransfer.getData('application/x-node-kind');
    if (!kind) return;
    const w = clientToWorld(e.clientX, e.clientY);
    onDropPaletteItem(kind, snap(w.x - NODE_W / 2), snap(w.y - NODE_H / 2));
  };

  // ── Render ──
  return (
    <div style={{
      position: 'relative', flex: 1, minWidth: 0, height: '100%',
      background: 'var(--surface-inset)', overflow: 'hidden',
      cursor: drag?.kind === 'pan' ? 'grabbing' : (window.__editorSpaceDown ? 'grab' : 'default'),
    }} onDragOver={handleDragOver} onDrop={handleDrop}>
      <svg
        ref={svgRef}
        width="100%" height="100%"
        onMouseDown={handleBgDown}
        onMouseMove={handleMove}
        onMouseUp={handleUp}
        onMouseLeave={handleUp}
        onWheel={handleWheel}
        style={{ display: 'block', userSelect: 'none' }}
      >
        <defs>
          {/* dot grid pattern */}
          <pattern id="dotgrid" x="0" y="0" width={GRID * 2} height={GRID * 2} patternUnits="userSpaceOnUse">
            <circle cx="1" cy="1" r="1" fill="rgba(0,0,0,0.07)"/>
          </pattern>
          {/* arrowhead marker for edges */}
          <marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5"
            markerWidth="7" markerHeight="7" orient="auto-start-reverse">
            <path d="M 0 0 L 10 5 L 0 10 z" fill="#9aa3ad"/>
          </marker>
          <marker id="arrow-active" viewBox="0 0 10 10" refX="9" refY="5"
            markerWidth="7" markerHeight="7" orient="auto-start-reverse">
            <path d="M 0 0 L 10 5 L 0 10 z" fill="#055a60"/>
          </marker>
          <marker id="arrow-trace" viewBox="0 0 10 10" refX="9" refY="5"
            markerWidth="7" markerHeight="7" orient="auto-start-reverse">
            <path d="M 0 0 L 10 5 L 0 10 z" fill="#3ab795"/>
          </marker>
        </defs>

        {/* Grid (in screen space, but offset by pan so it tracks) */}
        {showGrid && (
          <rect
            x={view.tx % (GRID * 2 * view.scale) - GRID * 2 * view.scale}
            y={view.ty % (GRID * 2 * view.scale) - GRID * 2 * view.scale}
            width="200%" height="200%"
            fill="url(#dotgrid)"
          />
        )}

        {/* World group — applies pan + zoom */}
        <g transform={`translate(${view.tx} ${view.ty}) scale(${view.scale})`}>
          {/* Edges */}
          {edges.map(e => {
            const a = nodes.find(n => n.id === e.from);
            const b = nodes.find(n => n.id === e.to);
            if (!a || !b) return null;
            const total = NODE_KINDS[a.kind].outputs;
            const p1 = outPortPos(a, e.fromIdx || 0, total);
            const p2 = inPortPos(b);
            const isSel = selection.edges.includes(e.id);
            const isHover = hoveredEdge === e.id;
            const isTraced = trace && (trace.completed.includes(e.from) && (trace.completed.includes(e.to) || trace.current === e.to));
            const stroke = isTraced ? '#3ab795' : isSel ? 'var(--brand-primary)' : isHover ? '#055a60' : '#9aa3ad';
            const sw = isSel || isTraced ? 2.5 : isHover ? 2 : 1.5;
            return (
              <g key={e.id}>
                {/* fat invisible hit path */}
                <path d={bezierPath(p1.x, p1.y, p2.x, p2.y)}
                  stroke="transparent" strokeWidth="14" fill="none"
                  onMouseEnter={() => setHoveredEdge(e.id)}
                  onMouseLeave={() => setHoveredEdge(null)}
                  onMouseDown={(ev) => { ev.stopPropagation(); setSelection({ nodes: [], edges: [e.id] }); }}
                  style={{ cursor: 'pointer' }}/>
                <path d={bezierPath(p1.x, p1.y, p2.x, p2.y)}
                  stroke={stroke} strokeWidth={sw} fill="none"
                  markerEnd={isTraced ? 'url(#arrow-trace)' : isSel ? 'url(#arrow-active)' : 'url(#arrow)'}
                  style={{ pointerEvents: 'none' }}/>
                {/* edge label (decision yes/no, loop body/done) */}
                {e.label && (
                  <g pointerEvents="none">
                    <rect
                      x={(p1.x + p2.x) / 2 - 18} y={(p1.y + p2.y) / 2 - 9}
                      width="36" height="16" rx="3"
                      fill="var(--surface)" stroke="var(--border-light)" strokeWidth="1"/>
                    <text
                      x={(p1.x + p2.x) / 2} y={(p1.y + p2.y) / 2 + 3}
                      textAnchor="middle" fontSize="10" fontFamily="var(--font-mono)"
                      fill="var(--text-secondary)">{e.label}</text>
                  </g>
                )}
              </g>
            );
          })}

          {/* In-progress wire */}
          {drag?.kind === 'wire' && (
            <path
              d={bezierPath(drag.fromPt.x, drag.fromPt.y, drag.to.x, drag.to.y)}
              stroke="var(--brand-primary)" strokeWidth="2" strokeDasharray="5 4"
              fill="none" style={{ pointerEvents: 'none' }}/>
          )}

          {/* Nodes */}
          {nodes.map(n => (
            <NodeShape
              key={n.id}
              node={n}
              selected={selection.nodes.includes(n.id)}
              traceCurrent={trace?.current === n.id}
              traceDone={trace?.completed.includes(n.id)}
              hasError={validationErrors.some(v => v.nodeId === n.id)}
              onMouseDown={(e) => handleNodeDown(e, n)}
              onPortOutDown={(e, idx) => handleOutPortDown(e, n, idx)}
              onPortInUp={(e) => handleInPortUp(e, n)}
            />
          ))}

          {/* Comments */}
          {comments.map(c => (
            <g key={c.id} transform={`translate(${c.x}, ${c.y})`}>
              <rect x="0" y="0" width="200" height="56" rx="6"
                fill="#fff8df" stroke="#d4a843" strokeWidth="1"/>
              <text x="10" y="20" fontSize="11" fontWeight="600" fill="#7d5a0e">{c.author}</text>
              <foreignObject x="10" y="24" width="180" height="28">
                <div style={{ fontSize: 10.5, color: '#5a4308', lineHeight: 1.4, fontFamily: 'var(--font-sans)' }}>{c.body}</div>
              </foreignObject>
            </g>
          ))}

          {/* Marquee selection */}
          {drag?.kind === 'marquee' && (
            <rect
              x={Math.min(drag.start.x, drag.end.x)} y={Math.min(drag.start.y, drag.end.y)}
              width={Math.abs(drag.end.x - drag.start.x)} height={Math.abs(drag.end.y - drag.start.y)}
              fill="var(--brand-primary-10)" stroke="var(--brand-primary)" strokeWidth="1" strokeDasharray="4 3"
              pointerEvents="none"/>
          )}
        </g>
      </svg>

      {/* Minimap (DOM, pinned to bottom-right) */}
      {showMinimap && (
        <Minimap nodes={nodes} view={view}/>
      )}

      {/* Zoom controls */}
      <ZoomControls view={view} setView={setView}/>
    </div>
  );
}

// ─── Single node ─────────────────────────────────────────────────
function NodeShape({ node, selected, traceCurrent, traceDone, hasError, onMouseDown, onPortOutDown, onPortInUp }) {
  const k = NODE_KINDS[node.kind];
  const tint = k.tint;
  const total = k.outputs;
  const ringColor = traceCurrent ? '#3ab795' : selected ? 'var(--brand-primary)' : hasError ? '#c44536' : 'transparent';
  const ringWidth = traceCurrent || selected || hasError ? 2 : 0;

  return (
    <g transform={`translate(${node.x}, ${node.y})`} style={{ cursor: 'grab' }}>
      {/* selection / state ring */}
      {ringWidth > 0 && (
        <rect x={-3} y={-3} width={NODE_W + 6} height={NODE_H + 6} rx="9"
          fill="none" stroke={ringColor} strokeWidth={ringWidth}
          strokeDasharray={traceCurrent ? '6 3' : 'none'}
          style={{ animation: traceCurrent ? 'pulse 1.4s infinite' : 'none' }}/>
      )}

      {/* card body */}
      <rect x="0" y="0" width={NODE_W} height={NODE_H} rx="7"
        fill="var(--surface)"
        stroke="var(--border-light)"
        strokeWidth="1"
        filter="drop-shadow(0 1px 2px rgba(0,0,0,0.04))"
        onMouseDown={onMouseDown}/>

      {/* tint header band */}
      <rect x="0" y="0" width={NODE_W} height={HEADER_H} rx="7"
        fill={tint + '14'} stroke="none"
        onMouseDown={onMouseDown}/>
      {/* Mask the bottom of the rounded header so it doesn't bleed past */}
      <rect x="0" y={HEADER_H - 7} width={NODE_W} height="7"
        fill={tint + '14'} stroke="none" onMouseDown={onMouseDown}/>

      {/* tint left rail (1.5px) */}
      <rect x="0" y="0" width="3" height={NODE_H} fill={tint}/>

      {/* icon chip */}
      <foreignObject x="10" y="6" width="18" height="18" pointerEvents="none">
        <div style={{
          width: 18, height: 18, borderRadius: 4,
          background: tint, color: '#fff',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        }}>
          <Icon name={k.icon} size={10}/>
        </div>
      </foreignObject>

      {/* kind label */}
      <text x="34" y="20" fontSize="10" fontWeight="600"
        fill={tint} letterSpacing="0.06em"
        style={{ textTransform: 'uppercase', pointerEvents: 'none' }}>
        {k.label}
      </text>

      {/* error pip */}
      {hasError && (
        <g transform={`translate(${NODE_W - 22}, 6)`} pointerEvents="none">
          <circle cx="8" cy="8" r="7" fill="#c44536"/>
          <text x="8" y="11.5" textAnchor="middle" fontSize="10" fontWeight="700" fill="#fff">!</text>
        </g>
      )}

      {/* node label (user-named) + summary line */}
      <foreignObject x="10" y={HEADER_H + 4} width={NODE_W - 20} height={NODE_H - HEADER_H - 6} pointerEvents="none">
        <div style={{ fontFamily: 'var(--font-sans)', display: 'flex', flexDirection: 'column', gap: 2, padding: '2px 0' }}>
          <div style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {node.label}
          </div>
          {node.summary && (
            <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
              {node.summary}
            </div>
          )}
        </div>
      </foreignObject>

      {/* trace done check */}
      {traceDone && !traceCurrent && (
        <g transform={`translate(${NODE_W - 20}, ${NODE_H - 20})`} pointerEvents="none">
          <circle cx="8" cy="8" r="8" fill="#3ab795"/>
          <path d="M 4 8 L 7 11 L 12 5" stroke="#fff" strokeWidth="2" fill="none"/>
        </g>
      )}

      {/* Input port */}
      {k.inputs > 0 && (
        <g transform={`translate(0, ${NODE_H / 2})`}>
          <circle cx="0" cy="0" r="9" fill="transparent" onMouseUp={onPortInUp}/>
          <circle cx="0" cy="0" r={PORT_R} fill="var(--surface)" stroke={tint} strokeWidth="2"
            pointerEvents="none"/>
        </g>
      )}

      {/* Output port(s) */}
      {Array.from({ length: k.outputs }).map((_, idx) => {
        const p = outPortPos({ x: 0, y: 0 }, idx, total);
        return (
          <g key={idx} transform={`translate(${p.x}, ${p.y})`} style={{ cursor: 'crosshair' }}>
            <circle cx="0" cy="0" r="9" fill="transparent" onMouseDown={(e) => onPortOutDown(e, idx)}/>
            <circle cx="0" cy="0" r={PORT_R} fill={tint} stroke="var(--surface)" strokeWidth="2" pointerEvents="none"/>
            {/* label for multi-output (decision/loop) */}
            {total > 1 && (
              <text x="14" y="3" fontSize="9" fontFamily="var(--font-mono)"
                fill="var(--text-tertiary)" pointerEvents="none">
                {node.kind === 'decision' ? (idx === 0 ? 'yes' : 'no') : (idx === 0 ? 'body' : 'done')}
              </text>
            )}
          </g>
        );
      })}
    </g>
  );
}

// ─── Minimap ─────────────────────────────────────────────────────
function Minimap({ nodes, view }) {
  if (!nodes.length) return null;
  const xs = nodes.map(n => n.x), ys = nodes.map(n => n.y);
  const x0 = Math.min(...xs) - 40, y0 = Math.min(...ys) - 40;
  const x1 = Math.max(...xs.map((x, i) => xs[i] + NODE_W)) + 40;
  const y1 = Math.max(...ys.map((y, i) => ys[i] + NODE_H)) + 40;
  const w = x1 - x0, h = y1 - y0;
  const W = 180, H = 110;
  const sx = W / w, sy = H / h;
  const s = Math.min(sx, sy);
  return (
    <div style={{
      position: 'absolute', right: 16, bottom: 16,
      width: W + 12, height: H + 12, padding: 6,
      background: 'var(--surface)', border: '1px solid var(--border-light)',
      borderRadius: 6, boxShadow: '0 4px 14px -4px rgba(0,0,0,0.12)',
      zIndex: 5,
    }}>
      <svg width={W} height={H} style={{ display: 'block', background: 'var(--surface-inset)', borderRadius: 3 }}>
        {nodes.map(n => (
          <rect key={n.id}
            x={(n.x - x0) * s} y={(n.y - y0) * s}
            width={NODE_W * s} height={NODE_H * s}
            rx="1.5"
            fill={NODE_KINDS[n.kind].tint} fillOpacity="0.8"/>
        ))}
      </svg>
    </div>
  );
}

// ─── Zoom controls ───────────────────────────────────────────────
function ZoomControls({ view, setView }) {
  const Btn = ({ children, onClick, title }) => (
    <button onClick={onClick} title={title}
      style={{
        width: 28, height: 28, padding: 0,
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 6, cursor: 'pointer',
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        color: 'var(--text-secondary)',
      }}>
      {children}
    </button>
  );
  return (
    <div style={{
      position: 'absolute', left: 16, bottom: 16, zIndex: 5,
      display: 'flex', gap: 6, alignItems: 'center',
      background: 'var(--surface)', padding: 4,
      border: '1px solid var(--border-light)', borderRadius: 8,
      boxShadow: '0 4px 14px -4px rgba(0,0,0,0.12)',
    }}>
      <Btn title="Zoom out" onClick={() => setView({ ...view, scale: Math.max(0.35, view.scale - 0.15) })}>
        <Icon name="minus" size={12}/>
      </Btn>
      <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-secondary)', minWidth: 38, textAlign: 'center' }}>
        {Math.round(view.scale * 100)}%
      </span>
      <Btn title="Zoom in" onClick={() => setView({ ...view, scale: Math.min(2, view.scale + 0.15) })}>
        <Icon name="plus" size={12}/>
      </Btn>
      <span style={{ width: 1, height: 16, background: 'var(--border-light)' }}/>
      <Btn title="Fit view" onClick={() => setView({ scale: 1, tx: 60, ty: 100 })}>
        <Icon name="search" size={12}/>
      </Btn>
    </div>
  );
}

Object.assign(window, {
  WorkflowCanvas, NODE_KINDS, NODE_W, NODE_H, snap, bezierPath, inPortPos, outPortPos,
});
