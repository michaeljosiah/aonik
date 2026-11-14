import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { NativeSelect } from '@/components/ui/native-select';
import { JsonField } from './WorkflowJsonField';
import { defaultComparison } from './decisionParameters';
import type { DecisionComparison, DecisionPredicate } from './workflowTypes';

export function DecisionComparisonEditor({ value, fields, operators, onChange }: { value: DecisionComparison; fields: string[]; operators: string[]; onChange: (value: DecisionComparison) => void }) {
  return <div className="space-y-2">
    <NativeSelect aria-label="Invoice fact" value={value.field} onChange={event => onChange({ ...value, field: event.target.value })}>{[...new Set([value.field, ...fields])].map(field => <option key={field}>{field}</option>)}</NativeSelect>
    <NativeSelect aria-label="Comparison" value={value.operator} onChange={event => onChange({ ...value, operator: event.target.value, value: event.target.value === 'isMissing' ? undefined : value.value ?? '' })}>{[...new Set([value.operator, ...operators])].map(operator => <option key={operator}>{operator}</option>)}</NativeSelect>
    {value.operator !== 'isMissing' && (value.operator === 'in' ? <JsonField label="Allowed values (JSON array)" value={value.value ?? []} onChange={parsed => onChange({ ...value, value: parsed })} />
      : value.field === 'invoice.isDuplicate' || value.field === 'invoice.hasRiskConcern' ? <NativeSelect aria-label="Comparison value" value={String(value.value ?? '')} onChange={event => onChange({ ...value, value: event.target.value === 'true' })}><option value="" disabled>Choose a value</option><option value="true">True</option><option value="false">False</option></NativeSelect>
        : <Input aria-label="Comparison value" value={String(value.value ?? '')} onChange={event => onChange({ ...value, value: event.target.value })} />)}
  </div>;
}
export function DecisionConditionBuilder({ value, depth = 1, canAdd, fields, operators, onChange }: { value: DecisionPredicate; depth?: number; canAdd: boolean; fields: string[]; operators: string[]; onChange: (value: DecisionPredicate) => void }) {
  const children = value.children ?? [];
  return <div className="space-y-2 rounded-md border p-2">
    <NativeSelect aria-label={`Condition type at depth ${depth}`} value={value.kind} onChange={event => { const kind = event.target.value as DecisionPredicate['kind']; onChange(kind === 'Comparison' ? { kind, comparison: value.comparison ?? defaultComparison() } : { kind, children: kind === 'Not' ? [children[0] ?? { kind: 'Comparison', comparison: defaultComparison() }] : children.length ? children : [{ kind: 'Comparison', comparison: defaultComparison() }] }); }}>
      <option value="Comparison">Comparison</option><option value="All" disabled={depth >= 4 || !canAdd && value.kind === 'Comparison'}>All conditions</option><option value="Any" disabled={depth >= 4 || !canAdd && value.kind === 'Comparison'}>Any condition</option><option value="Not" disabled={depth >= 4 || !canAdd && value.kind === 'Comparison'}>Not</option>
    </NativeSelect>
    {value.kind === 'Comparison' ? <DecisionComparisonEditor value={value.comparison ?? defaultComparison()} fields={fields} operators={operators} onChange={comparison => onChange({ kind: 'Comparison', comparison })} /> : <>
      {children.map((child, index) => <div key={index} className="space-y-1"><DecisionConditionBuilder value={child} depth={depth + 1} canAdd={canAdd} fields={fields} operators={operators} onChange={updated => onChange({ ...value, children: children.map((item, position) => position === index ? updated : item) })} />
        {value.kind !== 'Not' && children.length > 1 && <Button size="sm" variant="outline" onClick={() => onChange({ ...value, children: children.filter((_, position) => position !== index) })}>Remove condition</Button>}
      </div>)}
      {value.kind !== 'Not' && <Button size="sm" variant="outline" disabled={!canAdd || depth >= 4} onClick={() => onChange({ ...value, children: [...children, { kind: 'Comparison', comparison: defaultComparison() }] })}>Add condition</Button>}
    </>}
  </div>;
}
