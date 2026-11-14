import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { NativeSelect } from '@/components/ui/native-select';
import { compositionParameters } from './decisionParameters';
import { WorkflowInvoiceBindingPanel } from './WorkflowInvoiceBindingPanel';
import { WorkflowVersionPicker } from './WorkflowVersionPicker';
import type { WorkflowNodeParams, WorkflowParallelBranch, WorkflowVersionPin } from './workflowTypes';

export function WorkflowCompositionPanel({ workflowId, kind, parameters, onChange }: { workflowId: string; kind: 'child' | 'loop' | 'parallel'; parameters: WorkflowNodeParams; onChange: (value: WorkflowNodeParams) => void }) {
  const valid = (parameters.schemaVersion === 1 || kind === 'loop' && parameters.schemaVersion === 2 && typeof parameters.itemsFromDecision === 'string') && (kind === 'child' ? isPin(parameters.workflow) && parameters.invoiceId && typeof parameters.invoiceId === 'object'
    : kind === 'loop' ? isPin(parameters.body) && Array.isArray(parameters.items) && parameters.items.every(item => item && typeof item.key === 'string' && typeof item.invoiceId === 'string')
      : Array.isArray(parameters.branches) && parameters.branches.every(branch => branch && typeof branch.key === 'string' && isPin(branch.workflow) && branch.invoiceId && typeof branch.invoiceId === 'object'));
  if (!valid) return <div className="space-y-2 text-xs"><p>This saved step requires an explicit version and bounded control contract before publication.</p><Button size="sm" variant="outline" onClick={() => onChange(compositionParameters(kind))}>Configure {kind === 'loop' ? 'bounded foreach' : kind}</Button></div>;
  const change = (patch: Partial<WorkflowNodeParams>) => onChange({ ...parameters, ...patch });
  const updateBranch = (index: number, patch: Partial<WorkflowParallelBranch>) => change({ branches: parameters.branches!.map((branch, position) => position === index ? { ...branch, ...patch } : branch) });
  return <div className="space-y-3 text-xs">
    <p className="text-muted-foreground">Each child keeps its own authorization and financial approvals. Unattended runs require grants for each child version and invoice scope.</p>
    {kind === 'child' && <><WorkflowVersionPicker workflowId={workflowId} pin={parameters.workflow!} onChange={workflow => change({ workflow })} /><WorkflowInvoiceBindingPanel value={parameters.invoiceId ?? { fact: 'invoice.id' }} onChange={invoiceId => change({ invoiceId })} /></>}
    {kind === 'loop' && <>
      <WorkflowVersionPicker workflowId={workflowId} pin={parameters.body!} onChange={body => change({ body })} />
      <label className="block space-y-1">Maximum iterations<Input type="number" min={1} max={100} step={1} value={parameters.maxIterations ?? 100} onChange={event => change({ maxIterations: Number(event.target.value) })} /></label>
      <label className="block space-y-1">Total loop window (seconds)<Input type="number" min={1} max={604800} step={1} value={parameters.timeoutSeconds ?? 3600} onChange={event => change({ timeoutSeconds: Number(event.target.value) })} /></label>
      <label className="block space-y-1">Collection source<NativeSelect value={parameters.schemaVersion === 2 ? 'decision' : 'literal'} onChange={event => { const { itemsFromDecision: previousSource, ...previous } = parameters; onChange(event.target.value === 'decision' ? { ...previous, schemaVersion: 2, items: [], itemsFromDecision: previousSource ?? '' } : { ...previous, schemaVersion: 1, items: [] }); }}><option value="literal">Declared invoice list</option><option value="decision">Typed Collect decision output</option></NativeSelect></label>
      {parameters.schemaVersion === 2 ? <><label className="block space-y-1">Source decision key<Input value={parameters.itemsFromDecision ?? ''} onChange={event => change({ itemsFromDecision: event.target.value })} /></label><p className="text-muted-foreground">Connect the source decision directly to this foreach. Its typed items and stable keys are persisted before the first child starts.</p></> : <>
        {(parameters.items ?? []).map((item, index) => <fieldset key={index} className="space-y-2 rounded-md border p-2"><legend>Item {index + 1}</legend><label className="block space-y-1">Stable item key<Input maxLength={32} value={item.key} onChange={event => change({ items: parameters.items!.map((value, position) => position === index ? { ...value, key: event.target.value } : value) })} /></label><label className="block space-y-1">Stored invoice ID<Input value={item.invoiceId} onChange={event => change({ items: parameters.items!.map((value, position) => position === index ? { ...value, invoiceId: event.target.value } : value) })} /></label><Button size="sm" variant="outline" onClick={() => change({ items: parameters.items!.filter((_, position) => position !== index) })}>Remove item</Button></fieldset>)}
        <Button size="sm" variant="outline" disabled={(parameters.items?.length ?? 0) >= 100} onClick={() => change({ items: [...(parameters.items ?? []), { key: `item-${crypto.randomUUID().slice(0, 8)}`, invoiceId: '' }] })}>Add invoice item</Button>
      </>}
      <label className="block space-y-1">Exhaustion route<Input value={parameters.exhaustedRoute ?? 'exhausted'} onChange={event => change({ exhaustedRoute: event.target.value })} /></label>
    </>}
    {kind === 'parallel' && <>
      <label className="block space-y-1">Join policy<NativeSelect value={parameters.joinPolicy ?? 'All'} onChange={event => change({ joinPolicy: event.target.value as 'All' | 'AnySuccess' })}><option value="All">All required branches</option><option value="AnySuccess">Any success from read/pure branches</option></NativeSelect></label>
      <label className="block space-y-1">Concurrent branches<NativeSelect value={parameters.maxConcurrency ?? 1} onChange={event => change({ maxConcurrency: Number(event.target.value) })}>{Array.from({ length: parameters.branches!.length }, (_, index) => <option key={index + 1} value={index + 1}>{index + 1}</option>)}</NativeSelect></label>
      <p className="text-muted-foreground">Writes require All with concurrency one. The full pinned graph is limited to four concurrent branches.</p>
      {parameters.branches!.map((branch, index) => <fieldset key={index} className="space-y-2 rounded-md border p-2"><legend>Branch {index + 1}</legend><label className="block space-y-1">Stable branch key<Input maxLength={32} value={branch.key} onChange={event => updateBranch(index, { key: event.target.value })} /></label><WorkflowVersionPicker workflowId={workflowId} pin={branch.workflow} onChange={workflow => updateBranch(index, { workflow })} /><WorkflowInvoiceBindingPanel value={branch.invoiceId} onChange={invoiceId => updateBranch(index, { invoiceId })} /><Button size="sm" variant="outline" disabled={parameters.branches!.length <= 2} onClick={() => change({ branches: parameters.branches!.filter((_, position) => position !== index), maxConcurrency: Math.min(parameters.maxConcurrency ?? 1, parameters.branches!.length - 1) })}>Remove branch</Button></fieldset>)}
      <Button size="sm" variant="outline" disabled={parameters.branches!.length >= 4} onClick={() => change({ branches: [...parameters.branches!, { key: `branch-${crypto.randomUUID().slice(0, 8)}`, workflow: { versionId: '', definitionHash: '' }, invoiceId: { fact: 'invoice.id' } }] })}>Add branch</Button>
    </>}
    <label className="block space-y-1">Completion route<Input value={parameters.completedRoute ?? 'completed'} onChange={event => change({ completedRoute: event.target.value })} /></label>
    <label className="block space-y-1">Failure route<Input value={parameters.errorRoute ?? 'error'} onChange={event => change({ errorRoute: event.target.value })} /></label>
    <p className="text-muted-foreground">Connect every route. Unknown effects stay held for reconciliation.</p>
  </div>;
}

function isPin(value: unknown): value is WorkflowVersionPin {
  return value !== null && typeof value === 'object' && typeof (value as WorkflowVersionPin).versionId === 'string' && typeof (value as WorkflowVersionPin).definitionHash === 'string';
}
