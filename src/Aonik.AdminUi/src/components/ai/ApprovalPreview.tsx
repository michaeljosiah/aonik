import { approvalChanges, type ToolApprovalPreview } from '@/lib/toolApprovalPreview';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';

export function ApprovalPreview({ preview }: { preview: ToolApprovalPreview }) {
  return <details className="mt-3 text-xs"><summary className="cursor-pointer font-medium text-foreground">Review exact changes</summary>
    <div className="mt-2 max-h-96 overflow-auto rounded border border-border"><Table>
      <TableHeader><TableRow><TableHead>Field</TableHead><TableHead>Before</TableHead><TableHead>After</TableHead></TableRow></TableHeader>
      <TableBody>{approvalChanges(preview.before, preview.after).map(change => <TableRow key={change.path} className="align-top">
        <TableCell className="font-mono">{change.path}</TableCell><TableCell><pre className="whitespace-pre-wrap break-words">{change.before}</pre></TableCell>
        <TableCell><pre className="whitespace-pre-wrap break-words">{change.after}</pre></TableCell>
      </TableRow>)}</TableBody>
    </Table></div>
  </details>;
}
