import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { NativeSelect } from '@/components/ui/native-select';
import { Textarea } from '@/components/ui/textarea';
import { workflowService } from '@/services/workflowService';
import type { DecisionComparison, DecisionParameters, WorkflowNodeParams } from './workflowTypes';
import { invoiceDecision } from './decisionParameters';

export function DecisionTablePanel({ parameters, onChange }: { parameters: WorkflowNodeParams; onChange: (params: WorkflowNodeParams) => void }) {
  const [fields, setFields] = useState<string[]>([]);
  const [operators, setOperators] = useState<string[]>([]);
  useEffect(() => { void workflowService.capabilities().then(capabilities => { setFields(capabilities.invoiceFacts.map(field => field.name)); setOperators(capabilities.comparisonOperators); }).catch(() => { setFields([]); setOperators([]); }); }, []);
  if (!parameters.rows || parameters.schemaVersion !== 1) return <div className="space-y-3"><p className="text-sm text-muted-foreground">Convert this legacy condition to an invoice decision table, then configure its routes and fixtures.</p><Button onClick={() => onChange(invoiceDecision())}>Create invoice decision table</Button></div>;
  const table = parameters as DecisionParameters;
  const updateComparison = (rowIndex: number, comparisonIndex: number, comparison: DecisionComparison) => onChange({ ...table, rows: table.rows.map((row, index) => index === rowIndex ? { ...row, all: row.all.map((item, position) => position === comparisonIndex ? comparison : item) } : row) });
  return <div className="space-y-4">
    <p className="text-xs text-muted-foreground">First matching row wins in priority order. Amounts are decimal strings with explicit currency. Missing required facts require manual review.</p>
    <label className="block space-y-1 text-xs">Safe default route<Input value={table.defaultRoute} onChange={event => onChange({ ...table, defaultRoute: event.target.value })} /></label>
    {table.rows.map((row, index) => <div key={index} className="space-y-3 rounded-md border p-3">
      <div className="grid grid-cols-2 gap-2"><label className="space-y-1 text-xs">Row key<Input value={row.id} onChange={event => onChange({ ...table, rows: table.rows.map((item, position) => position === index ? { ...item, id: event.target.value } : item) })} /></label>
        <label className="space-y-1 text-xs">Priority<Input type="number" value={row.priority} onChange={event => onChange({ ...table, rows: table.rows.map((item, position) => position === index ? { ...item, priority: Number(event.target.value) } : item) })} /></label></div>
      <label className="block space-y-1 text-xs">Route<Input value={row.route} onChange={event => onChange({ ...table, rows: table.rows.map((item, position) => position === index ? { ...item, route: event.target.value } : item) })} /></label>
      {row.all.map((comparison, comparisonIndex) => <div key={comparisonIndex} className="space-y-2 border-t pt-2">
        <NativeSelect aria-label="Invoice fact" value={comparison.field} onChange={event => updateComparison(index, comparisonIndex, { ...comparison, field: event.target.value })}>
          {[...new Set([comparison.field, ...fields])].map(field => <option key={field}>{field}</option>)}
        </NativeSelect>
        <NativeSelect aria-label="Comparison" value={comparison.operator} onChange={event => updateComparison(index, comparisonIndex, { ...comparison, operator: event.target.value, value: event.target.value === 'isMissing' ? undefined : comparison.value ?? '' })}>
          {[...new Set([comparison.operator, ...operators])].map(operator => <option key={operator}>{operator}</option>)}
        </NativeSelect>
        {comparison.operator !== 'isMissing' && (comparison.operator === 'in' ? <JsonField label="Allowed values (JSON array)" value={comparison.value ?? []} onChange={value => updateComparison(index, comparisonIndex, { ...comparison, value })} />
          : comparison.field === 'invoice.isDuplicate' || comparison.field === 'invoice.hasRiskConcern'
            ? <NativeSelect aria-label="Comparison value" value={String(comparison.value ?? '')} onChange={event => updateComparison(index, comparisonIndex, { ...comparison, value: event.target.value === 'true' })}><option value="" disabled>Choose a value</option><option value="true">True</option><option value="false">False</option></NativeSelect>
            : <Input aria-label="Comparison value" value={String(comparison.value ?? '')} onChange={event => updateComparison(index, comparisonIndex, { ...comparison, value: event.target.value })} />)}
        <Button size="sm" variant="outline" onClick={() => onChange({ ...table, rows: table.rows.map((item, position) => position === index ? { ...item, all: item.all.filter((_, position) => position !== comparisonIndex) } : item) })}>Remove comparison</Button>
      </div>)}
      <div className="flex flex-wrap gap-2"><Button size="sm" variant="outline" disabled={row.all.length >= 8} onClick={() => onChange({ ...table, rows: table.rows.map((item, position) => position === index ? { ...item, all: [...item.all, { field: 'invoice.status', operator: 'eq', value: 'Draft' }] } : item) })}>Add comparison</Button>
        <Button size="sm" variant="outline" onClick={() => onChange({ ...table, rows: table.rows.filter((_, position) => position !== index) })}>Remove row</Button></div>
    </div>)}
    <Button variant="outline" size="sm" disabled={table.rows.length >= 50} onClick={() => onChange({ ...table, rows: [...table.rows, { id: `row_${crypto.randomUUID().slice(0, 8)}`, priority: Math.max(0, ...table.rows.map(row => row.priority)) + 10, route: table.defaultRoute, all: [{ field: 'invoice.status', operator: 'eq', value: 'Draft' }] }] })}>Add decision row</Button>
    <JsonField label="Fixtures: Normal, Boundary and MissingData" value={table.fixtures ?? []} onChange={value => onChange({ ...table, fixtures: value as DecisionParameters['fixtures'] })} />
  </div>;
}

export function JsonField({ label, value, onChange }: { label: string; value: unknown; onChange: (value: unknown) => void }) {
  const encoded = JSON.stringify(value, null, 2);
  const [text, setText] = useState(encoded); const [error, setError] = useState(false);
  useEffect(() => { setText(encoded); setError(false); }, [encoded]);
  return <label className="block space-y-1 text-xs">{label}<Textarea value={text} rows={5} aria-invalid={error} onChange={event => {
    setText(event.target.value);
    try { const parsed: unknown = JSON.parse(event.target.value); event.target.setCustomValidity(''); setError(false); onChange(parsed); }
    catch { event.target.setCustomValidity('Enter valid JSON before saving.'); setError(true); }
  }} />{error && <span role="alert" className="text-destructive">Enter valid JSON before saving.</span>}</label>;
}
