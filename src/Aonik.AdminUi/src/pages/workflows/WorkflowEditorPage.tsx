// Workflow editor — full-page surface composing header, palette, canvas,
// inspector and the optional bottom panels. Reads its initial workflow
// graph + comments + recent runs + version history from the live API
// (workflowService.getBySlug / listRuns / listVersions). Edits stay
// in-memory — there's no PUT endpoint yet, so save/discard cycle the
// fetched copy rather than persisting.
//
// Initial port: templates/aonik-admin-starterkit/screens/workflow-editor-screen.jsx

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Workflow as WorkflowIcon } from 'lucide-react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { useWorkflow, useWorkflowRuns, useWorkflowVersions } from '@/hooks/useWorkflows';
import { adaptComment, adaptGraph, adaptRun, adaptVersion } from './workflowAdapters';
import { NODE_KIND } from './stepKindCatalog';
import {
  type EditorNodeKind,
  type WorkflowComment,
  type WorkflowEdge,
  type WorkflowGraph,
  type WorkflowNode,
  type WorkflowNodeParams,
} from './workflowTypes';
import { WorkflowCanvas, type Selection, type TraceState, type ValidationError } from './WorkflowCanvas';
import type { CanvasView } from './Minimap';
import { EditorHeader } from './EditorHeader';
import { EditorPalette } from './EditorPalette';
import { EditorInspector } from './EditorInspector';
import { TestPanel, HistoryPanel, TraceBar } from './EditorPanels';

// Validation — derive errors from the graph state.
// Mirrors computeValidation() in workflow-editor-screen.jsx.
function computeValidation(
  nodes: WorkflowNode[],
  edges: WorkflowEdge[],
): ValidationError[] {
  const errs: ValidationError[] = [];
  const inDeg: Record<string, number> = {};
  const outDeg: Record<string, number> = {};
  nodes.forEach((n) => {
    inDeg[n.id] = 0;
    outDeg[n.id] = 0;
  });
  edges.forEach((e) => {
    outDeg[e.from] = (outDeg[e.from] || 0) + 1;
    inDeg[e.to] = (inDeg[e.to] || 0) + 1;
  });

  nodes.forEach((n) => {
    const k = NODE_KIND[n.kind];
    if ((k.inputs ?? 0) > 0 && inDeg[n.id] === 0 && n.kind !== 'trigger') {
      errs.push({ nodeId: n.id, message: `${n.label}: no incoming connection.` });
    }
    if ((k.outputs ?? 0) > 0 && outDeg[n.id] === 0 && n.kind !== 'end') {
      errs.push({
        nodeId: n.id,
        message: `${n.label}: dangling output — connect to the next step or an End node.`,
      });
    }
    if (n.kind === 'decision' && outDeg[n.id] < 2) {
      errs.push({
        nodeId: n.id,
        message: `${n.label}: decision needs both yes and no branches wired.`,
      });
    }
  });
  if (!nodes.some((n) => n.kind === 'trigger')) {
    errs.push({ nodeId: null, message: 'Workflow has no trigger node.' });
  }
  return errs;
}

