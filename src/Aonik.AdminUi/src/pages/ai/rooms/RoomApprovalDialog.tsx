import { useEffect, useState } from 'react';
import { Check, Loader2 } from 'lucide-react';
import { ApprovalPreview } from '@/components/ai/ApprovalPreview';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import type { ToolApprovalPreview } from '@/lib/toolApprovalPreview';
import type { RoomApproval } from '@/services/chatRoomService';
import { connectorError, connectorService } from '@/services/connectorService';

export function RoomApprovalDialog({ approval, choice, disabled, busy, error, onClose, onConfirm }: {
  approval: RoomApproval; choice: 'Approve' | 'Reject'; disabled: boolean; busy: boolean; error: string; onClose: () => void; onConfirm: () => void;
}) {
  const [preview, setPreview] = useState<ToolApprovalPreview | null>(null); const [reviewError, setReviewError] = useState('');
  const mailbox = approval.tool.startsWith('gmail_');
  useEffect(() => {
    if (!mailbox) return;
    let disposed = false;
    void connectorService.mutationReview(approval.id).then(value => { if (!disposed) setPreview(value); })
      .catch(reason => { if (!disposed) setReviewError(connectorError(reason)); });
    return () => { disposed = true; };
  }, [approval.id, mailbox]);
  return <Dialog open onOpenChange={open => { if (!open) onClose(); }}><DialogContent><DialogHeader>
    <DialogTitle>{choice === 'Approve' ? 'Confirm this proposed action' : 'Reject this proposed action'}</DialogTitle>
    <DialogDescription>Review the exact request. Your decision is checked by the server; a room role does not grant financial or mailbox authority.</DialogDescription>
  </DialogHeader><div className="space-y-3"><strong className="text-sm">{approval.actionKind}</strong>
    {mailbox && !preview && !reviewError && <p className="text-xs text-muted-foreground">Checking your mailbox access and loading the private reviewed effect…</p>}
    {preview && <ApprovalPreview preview={preview} />}
    <pre className="max-h-72 overflow-auto rounded-lg border bg-muted p-3 font-mono text-xs whitespace-pre-wrap break-words">{approval.argumentsJson}</pre>
    <p className="text-xs text-muted-foreground">{approval.tier === 'High' ? 'High-risk action: the durable proposal dispatcher owns execution.' : 'Medium-risk action: approval authorises only the exact recorded arguments, once.'}</p>
    {(reviewError || error) && <p role="alert" className="text-sm text-destructive">{reviewError || error}</p>}
  </div><DialogFooter><Button variant="outline" onClick={onClose}>Cancel</Button><Button variant={choice === 'Approve' ? 'agent' : 'destructive'} disabled={disabled || busy || mailbox && !preview} onClick={onConfirm}>
    {busy ? <Loader2 className="animate-spin" /> : <Check />}Confirm {choice.toLowerCase()}
  </Button></DialogFooter></DialogContent></Dialog>;
}
