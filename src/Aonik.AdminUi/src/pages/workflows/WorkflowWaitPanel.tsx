import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { NativeSelect } from '@/components/ui/native-select';
import type { WorkflowNodeParams } from './workflowTypes';

export function WorkflowWaitPanel({ parameters, onChange }: { parameters: WorkflowNodeParams; onChange: (value: WorkflowNodeParams) => void }) {
  const absolute = Boolean(parameters.untilUtc);
  const valid = parameters.schemaVersion === 1 && (absolute || Number.isInteger(parameters.delaySeconds));
  return <div className="space-y-3 text-xs">
    <p className="text-muted-foreground">The recorded deadline survives restarts. Later actions keep their own approval requirements. The overall run is limited to seven days.</p>
    {!valid && <div className="space-y-2 rounded-md border p-2"><p>This saved wait needs a typed duration or UTC deadline before publication.</p><Button variant="outline" size="sm" onClick={() => onChange({ schemaVersion: 1, delaySeconds: 60, untilUtc: null })}>Use a 60-second delay</Button></div>}
    <label className="block space-y-1">Wait mode<NativeSelect value={absolute ? 'until' : 'delay'} onChange={event => onChange(event.target.value === 'until'
      ? { schemaVersion: 1, delaySeconds: null, untilUtc: new Date(Date.now() + 60000).toISOString() }
      : { schemaVersion: 1, delaySeconds: 60, untilUtc: null })}><option value="delay">Duration from this step's recorded start</option><option value="until">Fixed UTC deadline</option></NativeSelect></label>
    {absolute ? <label className="block space-y-1">Deadline (UTC ISO date)<Input value={parameters.untilUtc ?? ''} placeholder="2026-11-15T09:00:00Z" onChange={event => onChange({ schemaVersion: 1, delaySeconds: null, untilUtc: event.target.value })} /></label>
      : <label className="block space-y-1">Delay in seconds<Input type="number" min={1} max={604800} step={1} value={parameters.delaySeconds ?? ''} onChange={event => onChange({ schemaVersion: 1, delaySeconds: Number(event.target.value), untilUtc: null })} /></label>}
  </div>;
}
