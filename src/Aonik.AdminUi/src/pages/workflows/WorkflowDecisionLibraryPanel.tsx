import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { NativeSelect } from '@/components/ui/native-select';
import { workflowService } from '@/services/workflowService';
import { WorkflowVersionPicker } from './WorkflowVersionPicker';
import type { DecisionFactField, DecisionLibraryContract, WorkflowInputBinding, WorkflowNodeParams } from './workflowTypes';

export function WorkflowDecisionLibraryPanel({ workflowId, parameters, onChange, onInline }: { workflowId: string; parameters: WorkflowNodeParams; onChange: (parameters: WorkflowNodeParams) => void; onInline: () => void }) {
  const [fields, setFields] = useState<DecisionFactField[]>([]);
  useEffect(() => { void workflowService.capabilities().then(value => setFields(value.invoiceFacts.map(field => ({ ...field, required: field.required ?? false })))).catch(() => setFields([])); }, []);
  const [selectedContract, setContract] = useState<{ versionId: string; contract: DecisionLibraryContract } | null>(null);
  const inputFields = selectedContract?.versionId === parameters.library?.versionId ? selectedContract!.contract.inputs
    : fields.filter(field => Object.hasOwn(parameters.inputs ?? {}, field.name)).map(field => ({ ...field, required: false }));
  const update = (name: string, binding: WorkflowInputBinding) => onChange({ ...parameters, inputs: { ...parameters.inputs, [name]: binding } });
  return <div className="space-y-3 text-xs">
    <p className="text-muted-foreground">This decision uses one immutable pure library. Selecting a newer version changes the draft and requires a new publication.</p>
    <WorkflowVersionPicker workflowId={workflowId} definitionKind="DecisionLibrary" pin={parameters.library ?? { versionId: '', definitionHash: '' }} onSelectedVersion={version => {
      if (!version.libraryContract || !version.definitionHash) return;
      setContract({ versionId: version.id, contract: version.libraryContract });
      onChange({ schemaVersion: 1, library: { versionId: version.id, definitionHash: version.definitionHash },
        inputs: Object.fromEntries(version.libraryContract.inputs.map(field => [field.name, parameters.inputs?.[field.name] ?? { fact: field.name }])),
        routes: version.libraryContract.routes, outputType: version.libraryContract.outputType });
    }} />
    {inputFields.map(field => {
      const binding = parameters.inputs?.[field.name] ?? { fact: field.name }; const literal = Object.hasOwn(binding, 'literal');
      return <fieldset key={field.name} className="space-y-2 rounded-md border p-2"><legend>{field.name} · {field.type}{field.required ? ' · required' : ''}</legend>
        <NativeSelect aria-label={`Source for ${field.name}`} value={literal ? 'literal' : 'fact'} onChange={event => update(field.name, event.target.value === 'literal' ? { literal: field.type === 'Boolean' ? false : '' } : { fact: field.name })}><option value="fact">Declared fact</option><option value="literal">Typed literal</option></NativeSelect>
        {!literal ? <NativeSelect aria-label={`Fact for ${field.name}`} value={binding.fact ?? field.name} onChange={event => update(field.name, { fact: event.target.value })}>{fields.filter(source => source.type === field.type).map(source => <option key={source.name}>{source.name}</option>)}</NativeSelect>
          : field.type === 'Boolean' ? <NativeSelect aria-label={`Value for ${field.name}`} value={String(binding.literal)} onChange={event => update(field.name, { literal: event.target.value === 'true' })}><option value="true">True</option><option value="false">False</option></NativeSelect>
            : <Input aria-label={`Value for ${field.name}`} value={String(binding.literal ?? '')} onChange={event => update(field.name, { literal: event.target.value })} />}
      </fieldset>;
    })}
    <p>Declared output: {parameters.outputType ?? 'Unselected'} · {(parameters.routes ?? []).join(', ')}</p>
    {parameters.outputType === 'InvoiceList' && <p className="text-muted-foreground">Wire the collected route directly to a foreach explicitly bound to this decision key. Rows produce typed references; they do not execute actions.</p>}
    <Button size="sm" variant="outline" onClick={onInline}>Replace with an inline table</Button>
  </div>;
}

export function WorkflowLibraryContractPanel({ parameters, onChange }: { parameters: WorkflowNodeParams; onChange: (parameters: WorkflowNodeParams) => void }) {
  const [fields, setFields] = useState<DecisionFactField[]>([]);
  useEffect(() => { void workflowService.capabilities().then(value => setFields(value.invoiceFacts.map(field => ({ ...field, required: field.required ?? false })))).catch(() => setFields([])); }, []);
  const contract = parameters.libraryContract ?? { schemaVersion: 1, inputs: [], routes: [], outputType: 'Route' };
  const update = (inputs: DecisionFactField[]) => onChange({ libraryContract: { ...contract, inputs } });
  return <div className="space-y-3 text-xs"><p>A library contains one pure table. Declare every fact it uses; consumers can map only these inputs.</p>
    {fields.map(field => { const selected = contract.inputs.find(input => input.name === field.name); return <div key={field.name} className="space-y-1 rounded-md border p-2">
      <label className="flex items-center gap-2"><Checkbox checked={Boolean(selected)} onCheckedChange={checked => update(checked ? [...contract.inputs, field] : contract.inputs.filter(input => input.name !== field.name))} />{field.name} · {field.type}</label>
      {selected && <label className="flex items-center gap-2"><Checkbox checked={selected.required} onCheckedChange={checked => update(contract.inputs.map(input => input.name === field.name ? { ...input, required: checked === true } : input))} />Required input</label>}
    </div>; })}
    <p className="text-muted-foreground">Missing required inputs stop evaluation. Optional inputs use the table's Unknown semantics. Output routes follow the table in this draft and are checked at publication.</p>
    <p>Output: {contract.outputType} · {contract.routes.join(', ')}</p>
  </div>;
}
