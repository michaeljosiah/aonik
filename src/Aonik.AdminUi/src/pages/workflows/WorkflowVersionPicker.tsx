import { useEffect, useState } from 'react';
import { NativeSelect } from '@/components/ui/native-select';
import { Button } from '@/components/ui/button';
import { workflowService, type WorkflowSummaryDto, type WorkflowVersionDto } from '@/services/workflowService';
import { connectorError } from '@/services/connectorService';
import type { WorkflowVersionPin } from './workflowTypes';

export function WorkflowVersionPicker({ workflowId, pin, onChange, definitionKind = 'Workflow', onSelectedVersion }: { workflowId: string; pin: WorkflowVersionPin; onChange?: (pin: WorkflowVersionPin) => void; definitionKind?: 'Workflow' | 'DecisionLibrary'; onSelectedVersion?: (version: WorkflowVersionDto) => void }) {
  const [workflows, setWorkflows] = useState<WorkflowSummaryDto[]>([]);
  const [selected, setSelected] = useState('');
  const [versions, setVersions] = useState<WorkflowVersionDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [offset, setOffset] = useState(0); const [more, setMore] = useState(false); const [loading, setLoading] = useState(false);
  useEffect(() => { let active = true; void workflowService.list(0, 100).then(items => { if (active) { setWorkflows(items); setOffset(items.length); setMore(items.length === 100); } }).catch(reason => { if (active) setError(connectorError(reason)); }); return () => { active = false; }; }, [workflowId]);
  useEffect(() => { let active = true; if (selected) void workflowService.listVersions(selected).then(items => { if (active) { setVersions(items.filter(item => item.publishedAt && item.definitionHash && item.isExecutable)); setError(null); } }).catch(reason => { if (active) setError(connectorError(reason)); }); return () => { active = false; }; }, [selected]);
  return <div className="space-y-2 text-xs">
    {error && <p role="alert" className="text-destructive">{error}</p>}
    <label className="block space-y-1">Published {definitionKind === 'DecisionLibrary' ? 'decision library' : 'workflow'}<NativeSelect value={selected} onChange={event => { setSelected(event.target.value); setVersions([]); }}><option value="">Choose a definition to select a version</option>{workflows.filter(item => item.state === 'Active' && item.id !== workflowId && (item.definitionKind ?? 'Workflow') === definitionKind).map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</NativeSelect></label>
    {more && <Button size="sm" variant="outline" disabled={loading} onClick={() => { setLoading(true); void workflowService.list(offset, 100).then(items => { setWorkflows(previous => [...previous, ...items]); setOffset(offset + items.length); setMore(items.length === 100); setError(null); }).catch(reason => setError(connectorError(reason))).finally(() => setLoading(false)); }}>Load more definitions</Button>}
    <label className="block space-y-1">Exact version<NativeSelect value={pin.versionId} onChange={event => { const version = versions.find(item => item.id === event.target.value); if (version?.definitionHash) { if (onSelectedVersion) onSelectedVersion(version); else onChange?.({ versionId: version.id, definitionHash: version.definitionHash }); } }}><option value="">Choose a published version</option>{pin.versionId && !versions.some(item => item.id === pin.versionId) && <option value={pin.versionId}>Current pin · {pin.versionId}</option>}{versions.filter(version => definitionKind !== 'DecisionLibrary' || version.libraryContract).map(item => <option key={item.id} value={item.id}>{item.tag} · {item.id}</option>)}</NativeSelect></label>
    {pin.definitionHash && <p className="break-all text-xs text-muted-foreground">Pinned definition: {pin.definitionHash}</p>}
    <p className="text-muted-foreground">New publications never replace this pin. Publication validates the full dependency tree.</p>
  </div>;
}
