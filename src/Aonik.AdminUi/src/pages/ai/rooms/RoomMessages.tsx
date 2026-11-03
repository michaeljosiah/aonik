import { ChatMarkdown } from '@/components/ai/ChatMessageList';
import { Loader2, ShieldCheck } from 'lucide-react';
import { AgentAvatar } from '@/components/ai/AgentAvatar';
import { ServerApprovalCard, type ServerApprovalState } from '@/components/ai/chatSupport';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { agentDisplayName } from '@/lib/agentIdentity';
import { cn } from '@/lib/utils';
import type { RoomApproval, RoomDetail, RoomMessage, RoomTurn } from '@/services/chatRoomService';
import { HumanAvatar } from '@/components/ai/HumanAvatar';

export { HumanAvatar };
export function RoomMessageView({ message, room }: { message: RoomMessage; room: RoomDetail }) {
  if (message.actorKind === 'System') return <div className="flex items-center gap-2 py-1 text-xs text-muted-foreground"><ShieldCheck className="size-3.5 shrink-0" /><span>{message.content}</span></div>;
  return <article className="flex gap-3" data-room-message={message.id}>
    {message.agentName ? <AgentAvatar name={message.agentName} /> : <HumanAvatar name={message.authorName} />}
    <div className="min-w-0 flex-1"><div className="mb-1.5 flex flex-wrap items-center gap-2 text-xs"><strong>{message.agentName ? agentDisplayName(message.agentName) : message.authorName}</strong><Badge variant="outline" className="text-[10px]">{message.actorKind === 'Agent' ? 'AI agent' : 'Human'}</Badge><time className="text-muted-foreground" dateTime={message.createdAt}>{new Date(message.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</time></div>
      <div className={cn('space-y-2 text-sm leading-6 [&_a]:text-primary [&_pre]:overflow-auto [&_pre]:rounded-md [&_pre]:bg-muted [&_pre]:p-3 [&_ul]:list-disc [&_ul]:pl-5', message.authorUserId === room.currentUserId && 'rounded-lg bg-primary/10 p-3')}><ChatMarkdown text={message.content} /></div>
      {message.mentionKeys.length > 0 && <div className="mt-2 flex flex-wrap gap-1">{message.mentionKeys.map(key => { const person = room.participants.find(item => item.actorKey === key); return <Badge key={key} variant="secondary">@{person?.agentName ? agentDisplayName(person.agentName) : person?.displayName ?? 'Former participant'}</Badge>; })}</div>}
      {message.aiRunId && <p className="mt-2 font-mono text-[10px] text-muted-foreground">AI run {message.aiRunId}</p>}
    </div>
  </article>;
}
function presentation(approval: RoomApproval): ServerApprovalState {
  return { id: approval.id, kind: approval.tier === 'High' ? 'high' : 'medium', approvalRequestId: approval.id, proposalId: approval.proposalId ?? undefined, tool: approval.tool, tier: approval.tier, actionKind: approval.actionKind,
    status: approval.status === 'Approved' ? 'approved' : approval.status === 'Rejected' ? 'rejected' : approval.status === 'Expired' ? 'error' : 'pending',
    message: approval.status === 'Expired' ? 'This request expired. Ask the agent to prepare it again.' : approval.status === 'Approved' ? 'Human approval recorded. Execution remains owned by the system; check the underlying records.' : undefined };
}
export function RoomTurnView({ turn, owner, pending, canUseAgents, onDecide, onContinue, onClose }: { turn: RoomTurn; owner: boolean; pending: boolean; canUseAgents: boolean; onDecide: (approval: RoomApproval, choice: 'Approve' | 'Reject') => void; onContinue: (approval: RoomApproval) => void; onClose: () => void }) {
  return <div className="ml-11 space-y-3" data-turn-id={turn.id}>
    {turn.status === 'Running' && <div className="flex items-center gap-2 rounded-lg border bg-muted/40 p-3 text-sm text-muted-foreground"><Loader2 className="size-4 animate-spin" />{agentDisplayName(turn.agentName)} is responding…</div>}
    {turn.error && <div className="rounded-lg border border-warning/30 bg-warning-subtle p-3 text-sm text-warning-foreground">{turn.error}</div>}
    {owner && turn.canClose && <Button variant="outline" size="sm" disabled={pending} onClick={onClose}>Close interrupted run without retrying</Button>}
    {turn.approvals.map(approval => <section key={approval.id} className="space-y-2" aria-label={`Approval ${approval.id}`}>
      <ServerApprovalCard approval={presentation(approval)} onDecide={approval.canDecide && canUseAgents && !pending ? (_, choice) => onDecide(approval, choice) : undefined} />
      <details className="rounded-md border p-3 text-xs"><summary className="cursor-pointer font-medium">Review proposed arguments and references</summary><pre className="mt-2 max-h-52 overflow-auto whitespace-pre-wrap break-words font-mono">{approval.argumentsJson}</pre><p className="mt-2 text-muted-foreground">Approval {approval.id}{approval.proposalId ? ` · Proposal ${approval.proposalId}` : ''}</p></details>
      {!approval.canDecide && approval.status === 'Pending' && <p className="text-xs text-muted-foreground">Awaiting the requesting human’s decision. Room roles do not grant approval authority.</p>}
      {approval.canContinue && canUseAgents && <Button size="sm" variant="outline" disabled={pending} onClick={() => onContinue(approval)}>Continue this approved action</Button>}
    </section>)}
  </div>;
}
