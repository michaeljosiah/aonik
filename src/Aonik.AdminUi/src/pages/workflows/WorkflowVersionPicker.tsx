import { useEffect, useState } from 'react';
import { NativeSelect } from '@/components/ui/native-select';
import { workflowService, type WorkflowSummaryDto, type WorkflowVersionDto } from '@/services/workflowService';
import { connectorError } from '@/services/connectorService';
import type { WorkflowVersionPin } from './workflowTypes';

export function WorkflowVersionPicker({ workflowId, pin, onChange }: { workflowId: string; pin: WorkflowVersionPin; onChange: (pin: WorkflowVersionPin) => void }) {
  const [workflows, setWorkflows] = useState<WorkflowSummaryDto[]>([]);
  const [selected, setSelected] = useState('');
  const [versions, setVersions] = useState<WorkflowVersionDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => { let active = true; void workflowService.list(0, 100).then(items => { if (active) setWorkflows(items.filter(item => item.state === 'Active' && item.id !== workflowId)); }).catch(reason => { if (active) setError(connectorError(reason)); }); return () => { active = false; }; }, [workflowId]);
  useEffect(() => { let active = true; if (selected) void workflowService.listVersions(selected).then(items => { if (active) { setVersions(items.filter(item => item.publishedAt && item.definitionHash && item.isExecutable)); setError(null); } }).catch(reason => { if (active) setError(connectorError(reason)); }); return () => { active = false; }; }, [selected]);
  return <div className="space-y-2 text-xs">
    {error && <p role="alert" className="text-destructive">{error}</p>}
    <label className="block space-y-1">Published workflow<NativeSelect value={selected} onChange={event => { setSelected(event.target.value); setVersions([]); }}><option value="">Choose a workflow to select a version</option>{workflows.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</NativeSelect></label>
    <label className="block space-y-1">Exact version<NativeSelect value={pin.versionId} onChange={event => { const version = versions.find(item => item.id === event.target.value); if (version?.definitionHash) onChange({ versionId: version.id, definitionHash: version.definitionHash }); }}><option value="">Choose a published version</option>{pin.versionId && !versions.some(item => item.id === pin.versionId) && <option value={pin.versionId}>Current pin · {pin.versionId}</option>}{versions.map(item => <option key={item.id} value={item.id}>{item.tag} · {item.id}</option>)}</NativeSelect></label>
    {pin.definitionHash && <p className="break-all text-xs text-muted-foreground">Pinned definition: {pin.definitionHash}</p>}
    <p className="text-muted-foreground">New publications never replace this pin. Publication validates the full dependency tree.</p>
  </div>;
}
