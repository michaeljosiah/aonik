import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { NativeSelect } from '@/components/ui/native-select';
import { Checkbox } from '@/components/ui/checkbox';
import { humanTaskParameters } from './decisionParameters';
import type { WorkflowNodeParams } from './workflowTypes';

export function WorkflowHumanPanel({ parameters, onChange }: { parameters: WorkflowNodeParams; onChange: (value: WorkflowNodeParams) => void }) {
  if (!parameters.choices || !parameters.reviewer) return <div className="space-y-2 text-xs"><p>Choose a typed response contract before publication.</p><Button variant="outline" size="sm" onClick={() => onChange(humanTaskParameters())}>Configure a human task</Button></div>;
  const named = parameters.reviewer.userId !== undefined;
  const change = (patch: Partial<WorkflowNodeParams>) => onChange({ ...parameters, ...patch });
  return <div className="space-y-3 text-xs">
    <p className="text-muted-foreground">Responses select a route. Financial actions still require their own approval.</p>
    <label className="block space-y-1">Purpose<Input maxLength={240} value={parameters.purpose ?? ''} onChange={event => change({ purpose: event.target.value })} /></label>
    <label className="block space-y-1">Reviewer<NativeSelect value={named ? 'user' : 'role'} onChange={event => change({ reviewer: event.target.value === 'user' ? { userId: '', forbidSelfApproval: true } : { roleId: '', forbidSelfApproval: true } })}><option value="role">Tenant role</option><option value="user">Named tenant user</option></NativeSelect></label>
    <label className="block space-y-1">{named ? 'User ID' : 'Role ID'}<Input value={(named ? parameters.reviewer.userId : parameters.reviewer.roleId) ?? ''} onChange={event => change({ reviewer: { ...(named ? { userId: event.target.value } : { roleId: event.target.value }), forbidSelfApproval: parameters.reviewer?.forbidSelfApproval ?? true } })} /></label>
    <label className="flex items-center gap-2"><Checkbox checked={parameters.reviewer.forbidSelfApproval} onCheckedChange={value => change({ reviewer: { ...parameters.reviewer!, forbidSelfApproval: value === true } })} />Require a separate reviewer</label>
    <label className="block space-y-1">Response window (seconds)<Input type="number" min={1} max={604800} step={1} value={parameters.timeoutSeconds ?? 3600} onChange={event => change({ timeoutSeconds: Number(event.target.value) })} /></label>
    {parameters.choices.map((choice, index) => <fieldset key={index} className="space-y-2 rounded-md border p-2"><legend>Choice {index + 1}</legend>{(['key', 'label', 'route'] as const).map(field => <label key={field} className="block space-y-1">{field}<Input aria-label={`Choice ${index + 1} ${field}`} value={choice[field]} onChange={event => change({ choices: parameters.choices!.map((item, position) => position === index ? { ...item, [field]: event.target.value } : item) })} /></label>)}<Button size="sm" variant="outline" disabled={parameters.choices!.length <= 1} onClick={() => change({ choices: parameters.choices!.filter((_, position) => position !== index) })}>Remove choice</Button></fieldset>)}
    <Button size="sm" variant="outline" disabled={parameters.choices.length >= 10} onClick={() => change({ choices: [...parameters.choices!, { key: `choice${parameters.choices!.length + 1}`, label: 'New choice', route: `route${parameters.choices!.length + 1}` }] })}>Add choice</Button>
    <label className="block space-y-1">Rejection route<Input value={parameters.rejectRoute ?? ''} onChange={event => change({ rejectRoute: event.target.value })} /></label>
    <label className="block space-y-1">Timeout route<Input value={parameters.timeoutRoute ?? ''} onChange={event => change({ timeoutRoute: event.target.value })} /></label>
  </div>;
}
