import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { NativeSelect } from '@/components/ui/native-select';
import { workflowService } from '@/services/workflowService';
import type { DecisionParameters, DecisionPredicate, DecisionRow, WorkflowNodeParams } from './workflowTypes';
import { defaultComparison, invoiceDecision, predicateSize } from './decisionParameters';
import { DecisionComparisonEditor, DecisionConditionBuilder } from './DecisionConditionBuilder';
import { WorkflowDecisionLibraryPanel } from './WorkflowDecisionLibraryPanel';
import { JsonField } from './WorkflowJsonField';

export { JsonField } from './WorkflowJsonField';

export function DecisionTablePanel({ parameters, onChange, workflowId = '', isLibrary = false }: { parameters: WorkflowNodeParams; onChange: (params: WorkflowNodeParams) => void; workflowId?: string; isLibrary?: boolean }) {
  const [fields, setFields] = useState<string[]>([]); const [operators, setOperators] = useState<string[]>([]);
  useEffect(() => { void workflowService.capabilities().then(capabilities => { setFields(capabilities.invoiceFacts.map(field => field.name)); setOperators(capabilities.comparisonOperators); }).catch(() => { setFields([]); setOperators([]); }); }, []);
  if (parameters.library && !isLibrary) return <WorkflowDecisionLibraryPanel workflowId={workflowId} parameters={parameters} onChange={onChange} onInline={() => onChange(invoiceDecision())} />;
  if (!Array.isArray(parameters.rows) || ![1, 2].includes(parameters.schemaVersion ?? 0) || !parameters.rows.every(row => row && Array.isArray(row.all) && row.all.every(validComparison) && (!row.condition || validPredicate(row.condition))))
    return <div className="space-y-3"><p className="text-sm text-muted-foreground">Configure a typed invoice decision table and its fixtures before publication.</p><Button onClick={() => onChange(invoiceDecision())}>Create invoice decision table</Button></div>;
  const table = parameters as DecisionParameters; const advanced = table.schemaVersion === 2;
  const nodes = table.rows.reduce((sum, row) => sum + (row.condition ? predicateSize(row.condition) : 1 + row.all.length), 0);
  const updateRow = (index: number, patch: Partial<DecisionRow>) => onChange({ ...table, rows: table.rows.map((row, position) => position === index ? { ...row, ...patch } : row) });
  const setPolicy = (hitPolicy: string) => onChange({ ...table, hitPolicy, collectRoute: hitPolicy === 'Collect' ? 'collected' : undefined,
    scoreBands: hitPolicy === 'Score' ? [{ minimumInclusive: '-500000', maximumExclusive: '500001', route: 'score' }] : undefined,
    rows: table.rows.map(row => ({ ...row, scoreWeight: hitPolicy === 'Score' ? row.scoreWeight ?? '1' : undefined, collectInvoiceId: hitPolicy === 'Collect' ? row.collectInvoiceId ?? { fact: 'invoice.id' } : undefined })) });
  return <div className="space-y-4">
    <p className="text-xs text-muted-foreground">{advanced ? 'Conditions are bounded to depth four and 100 nodes. Missing optional values remain Unknown under negation; use isMissing explicitly.' : 'First matching row wins in priority order. Amounts are decimal strings with explicit currency. Missing required facts require manual review.'}</p>
    {!isLibrary && <Button size="sm" variant="outline" onClick={() => onChange({ schemaVersion: 1, library: { versionId: '', definitionHash: '' }, inputs: {}, routes: [], outputType: 'Route' })}>Use a published decision library</Button>}
    {!advanced ? <Button size="sm" variant="outline" onClick={() => onChange({ ...table, schemaVersion: 2, currency: 'GBP', optionalFactBehavior: 'Unknown' })}>Enable advanced rules</Button> : <>
      <label className="block space-y-1 text-xs">Hit policy<NativeSelect value={table.hitPolicy} onChange={event => setPolicy(event.target.value)}>{['FirstMatch', 'Unique', 'Collect', 'Score'].map(policy => <option key={policy}>{policy}</option>)}</NativeSelect></label>
      <label className="block space-y-1 text-xs">Invoice currency<Input maxLength={3} value={table.currency ?? ''} onChange={event => onChange({ ...table, currency: event.target.value.toUpperCase() || undefined })} /></label>
      <p className="text-xs text-muted-foreground">Optional facts: {table.optionalFactBehavior ?? 'Unknown must be selected'}. Predicate nodes: {nodes}/100. A currency mismatch requires manual review.</p>
      {table.hitPolicy === 'Collect' && <><label className="block space-y-1 text-xs">Collection route<Input value={table.collectRoute ?? ''} onChange={event => onChange({ ...table, collectRoute: event.target.value })} /></label><p className="text-xs text-muted-foreground">Rows return ordered, deduplicated invoice references. Wire the collection route to an explicitly bound foreach. Each child keeps its own grants and approvals.</p></>}
      {table.hitPolicy === 'Score' && <div className="space-y-2 text-xs"><p>Score weights are decimal strings, magnitude at most 10000 with four decimal places. Adjacent bands must cover all possible totals; scoring cannot reduce action approval tiers.</p>
        {(table.scoreBands ?? []).map((band, index) => <fieldset key={index} className="space-y-2 rounded-md border p-2"><legend>Score band {index + 1}</legend>{(['minimumInclusive', 'maximumExclusive', 'route'] as const).map(field => <label key={field} className="block space-y-1">{field === 'minimumInclusive' ? 'Minimum inclusive' : field === 'maximumExclusive' ? 'Maximum exclusive' : 'Route'}<Input value={band[field]} onChange={event => onChange({ ...table, scoreBands: table.scoreBands!.map((item, position) => position === index ? { ...item, [field]: event.target.value } : item) })} /></label>)}<Button size="sm" variant="outline" onClick={() => onChange({ ...table, scoreBands: table.scoreBands!.filter((_, position) => position !== index) })}>Remove band</Button></fieldset>)}
        <Button size="sm" variant="outline" disabled={(table.scoreBands?.length ?? 0) >= 50} onClick={() => onChange({ ...table, scoreBands: [...(table.scoreBands ?? []), { minimumInclusive: '0', maximumExclusive: '1', route: 'score' }] })}>Add score band</Button>
      </div>}
    </>}
    <label className="block space-y-1 text-xs">Safe default route<Input value={table.defaultRoute} onChange={event => onChange({ ...table, defaultRoute: event.target.value })} /></label>
    {table.rows.map((row, index) => <div key={index} className="space-y-3 rounded-md border p-3">
      <div className="grid grid-cols-2 gap-2"><label className="space-y-1 text-xs">Row key<Input value={row.id} onChange={event => updateRow(index, { id: event.target.value })} /></label><label className="space-y-1 text-xs">Priority<Input type="number" value={row.priority} onChange={event => updateRow(index, { priority: Number(event.target.value) })} /></label></div>
      <label className="block space-y-1 text-xs">{table.hitPolicy === 'Collect' ? 'Stable collected item key' : 'Route'}<Input value={row.route} onChange={event => updateRow(index, { route: event.target.value })} /></label>
      {table.hitPolicy === 'Score' && <label className="block space-y-1 text-xs">Score contribution<Input value={row.scoreWeight ?? ''} onChange={event => updateRow(index, { scoreWeight: event.target.value })} /></label>}
      {table.hitPolicy === 'Collect' && <div className="space-y-2 text-xs"><label className="block space-y-1">Invoice reference<NativeSelect value={row.collectInvoiceId?.fact ? 'fact' : 'literal'} onChange={event => updateRow(index, { collectInvoiceId: event.target.value === 'fact' ? { fact: 'invoice.id' } : { literal: '' } })}><option value="fact">Current invoice ID</option><option value="literal">Stored invoice ID</option></NativeSelect></label>{!row.collectInvoiceId?.fact && <Input aria-label="Collected invoice ID" value={String(row.collectInvoiceId?.literal ?? '')} onChange={event => updateRow(index, { collectInvoiceId: { literal: event.target.value } })} />}</div>}
      {row.condition ? <DecisionConditionBuilder value={row.condition} canAdd={nodes < 100} fields={fields} operators={operators} onChange={condition => updateRow(index, { condition })} /> : <>
        {row.all.map((comparison, comparisonIndex) => <div key={comparisonIndex} className="space-y-2 border-t pt-2"><DecisionComparisonEditor value={comparison} fields={fields} operators={operators} onChange={updated => updateRow(index, { all: row.all.map((item, position) => position === comparisonIndex ? updated : item) })} /><Button size="sm" variant="outline" onClick={() => updateRow(index, { all: row.all.filter((_, position) => position !== comparisonIndex) })}>Remove comparison</Button></div>)}
        <div className="flex flex-wrap gap-2"><Button size="sm" variant="outline" disabled={row.all.length >= 8 || advanced && nodes >= 100} onClick={() => updateRow(index, { all: [...row.all, defaultComparison()] })}>Add comparison</Button>{advanced && <Button size="sm" variant="outline" onClick={() => updateRow(index, { all: [], condition: { kind: 'All', children: row.all.length ? row.all.map(comparison => ({ kind: 'Comparison', comparison })) : [{ kind: 'Comparison', comparison: defaultComparison() }] } })}>Use condition groups</Button>}</div>
      </>}
      <Button size="sm" variant="outline" onClick={() => onChange({ ...table, rows: table.rows.filter((_, position) => position !== index) })}>Remove row</Button>
    </div>)}
    <Button variant="outline" size="sm" disabled={table.rows.length >= 50 || advanced && nodes >= 99} onClick={() => onChange({ ...table, rows: [...table.rows, { id: `row_${crypto.randomUUID().slice(0, 8)}`, priority: Math.max(0, ...table.rows.map(row => row.priority)) + 10, route: table.defaultRoute, all: [defaultComparison()], scoreWeight: table.hitPolicy === 'Score' ? '1' : undefined, collectInvoiceId: table.hitPolicy === 'Collect' ? { fact: 'invoice.id' } : undefined }] })}>Add decision row</Button>
    <JsonField label="Fixtures: Normal, Boundary and MissingData" value={table.fixtures ?? []} onChange={value => onChange({ ...table, fixtures: value as DecisionParameters['fixtures'] })} />
  </div>;
}

function validPredicate(value: DecisionPredicate, depth = 1): boolean {
  return value !== null && typeof value === 'object' && depth <= 4 && (value.kind === 'Comparison' ? validComparison(value.comparison)
    : ['All', 'Any', 'Not'].includes(value.kind) && Array.isArray(value.children) && value.children.length > 0 && value.children.length <= 100
      && (value.kind !== 'Not' || value.children.length === 1) && value.children.every(child => validPredicate(child, depth + 1)));
}
function validComparison(value: unknown): boolean { return value !== null && typeof value === 'object' && 'field' in value && typeof value.field === 'string' && 'operator' in value && typeof value.operator === 'string'; }