export function WorkflowEditorPage() {
  const navigate = useNavigate();
  const { workflowId: slug } = useParams<{ workflowId: string }>();

  const { workflow: dto, loading, error } = useWorkflow(slug);
  const { runs: rawRuns } = useWorkflowRuns(dto?.id);
  const { versions: rawVersions } = useWorkflowVersions(dto?.id);

  const initialGraph = useMemo<WorkflowGraph | null>(
    () => (dto ? adaptGraph(dto) : null),
    [dto],
  );
  const initialComments = useMemo<WorkflowComment[]>(
    () => (dto ? dto.comments.map(adaptComment) : []),
    [dto],
  );
  const runs = useMemo(() => rawRuns.map(adaptRun), [rawRuns]);
  const versions = useMemo(() => rawVersions.map(adaptVersion), [rawVersions]);

  const [wf, setWf] = useState<WorkflowGraph | null>(null);
  const [view, setView] = useState<CanvasView>({ scale: 0.8, tx: 60, ty: 80 });
  const [selection, setSelection] = useState<Selection>({ nodes: [], edges: [] });
  const [hoveredEdge, setHoveredEdge] = useState<string | null>(null);
  const [hasChanges, setHasChanges] = useState(false);
  const [paletteCollapsed, setPaletteCollapsed] = useState(false);

  // Bottom panels
  const [testOpen, setTestOpen] = useState(true);
  const [traceOpen, setTraceOpen] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);

  // Trace
  const [trace, setTrace] = useState<TraceState | null>(null);

  // Hydrate workflow state when API returns. Keep in-memory edits if they exist.
  useEffect(() => {
    if (initialGraph) {
      setWf(initialGraph);
      setHasChanges(false);
      setSelection({ nodes: [], edges: [] });
    }
  }, [initialGraph]);

  const dirty = useCallback(() => setHasChanges(true), []);

  // ── Mutations ──
  const onMoveNode = useCallback(
    (id: string, x: number, y: number) => {
      setWf((w) =>
        w
          ? { ...w, nodes: w.nodes.map((n) => (n.id === id ? { ...n, x, y } : n)) }
          : w,
      );
      dirty();
    },
    [dirty],
  );

  const onUpdateNode = useCallback(
    (
      id: string,
      patch: Partial<WorkflowNode> & { params?: Partial<WorkflowNodeParams> },
    ) => {
      setWf((w) =>
        w
          ? {
              ...w,
              nodes: w.nodes.map((n) =>
                n.id === id
                  ? {
                      ...n,
                      ...patch,
                      params: { ...n.params, ...(patch.params ?? {}) },
                    }
                  : n,
              ),
            }
          : w,
      );
      dirty();
    },
    [dirty],
  );

  const onDeleteNode = useCallback(
    (id: string) => {
      setWf((w) =>
        w
          ? {
              ...w,
              nodes: w.nodes.filter((n) => n.id !== id),
              edges: w.edges.filter((e) => e.from !== id && e.to !== id),
            }
          : w,
      );
      setSelection({ nodes: [], edges: [] });
      dirty();
    },
    [dirty],
  );

  const onAddEdge = useCallback(
    ({ from, to, fromIdx }: { from: string; to: string; fromIdx: number }) => {
      setWf((w) => {
        if (!w) return w;
        const key = `${from}-${fromIdx}-${to}`;
        const exists = w.edges.some(
          (e) => `${e.from}-${e.fromIdx ?? 0}-${e.to}` === key,
        );
        if (exists) return w;
        const newId = 'e' + Math.random().toString(36).slice(2, 7);
        const fromNode = w.nodes.find((n) => n.id === from);
        let label: string | undefined;
        if (fromNode?.kind === 'decision') {
          label = fromIdx === 0 ? fromNode.params.yesLabel : fromNode.params.noLabel;
        } else if (fromNode?.kind === 'loop') {
          label = fromIdx === 0 ? 'body' : 'done';
        }
        const next: WorkflowEdge = { id: newId, from, to, fromIdx, label };
        return { ...w, edges: [...w.edges, next] };
      });
      dirty();
    },
    [dirty],
  );

  const onDeleteEdge = useCallback(
    (id: string) => {
      setWf((w) => (w ? { ...w, edges: w.edges.filter((e) => e.id !== id) } : w));
      setSelection({ nodes: [], edges: [] });
      dirty();
    },
    [dirty],
  );

  const onDropPaletteItem = useCallback(
    (kind: string, x: number, y: number) => {
      const k = NODE_KIND[kind as EditorNodeKind];
      if (!k) return;
      const id = 'n' + Math.random().toString(36).slice(2, 7);
      setWf((w) =>
        w
          ? {
              ...w,
              nodes: [
                ...w.nodes,
                {
                  id,
                  kind: kind as EditorNodeKind,
                  x,
                  y,
                  label: k.label,
                  summary: '',
                  params: { ...(k.defaults as WorkflowNodeParams) },
                },
              ],
            }
          : w,
      );
      setSelection({ nodes: [id], edges: [] });
      dirty();
    },
    [dirty],
  );

  // ── Validation ──
  const validationErrors = useMemo(
    () => (wf ? computeValidation(wf.nodes, wf.edges) : []),
    [wf],
  );

  // ── Trace control ──
  const startTrace = useCallback(
    (runId: string = runs[0]?.id ?? '', atStep = 1) => {
      const run = runs.find((r) => r.id === runId);
      if (!run) return;
      const completed = run.sequence.slice(0, atStep);
      const current = run.sequence[atStep] ?? run.sequence[run.sequence.length - 1];
      setTrace({ runId, current, completed });
      setTraceOpen(true);
    },
    [runs],
  );

  const stepTrace = useCallback(
    (delta: number) => {
      if (!trace) return;
      const run = runs.find((r) => r.id === trace.runId);
      if (!run) return;
      const next = Math.max(0, Math.min(run.sequence.length, trace.completed.length + delta));
      setTrace({
        runId: trace.runId,
        current: run.sequence[next] ?? run.sequence[run.sequence.length - 1],
        completed: run.sequence.slice(0, next),
      });
    },
    [runs, trace],
  );

  useEffect(() => {
    if (!traceOpen) setTrace(null);
  }, [traceOpen]);

  // ── Keyboard: Delete/Backspace removes selection, Escape clears it ──
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Delete' || e.key === 'Backspace') {
        const tag = (document.activeElement as HTMLElement | null)?.tagName;
        if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return;
        if (selection.nodes.length) selection.nodes.forEach(onDeleteNode);
        if (selection.edges.length) selection.edges.forEach(onDeleteEdge);
      }
      if (e.key === 'Escape') setSelection({ nodes: [], edges: [] });
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [selection, onDeleteNode, onDeleteEdge]);

  // ── States: error / loading / not-found / loaded ──────────────────────

  if (error) {
    return (
      <CenteredMessage
        title="Couldn't load workflow"
        body={error}
        action={
          <Link to="/ai/workflows">
            <Button size="sm" variant="outline">Back to Workflows</Button>
          </Link>
        }
      />
    );
  }

  if (loading || !wf) {
    return (
      <CenteredMessage
        title="Loading workflow…"
        body="Fetching graph, runs, and version history."
      />
    );
  }

  if (!initialGraph) {
    return (
      <CenteredMessage
        title="Workflow not found"
        body={`We couldn't find a workflow with slug "${slug}".`}
        action={
          <Link to="/ai/workflows">
            <Button size="sm" variant="outline">Back to Workflows</Button>
          </Link>
        }
      />
    );
  }

  return (
    <div className="flex h-full w-full flex-col overflow-hidden bg-[var(--color-background)]">
      <EditorHeader
        workflow={wf}
        onClose={() => navigate('/ai/workflows')}
        hasChanges={hasChanges}
        onSave={() => setHasChanges(false)}
        onDiscard={() => {
          if (initialGraph) setWf(initialGraph);
          setHasChanges(false);
        }}
        testOpen={testOpen}
        setTestOpen={setTestOpen}
        traceOpen={traceOpen}
        setTraceOpen={(v) => {
          setTraceOpen(v);
          if (v && !trace) startTrace();
        }}
        historyOpen={historyOpen}
        setHistoryOpen={setHistoryOpen}
        validationErrors={validationErrors}
      />

      {traceOpen && (
        <TraceBar
          trace={trace}
          runs={runs}
          onPick={(id) => startTrace(id, 1)}
          onStep={stepTrace}
          onClose={() => setTraceOpen(false)}
        />
      )}

      <div className="flex min-h-0 flex-1">
        <EditorPalette collapsed={paletteCollapsed} setCollapsed={setPaletteCollapsed} />

        <div className="flex min-w-0 flex-1 flex-col">
          <WorkflowCanvas
            nodes={wf.nodes}
            edges={wf.edges}
            view={view}
            setView={setView}
            selection={selection}
            setSelection={setSelection}
            hoveredEdge={hoveredEdge}
            setHoveredEdge={setHoveredEdge}
            onMoveNode={onMoveNode}
            onAddEdge={onAddEdge}
            onDropPaletteItem={onDropPaletteItem}
            comments={initialComments}
            trace={trace}
            validationErrors={validationErrors}
          />
          {testOpen && (
            <TestPanel
              workflow={wf}
              onClose={() => setTestOpen(false)}
              onStartRun={() => startTrace()}
            />
          )}
        </div>

        <EditorInspector
          selection={selection}
          nodes={wf.nodes}
          edges={wf.edges}
          workflow={wf}
          validationErrors={validationErrors}
          onUpdateNode={onUpdateNode}
          onDeleteNode={onDeleteNode}
          onDeleteEdge={onDeleteEdge}
        />

        {historyOpen && (
          <HistoryPanel
            versions={versions}
            onClose={() => setHistoryOpen(false)}
            onRestore={() => {}}
          />
        )}
      </div>
    </div>
  );
}

interface CenteredMessageProps {
  title: string;
  body: string;
  action?: React.ReactNode;
}

function CenteredMessage({ title, body, action }: CenteredMessageProps) {
  return (
    <div className="flex h-full w-full items-center justify-center p-12">
      <div
        className="flex max-w-md flex-col items-center rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] text-center"
        style={{ padding: '40px 32px' }}
      >
        <span
          className="mb-4 inline-flex items-center justify-center rounded-full bg-[var(--color-brand-primary-10)] text-[var(--color-brand-primary)]"
          style={{ width: 40, height: 40 }}
        >
          <WorkflowIcon className="h-5 w-5" />
        </span>
        <div className="mb-1.5 text-[14px] font-semibold text-[var(--color-text-primary)]">
          {title}
        </div>
        <div className="mb-4 text-[12.5px] text-[var(--color-text-secondary)]" style={{ lineHeight: 1.5 }}>
          {body}
        </div>
        {action}
      </div>
    </div>
  );
}
