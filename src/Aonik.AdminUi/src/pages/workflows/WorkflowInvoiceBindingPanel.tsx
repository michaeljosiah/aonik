import { Input } from '@/components/ui/input';
import { NativeSelect } from '@/components/ui/native-select';
import type { WorkflowInputBinding } from './workflowTypes';

export function WorkflowInvoiceBindingPanel({ value, onChange }: { value: WorkflowInputBinding; onChange: (value: WorkflowInputBinding) => void }) {
  const mode = value.literal !== undefined ? 'literal' : value.step !== undefined ? 'output' : 'fact';
  return <div className="space-y-2 text-xs"><label className="block space-y-1">Child invoice<NativeSelect value={mode} onChange={event => onChange(event.target.value === 'literal' ? { literal: '' } : event.target.value === 'output' ? { step: '', output: '' } : { fact: 'invoice.id' })}><option value="fact">Current invoice ID</option><option value="literal">Fixed stored invoice ID</option><option value="output">Declared prior UUID output</option></NativeSelect></label>
    {mode === 'literal' && <label className="block space-y-1">Stored invoice ID<Input value={typeof value.literal === 'string' ? value.literal : ''} onChange={event => onChange({ literal: event.target.value })} /></label>}
    {mode === 'output' && <><label className="block space-y-1">Prior node key<Input value={value.step ?? ''} onChange={event => onChange({ step: event.target.value, output: value.output ?? '' })} /></label><label className="block space-y-1">UUID output field<Input value={value.output ?? ''} onChange={event => onChange({ step: value.step ?? '', output: event.target.value })} /></label></>}
  </div>;
}
