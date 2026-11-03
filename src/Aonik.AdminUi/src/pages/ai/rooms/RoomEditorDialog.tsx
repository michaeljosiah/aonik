import { useEffect, useState } from 'react';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { agentDisplayName } from '@/lib/agentIdentity';
import { chatRoomService, type RoomAgent, type RoomDetail, type RoomPerson } from '@/services/chatRoomService';
import { roomError } from './roomPresentation';

export function RoomEditorDialog({ kind, room, agents, onClose, onCreated, onChanged }: { kind: 'create' | 'person' | 'agent'; room: RoomDetail | null; agents: RoomAgent[]; onClose: () => void; onCreated: (id: string) => void; onChanged: () => Promise<void> }) {
  const [title, setTitle] = useState('');
  const [referenceType, setReferenceType] = useState('Invoice');
  const [reference, setReference] = useState('');
  const [search, setSearch] = useState('');
  const [people, setPeople] = useState<RoomPerson[]>([]);
  const [candidate, setCandidate] = useState('');
  const [role, setRole] = useState('Participant');
  const [shareHistory, setShareHistory] = useState(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');
  useEffect(() => {
    if (kind !== 'person') return;
    const controller = new AbortController();
    const timer = window.setTimeout(() => void chatRoomService.people(search, controller.signal).then(setPeople).catch(failure => { if (!controller.signal.aborted) setError(roomError(failure)); }), 250);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [kind, search]);
  const submit = async () => {
    if (pending) return;
    setPending(true); setError('');
    try {
      if (kind === 'create') {
        const created = await chatRoomService.create(title.trim(), reference.trim() ? [{ type: referenceType, reference: reference.trim() }] : []);
        onCreated(created.room.id);
      } else if (room) {
        await chatRoomService.add(room.room.id, candidate, kind === 'agent' ? 'Agent' : role, shareHistory);
        await onChanged();
      }
      onClose();
    } catch (failure) { setError(roomError(failure)); }
    finally { setPending(false); }
  };
  return <Dialog open onOpenChange={open => !open && !pending && onClose()}><DialogContent><DialogHeader><DialogTitle>{kind === 'create' ? 'Create a shared room' : kind === 'person' ? 'Invite a colleague' : 'Add an agent'}</DialogTitle><DialogDescription>{kind === 'create' ? 'Start a new conversation. Your private chat history will not be copied.' : 'Participants receive access to this room’s existing history. Workspace and domain permissions still apply.'}</DialogDescription></DialogHeader><div className="space-y-4">
    {kind === 'create' ? <><div className="space-y-2"><Label htmlFor="room-title">Room name</Label><Input id="room-title" value={title} onChange={event => setTitle(event.target.value)} maxLength={160} placeholder="November collections review" /></div><div className="space-y-2"><Label>Optional linked record</Label><div className="flex gap-2"><Select value={referenceType} onValueChange={setReferenceType}><SelectTrigger className="w-40" aria-label="Record type"><SelectValue /></SelectTrigger><SelectContent>{['Invoice', 'Order', 'PaymentIntent', 'LedgerEntry', 'Proposal', 'Customer'].map(type => <SelectItem key={type} value={type}>{type}</SelectItem>)}</SelectContent></Select><Input aria-label="Record reference" value={reference} onChange={event => setReference(event.target.value)} placeholder="INV-2048" maxLength={100} /></div><p className="text-xs text-muted-foreground">Shares only the reference you enter. Agents must verify records through their authorised tools.</p></div></> : <>{kind === 'person' && <Input aria-label="Find workspace colleague" placeholder="Find a colleague by email" value={search} onChange={event => setSearch(event.target.value)} />}<Select value={candidate} onValueChange={setCandidate}><SelectTrigger aria-label={kind === 'person' ? 'Choose colleague' : 'Choose agent'}><SelectValue placeholder={kind === 'person' ? 'Select an active colleague' : 'Select an available agent'} /></SelectTrigger><SelectContent>{kind === 'person' ? people.filter(person => !room?.participants.some(member => member.userId === person.userId)).map(person => <SelectItem key={person.userId} value={`human:${person.userId}`}>{person.displayName}</SelectItem>) : agents.filter(agent => !room?.participants.some(member => member.agentName === agent.name)).map(agent => <SelectItem key={agent.name} value={`agent:${agent.name}`}>{agentDisplayName(agent.name)}</SelectItem>)}</SelectContent></Select>{kind === 'person' && <Select value={role} onValueChange={setRole}><SelectTrigger aria-label="Room role"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="Participant">Participant · read and contribute</SelectItem><SelectItem value="Observer">Observer · read only</SelectItem></SelectContent></Select>}<div className="flex items-start gap-2"><Checkbox id="share-room-history" checked={shareHistory} onCheckedChange={value => setShareHistory(value === true)} /><Label htmlFor="share-room-history" className="text-sm leading-5">I confirm that this participant can read the room’s existing messages and linked references.</Label></div></>}
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
  </div><DialogFooter><Button variant="outline" disabled={pending} onClick={onClose}>Cancel</Button><Button disabled={pending || (kind === 'create' ? !title.trim() : !candidate || !shareHistory)} onClick={() => void submit()}>{pending && <Loader2 className="animate-spin" />}{kind === 'create' ? 'Create room' : 'Add participant'}</Button></DialogFooter></DialogContent></Dialog>;
}
