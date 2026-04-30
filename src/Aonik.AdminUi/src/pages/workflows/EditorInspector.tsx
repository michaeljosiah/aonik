// Right inspector — switches between four views based on the canvas
// selection: single node, single edge, multi-selection, or workflow-level.
//
// 1:1 port of EditorInspector + the inspector subtypes (NodeInspector,
// EdgeInspector, MultiInspector, WorkflowInspector) + field primitives
// from templates/aonik-admin-starterkit/screens/workflow-editor-chrome.jsx.

import type { ReactNode } from 'react';
import {
  AlertTriangle,
  Copy,
  Package,
  Trash2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { NODE_KIND } from './stepKindCatalog';
import type {
  WorkflowEdge,
  WorkflowGraph,
  WorkflowNode,
  WorkflowNodeParams,
} from './workflowTypes';
import type { Selection, ValidationError } from './WorkflowCanvas';

// ─── Field primitives ───────────────────────────────────────────────

interface FieldLabelProps {
  children: ReactNode;
  hint?: string;
}

function FieldLabel({ children, hint }: FieldLabelProps) {
  return (
    <div className="flex items-center gap-1.5 text-[10.5px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
      {children}
      {hint && (
        <span className="font-medium normal-case tracking-normal text-[var(--color-text-tertiary)]">
          · {hint}
        </span>
      )}
    </div>
  );
}

interface TextFieldProps {
  label: string;
  value: string | undefined;
  onChange: (v: string) => void;
  mono?: boolean;
  hint?: string;
  placeholder?: string;
}

function TextField({ label, value, onChange, mono, hint, placeholder }: TextFieldProps) {
  return (
    <div>
      <FieldLabel hint={hint}>{label}</FieldLabel>
      <input
        value={value ?? ''}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        className="mt-1.5 box-border w-full rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)] text-[var(--color-text-primary)]"
        style={{
          padding: '8px 10px',
          fontSize: 12.5,
          fontFamily: mono ? 'var(--font-mono)' : 'inherit',
          borderBottomWidth: 2,
          borderBottomColor: 'var(--color-border-light)',
        }}
      />
    </div>
  );
}

interface TextAreaProps extends Omit<TextFieldProps, 'placeholder'> {
  rows?: number;
}

function TextArea({ label, value, onChange, hint, rows = 3, mono }: TextAreaProps) {
  return (
    <div>
      <FieldLabel hint={hint}>{label}</FieldLabel>
      <textarea
        value={value ?? ''}
        onChange={(e) => onChange(e.target.value)}
        rows={rows}
        className="mt-1.5 box-border w-full resize-y rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)] text-[var(--color-text-primary)]"
        style={{
          padding: '8px 10px',
          fontSize: 12,
          lineHeight: 1.5,
          fontFamily: mono ? 'var(--font-mono)' : 'inherit',
        }}
      />
    </div>
  );
}

interface SelectProps {
  label: string;
  value: string | undefined;
  onChange: (v: string) => void;
  options: string[];
  hint?: string;
}

function SelectField({ label, value, onChange, options, hint }: SelectProps) {
  return (
    <div>
      <FieldLabel hint={hint}>{label}</FieldLabel>
      <select
        value={value ?? ''}
        onChange={(e) => onChange(e.target.value)}
        className="mt-1.5 box-border w-full rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)] text-[var(--color-text-primary)]"
        style={{ padding: '8px 10px', fontSize: 12.5 }}
      >
        {options.map((o) => (
          <option key={o} value={o}>
            {o}
          </option>
        ))}
      </select>
    </div>
  );
}

// ─── Inspector shell ────────────────────────────────────────────────

interface InspectorShellProps {
  title: ReactNode;
  eyebrow?: ReactNode;
  kindTint?: string;
  children: ReactNode;
}

function InspectorShell({ title, eyebrow, kindTint, children }: InspectorShellProps) {
  return (
    <aside
      className="flex flex-none flex-col overflow-hidden border-l border-[var(--color-border-light)] bg-[var(--color-surface)]"
      style={{ width: 320 }}
    >
      <div className="border-b border-[var(--color-border-light)]" style={{ padding: 16 }}>
        {eyebrow && (
          <div
            className="mb-1 text-[9.5px] font-semibold uppercase tracking-[0.08em]"
            style={{ color: kindTint ?? 'var(--color-text-tertiary)' }}
          >
            {eyebrow}
          </div>
        )}
        <div className="text-[14px] font-semibold text-[var(--color-text-primary)]">{title}</div>
      </div>
      <div
        className="flex flex-1 flex-col gap-4 overflow-y-auto"
        style={{ padding: 16 }}
      >
        {children}
      </div>
    </aside>
  );
}

// ─── Node inspector ─────────────────────────────────────────────────

interface NodeInspectorProps {
  node: WorkflowNode;
  errors: ValidationError[];
  onUpdate: (patch: Partial<WorkflowNode> & { params?: Partial<WorkflowNodeParams> }) => void;
  onDelete: () => void;
}

function NodeInspector({ node, errors, onUpdate, onDelete }: NodeInspectorProps) {
  const meta = NODE_KIND[node.kind];

  const updateParam = <K extends keyof WorkflowNodeParams>(
    key: K,
    val: WorkflowNodeParams[K],
  ) => onUpdate({ params: { ...node.params, [key]: val } });

  return (
    <InspectorShell title={node.label} eyebrow={meta.label} kindTint={meta.tint}>
      {errors.length > 0 && (
        <div
          className="flex flex-col gap-1.5 rounded-md"
          style={{
            padding: '10px 12px',
            background: '#c4453610',
            border: '1px solid #c4453640',
          }}
        >
          {errors.map((e, i) => (
            <div
              key={i}
              className="flex items-start gap-1.5 text-[11.5px]"
              style={{ color: '#a3392b', lineHeight: 1.5 }}
            >
              <AlertTriangle size={11} />
              <span>{e.message}</span>
            </div>
          ))}
        </div>
      )}

      <TextField label="Name" value={node.label} onChange={(v) => onUpdate({ label: v })} />
      <TextArea
        label="Notes"
        hint="optional"
        value={node.notes}
        onChange={(v) => onUpdate({ notes: v })}
        rows={2}
      />

      {node.kind === 'trigger' && (
        <>
          <TextField
            label="Source"
            mono
            value={node.params.source}
            onChange={(v) => updateParam('source', v)}
          />
          <TextField
            label="Filter"
            mono
            hint="optional"
            value={node.params.filter}
            onChange={(v) => updateParam('filter', v)}
            placeholder="amount > 0"
          />
        </>
      )}
      {node.kind === 'tool' && (
        <>
          <SelectField
            label="Tool"
            value={node.params.tool}
            onChange={(v) => updateParam('tool', v)}
            options={[
              'search_invoices',
              'list_bank_transactions',
              'match_invoice_to_txn',
              'apply_match',
              'draft_journal_entry',
              'send_email',
              'fetch_fx_fix',
              'draft_forward_contract',
              'screen_counterparty',
              'lock_period',
              'aggregate_spend',
              'lookup_customer',
            ]}
          />
          <TextArea
            label="Parameters"
            mono
            hint="JSON"
            value={node.params.params}
            onChange={(v) => updateParam('params', v)}
            rows={4}
          />
        </>
      )}
      {node.kind === 'agent' && (
        <>
          <SelectField
            label="Agent"
            value={node.params.agent}
            onChange={(v) => updateParam('agent', v)}
            options={[
              'Billing',
              'Ledger',
              'FX',
              'Compliance',
              'Close',
              'Dunning',
              'Insights',
              'Orchestrator',
            ]}
          />
          <TextArea
            label="Task brief"
            value={node.params.task}
            onChange={(v) => updateParam('task', v)}
            rows={3}
          />
        </>
      )}
      {node.kind === 'decision' && (
        <>
          <TextField
            label="Condition"
            mono
            value={node.params.expr}
            onChange={(v) => updateParam('expr', v)}
          />
          <div className="grid grid-cols-2 gap-2">
            <TextField
              label="Yes label"
              value={node.params.yesLabel}
              onChange={(v) => updateParam('yesLabel', v)}
            />
            <TextField
              label="No label"
              value={node.params.noLabel}
              onChange={(v) => updateParam('noLabel', v)}
            />
          </div>
        </>
      )}
      {node.kind === 'human' && (
        <>
          <SelectField
            label="Approval group"
            value={node.params.group}
            onChange={(v) => updateParam('group', v)}
            options={['Treasury', 'Finance', 'Compliance', 'Anyone']}
          />
          <TextField
            label="SLA"
            hint="time before escalation"
            value={node.params.sla}
            onChange={(v) => updateParam('sla', v)}
          />
        </>
      )}
      {node.kind === 'wait' && (
        <TextField
          label="Duration"
          mono
          hint="e.g. 7d, 4h, 30m"
          value={node.params.duration}
          onChange={(v) => updateParam('duration', v)}
        />
      )}
      {node.kind === 'notify' && (
        <>
          <SelectField
            label="Channel"
            value={node.params.channel}
            onChange={(v) => updateParam('channel', v)}
            options={['email', 'sms', 'slack', 'push']}
          />
          <TextField
            label="Template"
            mono
            value={node.params.template}
            onChange={(v) => updateParam('template', v)}
          />
        </>
      )}
      {node.kind === 'emit' && (
        <TextField
          label="Event name"
          mono
          value={node.params.event}
          onChange={(v) => updateParam('event', v)}
        />
      )}
      {node.kind === 'loop' && (
        <>
          <TextField
            label="Iterate over"
            mono
            value={node.params.over}
            onChange={(v) => updateParam('over', v)}
          />
          <TextField
            label="Max iterations"
            mono
            value={String(node.params.maxIterations ?? '')}
            onChange={(v) => updateParam('maxIterations', v)}
          />
        </>
      )}

      {/* Footer */}
      <div
        className="mt-auto flex flex-col gap-2.5 border-t border-[var(--color-border-light)]"
        style={{ paddingTop: 12 }}
      >
        <div className="text-[10.5px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
          Node ID
        </div>
        <div
          className="text-[11px] text-[var(--color-text-secondary)]"
          style={{ fontFamily: 'var(--font-mono)' }}
        >
          {node.id}
        </div>
        <Button
          variant="outline"
          size="sm"
          onClick={onDelete}
          className="h-7"
          style={{ color: '#c44536', borderColor: '#c4453640' }}
        >
          <Trash2 size={11} />
          Delete node
        </Button>
      </div>
    </InspectorShell>
  );
}

// ─── Edge inspector ─────────────────────────────────────────────────

interface EdgeInspectorProps {
  edge: WorkflowEdge;
  nodes: WorkflowNode[];
  onDelete: () => void;
}

function EdgeInspector({ edge, nodes, onDelete }: EdgeInspectorProps) {
  const a = nodes.find((n) => n.id === edge.from);
  const b = nodes.find((n) => n.id === edge.to);
  return (
    <InspectorShell title="Connection" eyebrow="Edge">
      <div
        className="rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface-inset)]"
        style={{ padding: 12 }}
      >
        <div className="text-[10.5px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
          From
        </div>
        <div className="mt-0.5 text-[12.5px] font-medium text-[var(--color-text-primary)]">
          {a?.label}
        </div>
        <div className="mt-2.5 text-[10.5px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
          To
        </div>
        <div className="mt-0.5 text-[12.5px] font-medium text-[var(--color-text-primary)]">
          {b?.label}
        </div>
      </div>
      {edge.label && (
        <div>
          <FieldLabel>Label</FieldLabel>
          <div
            className="mt-1.5 text-[12px] text-[var(--color-text-primary)]"
            style={{ fontFamily: 'var(--font-mono)' }}
          >
            {edge.label}
          </div>
        </div>
      )}
      <Button
        variant="outline"
        size="sm"
        onClick={onDelete}
        className="h-7"
        style={{ color: '#c44536', borderColor: '#c4453640' }}
      >
        <Trash2 size={11} />
        Remove connection
      </Button>
    </InspectorShell>
  );
}

// ─── Multi-select inspector ─────────────────────────────────────────

interface MultiInspectorProps {
  count: number;
  onDeleteAll: () => void;
}

function MultiInspector({ count, onDeleteAll }: MultiInspectorProps) {
  return (
    <InspectorShell title={`${count} nodes selected`} eyebrow="Multi-select">
      <div className="text-[12px] text-[var(--color-text-secondary)]" style={{ lineHeight: 1.5 }}>
        Drag any node to move them together. Or run a bulk action below.
      </div>
      <Button variant="outline" size="sm" className="h-7">
        <Copy size={11} />
        Duplicate
      </Button>
      <Button variant="outline" size="sm" className="h-7">
        <Package size={11} />
        Group as sub-flow
      </Button>
      <Button
        variant="outline"
        size="sm"
        onClick={onDeleteAll}
        className="h-7"
        style={{ color: '#c44536', borderColor: '#c4453640' }}
      >
        <Trash2 size={11} />
        Delete {count} nodes
      </Button>
    </InspectorShell>
  );
}

// ─── Workflow inspector (no selection) ──────────────────────────────

interface WorkflowInspectorProps {
  workflow: WorkflowGraph;
  nodes: WorkflowNode[];
  edges: WorkflowEdge[];
  validationErrors: ValidationError[];
}

function WorkflowInspector({ workflow, nodes, edges, validationErrors }: WorkflowInspectorProps) {
  const counts = nodes.reduce<Record<string, number>>((acc, n) => {
    acc[n.kind] = (acc[n.kind] || 0) + 1;
    return acc;
  }, {});

  return (
    <InspectorShell title={workflow.name} eyebrow="Workflow">
      <TextField label="Name" value={workflow.name} onChange={() => {}} />
      <TextArea label="Description" value={workflow.desc} onChange={() => {}} rows={3} />

      <div>
        <FieldLabel>Composition</FieldLabel>
        <div className="mt-2 flex flex-col gap-1">
          <div className="text-[11.5px] text-[var(--color-text-secondary)]">
            <span style={{ fontFamily: 'var(--font-mono)' }}>{nodes.length}</span> node
            {nodes.length === 1 ? '' : 's'} ·{' '}
            <span style={{ fontFamily: 'var(--font-mono)' }}>{edges.length}</span> connection
            {edges.length === 1 ? '' : 's'}
          </div>
          {Object.entries(counts)
            .sort()
            .map(([k, c]) => {
              const meta = NODE_KIND[k as keyof typeof NODE_KIND];
              return (
                <div
                  key={k}
                  className="flex items-center gap-1.5 text-[11.5px] text-[var(--color-text-secondary)]"
                >
                  <span
                    className="rounded-[2px]"
                    style={{
                      width: 10,
                      height: 10,
                      background: meta.tint,
                      opacity: 0.8,
                    }}
                  />
                  <span className="flex-1">{meta.label}</span>
                  <span style={{ fontFamily: 'var(--font-mono)' }}>{c}</span>
                </div>
              );
            })}
        </div>
      </div>

      {validationErrors.length > 0 && (
        <div
          className="rounded-md"
          style={{
            background: '#c4453610',
            border: '1px solid #c4453640',
            padding: 12,
          }}
        >
          <div
            className="mb-1.5 text-[10.5px] font-semibold uppercase tracking-[0.06em]"
            style={{ color: '#c44536' }}
          >
            {validationErrors.length} issue{validationErrors.length === 1 ? '' : 's'}
          </div>
          {validationErrors.slice(0, 5).map((e, i) => (
            <div
              key={i}
              className="mt-1 text-[11.5px]"
              style={{ color: '#a3392b', lineHeight: 1.5 }}
            >
              · {e.message}
            </div>
          ))}
        </div>
      )}
    </InspectorShell>
  );
}

// ─── Empty inspector (rare — null fallback) ─────────────────────────

function EmptyInspector() {
  return (
    <InspectorShell title="Nothing selected" eyebrow="Inspector">
      <div className="text-[12px] text-[var(--color-text-secondary)]" style={{ lineHeight: 1.5 }}>
        Select a node or an edge to edit its properties.
      </div>
    </InspectorShell>
  );
}

// ─── Top-level switch ───────────────────────────────────────────────

export interface EditorInspectorProps {
  selection: Selection;
  nodes: WorkflowNode[];
  edges: WorkflowEdge[];
  workflow: WorkflowGraph;
  validationErrors: ValidationError[];
  onUpdateNode: (id: string, patch: Partial<WorkflowNode> & { params?: Partial<WorkflowNodeParams> }) => void;
  onDeleteNode: (id: string) => void;
  onDeleteEdge: (id: string) => void;
}

export function EditorInspector({
  selection,
  nodes,
  edges,
  workflow,
  validationErrors,
  onUpdateNode,
  onDeleteNode,
  onDeleteEdge,
}: EditorInspectorProps) {
  if (selection.nodes.length === 1) {
    const node = nodes.find((n) => n.id === selection.nodes[0]);
    if (!node) return <EmptyInspector />;
    const errs = validationErrors.filter((v) => v.nodeId === node.id);
    return (
      <NodeInspector
        node={node}
        errors={errs}
        onUpdate={(p) => onUpdateNode(node.id, p)}
        onDelete={() => onDeleteNode(node.id)}
      />
    );
  }
  if (selection.edges.length === 1) {
    const edge = edges.find((e) => e.id === selection.edges[0]);
    if (!edge) return <EmptyInspector />;
    return <EdgeInspector edge={edge} nodes={nodes} onDelete={() => onDeleteEdge(edge.id)} />;
  }
  if (selection.nodes.length > 1) {
    return (
      <MultiInspector
        count={selection.nodes.length}
        onDeleteAll={() => selection.nodes.forEach(onDeleteNode)}
      />
    );
  }
  return (
    <WorkflowInspector
      workflow={workflow}
      nodes={nodes}
      edges={edges}
      validationErrors={validationErrors}
    />
  );
}
