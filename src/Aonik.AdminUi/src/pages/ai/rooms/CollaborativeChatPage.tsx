import { useCallback, useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { ArrowLeft, AtSign, Check, Hash, Link, Loader2, LockKeyhole, Plus, Search, Send, Users, X } from 'lucide-react';
import { AgentAvatar } from '@/components/ai/AgentAvatar';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Sheet, SheetBody, SheetContent, SheetHeader } from '@/components/ui/sheet';
import { Textarea } from '@/components/ui/textarea';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useModules } from '@/modules/useModules';
import { agentDisplayName } from '@/lib/agentIdentity';
import { cn } from '@/lib/utils';
import { chatRoomService, type RoomAgent, type RoomApproval, type RoomDetail, type RoomParticipant, type RoomRunRequest, type RoomSendRequest, type RoomSummary, type RoomTurn } from '@/services/chatRoomService';
import { mergeRoomMessages, roomError } from './roomPresentation';
import { RoomMessageView, RoomTurnView } from './RoomMessages';
import { RoomParticipants } from './RoomParticipants';
import { RoomEditorDialog } from './RoomEditorDialog';

export function CollaborativeChatPage({ onOpenPrivate }: { onOpenPrivate: () => void }) {
  const [searchParams, setSearchParams] = useSearchParams();
  const activeId = searchParams.get('room') ?? '';
  const linkedMessage = /^[a-f0-9-]{36}$/i.test(searchParams.get('message') ?? '') ? searchParams.get('message')! : '';
  const linkedSequence = Number(searchParams.get('sequence'));
  const validLinkedSequence = Number.isSafeInteger(linkedSequence) && linkedSequence > 0 && linkedSequence < Number.MAX_SAFE_INTEGER ? linkedSequence : 0;
  const focusedMessage = useRef('');
  const { allowsPolicy } = useModules();
  const canUseAgents = allowsPolicy('AdminWritePolicy');
  const [rooms, setRooms] = useState<RoomSummary[]>([]);
  const [detail, setDetail] = useState<RoomDetail | null>(null);
  const [agents, setAgents] = useState<RoomAgent[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [pending, setPending] = useState(false);
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [target, setTarget] = useState('people');
  const [mentions, setMentions] = useState<string[]>([]);
  const [roomSearch, setRoomSearch] = useState('');
  const [messageSearch, setMessageSearch] = useState('');
  const [showPeople, setShowPeople] = useState(true);
  const [peopleDrawer, setPeopleDrawer] = useState(false);
  const [modal, setModal] = useState<'create' | 'person' | 'agent' | null>(null);
  const [remove, setRemove] = useState<RoomParticipant | null>(null);
  const [decision, setDecision] = useState<{ roomId: string; turn: RoomTurn; approval: RoomApproval; choice: 'Approve' | 'Reject' } | null>(null);
  const recovery = useRef<{ roomId: string; request: RoomSendRequest; target: string; runId: string } | null>(null);
  const runReferences = useRef(new Map<string, string>());
  const alive = useRef(true);
  const currentId = useRef(activeId); currentId.current = activeId;
  const bottom = useRef<HTMLDivElement>(null);
  const room = detail?.room.id === activeId ? detail : null;
  const draft = drafts[activeId] ?? '';
  const owner = room?.room.role === 'Owner';
  const writable = room && room.room.role !== 'Observer';
  const activeTurns = room?.turns.filter(turn => turn.status === 'Running') ?? [];

  useEffect(() => { alive.current = true; return () => { alive.current = false; }; }, []);
  const refresh = useCallback(async () => {
    const id = currentId.current;
    const updatedRooms = await chatRoomService.list();
    if (!alive.current) return;
    setRooms(updatedRooms);
    if (!id) return;
    const next = await chatRoomService.get(id);
    if (alive.current && currentId.current === id) setDetail(previous => previous?.room.id === id ? { ...next, turns: [...new Map([...previous.turns, ...next.turns].map(turn => [turn.id, turn])).values()], messages: mergeRoomMessages(previous.messages, next.messages), hasEarlier: previous.hasEarlier && next.hasEarlier } : next);
  }, []);
  useEffect(() => {
    let live = true, running = false, loadedLinkedPage = false;
    const controller = new AbortController();
    setLoading(true); setError(''); setDetail(null); setTarget('people'); setMentions([]); setMessageSearch(''); setModal(null); setDecision(null); setRemove(null); setPeopleDrawer(false); setNotice('');
    const update = async () => {
      if (running || document.visibilityState === 'hidden') return;
      running = true;
      try {
        const summaries = await chatRoomService.list(controller.signal);
        if (!live) return;
        setRooms(summaries);
        if (activeId) {
          const next = await chatRoomService.get(activeId, undefined, controller.signal);
          if (!loadedLinkedPage && linkedMessage && validLinkedSequence && !next.messages.some(message => message.id === linkedMessage)) {
            const earlier = await chatRoomService.get(activeId, validLinkedSequence + 1, controller.signal);
            next.messages = mergeRoomMessages(earlier.messages, next.messages);
            next.turns = [...new Map([...earlier.turns, ...next.turns].map(turn => [turn.id, turn])).values()];
            next.hasEarlier = earlier.hasEarlier;
          }
          loadedLinkedPage = true;
          if (!live) return;
          setDetail(previous => previous?.room.id === activeId ? { ...next, turns: [...new Map([...previous.turns, ...next.turns].map(turn => [turn.id, turn])).values()], messages: mergeRoomMessages(previous.messages, next.messages), hasEarlier: previous.hasEarlier && next.hasEarlier } : next);
        }
        if (live) setError('');
      } catch (failure) {
        if (!live) return;
        const status = (failure as { response?: { status?: number } }).response?.status;
        if (status === 403 || status === 404) { setDetail(null); setDecision(null); setRemove(null); setModal(null); setPeopleDrawer(false); }
        setError(roomError(failure));
      } finally { if (live) setLoading(false); running = false; }
    };
    void update();
    const interval = window.setInterval(() => void update(), 3000);
    const heartbeat = () => { if (activeId && document.visibilityState === 'visible') void chatRoomService.heartbeat(activeId).catch(() => {}); };
    heartbeat(); const presence = window.setInterval(heartbeat, 20000);
    return () => { live = false; controller.abort(); clearInterval(interval); clearInterval(presence); };
  }, [activeId, linkedMessage, validLinkedSequence]);
  useEffect(() => { const controller = new AbortController(); void chatRoomService.agents(controller.signal).then(setAgents).catch(failure => { if (!controller.signal.aborted) setError(roomError(failure)); }); return () => controller.abort(); }, []);
  useEffect(() => {
    if (linkedMessage) {
      const key = `${activeId}:${linkedMessage}`;
      const element = document.querySelector<HTMLElement>(`[data-notification-target="${linkedMessage}"]`);
      if (element && focusedMessage.current !== key) {
        focusedMessage.current = key;
        element.scrollIntoView({ behavior: 'smooth', block: 'center' });
        element.focus({ preventScroll: true });
      }
    } else {
      focusedMessage.current = '';
      bottom.current?.scrollIntoView({ behavior: 'smooth', block: 'end' });
    }
  }, [activeId, linkedMessage, room?.messages.length]);

  const openRoom = (id: string) => setSearchParams({ view: 'rooms', room: id });
  const act = async (operation: () => Promise<unknown>) => {
    if (pending) return;
    setPending(true); setError('');
    try { await operation(); if (alive.current) await refresh(); }
    catch (failure) { if (alive.current) setError(roomError(failure)); }
    finally { if (alive.current) setPending(false); }
  };
  const runAgent = async (id: string, request: RoomRunRequest) => {
    const key = `${id}:${request.promptMessageId}:${request.agentName}:${request.continueApprovalId ?? ''}`;
    const clientRequestId = runReferences.current.get(key) ?? request.clientRequestId;
    runReferences.current.set(key, clientRequestId);
    if (currentId.current === id) setNotice('Requesting an agent response…');
    await chatRoomService.run(id, { ...request, clientRequestId });
    if (alive.current && currentId.current === id) setNotice('The agent request finished. Review the response and any approval cards.');
  };
  const send = async () => {
    if (!room || !writable || !draft.trim() || pending) return;
    const id = room.room.id;
    const mentionKeys = [...new Set([...mentions, ...(target !== 'people' ? [`agent:${target}`] : [])])].sort();
    const previous = recovery.current;
    const payload = previous?.roomId === id && previous.request.content === draft.trim() && previous.target === target && JSON.stringify(previous.request.mentionKeys) === JSON.stringify(mentionKeys) ? previous : {
      roomId: id, request: { clientRequestId: crypto.randomUUID(), content: draft.trim(), mentionKeys }, target, runId: crypto.randomUUID(),
    };
    recovery.current = payload;
    setPending(true); setError('');
    try {
      const message = await chatRoomService.send(id, payload.request);
      if (alive.current && currentId.current === id) { setDrafts(values => ({ ...values, [id]: '' })); setMentions([]); await refresh().catch(() => {}); }
      if (payload.target !== 'people') await runAgent(id, { clientRequestId: payload.runId, promptMessageId: message.id, agentName: payload.target });
      recovery.current = null;
      if (alive.current) await refresh();
    } catch (failure) { if (alive.current) { setError(roomError(failure)); await refresh().catch(() => {}); } }
    finally { if (alive.current) setPending(false); }
  };
  const loadEarlier = (before?: number) => void act(async () => {
    if (!room) return;
    const id = room.room.id;
    const earlier = await chatRoomService.get(id, before ?? room.messages[0]?.sequence);
    if (currentId.current === id) setDetail(previous => previous?.room.id === id ? { ...previous, messages: mergeRoomMessages(earlier.messages, previous.messages), turns: [...new Map([...earlier.turns, ...previous.turns].map(turn => [turn.id, turn])).values()], hasEarlier: before === undefined ? earlier.hasEarlier : previous.hasEarlier } : previous);
  });

  return <div className="flex h-full min-h-0 bg-background" data-component="collaborative-chat">
    <aside className="flex w-60 shrink-0 flex-col border-r bg-sidebar max-md:w-44">
      <div className="flex items-center gap-2 border-b px-4 py-4"><AgentAvatar name="aonik" className="size-7" /><strong className="text-sm">People & agents</strong></div>
      <div className="space-y-3 p-3"><Button variant="ghost" size="sm" className="w-full justify-start" onClick={onOpenPrivate}><ArrowLeft />Private chats</Button><Button variant="outline" size="sm" className="w-full justify-start" onClick={() => setModal('create')}><Plus />New shared room</Button><div className="relative"><Search className="absolute left-2.5 top-2.5 size-3.5 text-muted-foreground" /><Input aria-label="Find a room" placeholder="Find a room" value={roomSearch} onChange={event => setRoomSearch(event.target.value)} className="h-9 pl-8" /></div></div>
      <p className="px-4 pb-2 text-xs font-medium text-muted-foreground">SHARED ROOMS</p><nav aria-label="Shared rooms" className="flex-1 space-y-1 overflow-y-auto px-2">{rooms.filter(item => item.title.toLowerCase().includes(roomSearch.toLowerCase())).map(item => <button key={item.id} onClick={() => openRoom(item.id)} className={cn('flex w-full gap-2 rounded-lg px-3 py-3 text-left hover:bg-accent', activeId === item.id && 'bg-accent')} aria-current={activeId === item.id ? 'page' : undefined}><Hash className="mt-0.5 size-4 shrink-0 text-muted-foreground" /><span className="min-w-0"><span className="block truncate text-sm font-medium">{item.title}</span><span className="mt-1 block text-xs text-muted-foreground">{item.humanCount} people · {item.agentCount} agents</span></span></button>)}{!loading && rooms.length === 0 && <p className="p-3 text-xs text-muted-foreground">Create a room to work together.</p>}</nav>
      <p className="flex gap-2 border-t p-4 text-xs leading-5 text-muted-foreground"><LockKeyhole className="mt-0.5 size-3.5 shrink-0" />Only participants can read a room. Private chats stay separate.</p>
    </aside>
    <section className="flex min-w-0 flex-1 flex-col">
      <header className="flex min-h-16 items-center gap-3 border-b px-5"><Hash className="size-5 text-muted-foreground" /><div className="min-w-0 flex-1"><h1 className="truncate font-semibold">{room?.room.title ?? 'Shared conversations'}</h1><p className="text-xs text-muted-foreground">{room ? `${room.room.humanCount} people · ${room.room.agentCount} agents · Visible to everyone in this room` : 'Work with your team and scoped AI agents'}</p></div>{room && <><Button variant="ghost" size="icon-sm" aria-label="Copy room link" onClick={() => void navigator.clipboard.writeText(window.location.href).then(() => setNotice('Room link copied. Access remains limited to current participants.')).catch(() => setError('The room link could not be copied.'))}><Link /></Button><Button variant="outline" size="sm" onClick={() => window.matchMedia('(max-width: 1023px)').matches ? setPeopleDrawer(value => !value) : setShowPeople(value => !value)}><Users />Participants</Button></>}</header>
      {error && <div role="alert" className="flex items-center gap-2 border-b bg-destructive/10 px-5 py-3 text-sm text-destructive"><span className="flex-1">{error}</span><Button variant="ghost" size="sm" onClick={() => void refresh().then(() => setError('')).catch(failure => setError(roomError(failure)))}>Refresh</Button></div>}
      {notice && <div role="status" className="flex items-center gap-2 border-b bg-muted/40 px-5 py-2 text-xs text-muted-foreground"><span className="flex-1">{notice}</span><button aria-label="Dismiss notice" onClick={() => setNotice('')}><X className="size-3.5" /></button></div>}
      {loading && !room ? <div className="grid flex-1 place-items-center"><Loader2 className="size-6 animate-spin text-muted-foreground" /></div> : !room ? <div className="m-auto max-w-md p-8 text-center"><Users className="mx-auto mb-4 size-10 text-primary" /><h2 className="text-xl font-semibold">A space for people and agents</h2><p className="my-3 text-sm leading-6 text-muted-foreground">Create a shared room, add colleagues and agents, and keep the discussion connected to the records you choose to share.</p><Button onClick={() => setModal('create')}><Plus />Create a shared room</Button></div> : <>
        <div className="flex items-center gap-2 border-b px-5 py-2"><Search className="size-3.5 text-muted-foreground" /><Input aria-label="Search room messages" value={messageSearch} onChange={event => setMessageSearch(event.target.value)} placeholder="Search loaded messages" className="h-8 border-transparent shadow-none" /><Badge variant="outline">{room.room.role}</Badge></div>
        <div className="flex-1 space-y-5 overflow-y-auto px-6 py-5" aria-label="Room conversation" aria-live="polite">
          {room.hasEarlier && <Button variant="ghost" size="sm" disabled={pending} onClick={() => loadEarlier()}>Load earlier messages</Button>}
          {room.messages.filter(message => !messageSearch || `${message.authorName} ${message.content}`.toLowerCase().includes(messageSearch.toLowerCase())).map((message, index) => <div key={message.id} tabIndex={message.id === linkedMessage ? -1 : undefined} data-notification-target={message.id === linkedMessage ? message.id : undefined} className={cn("space-y-4", message.id === linkedMessage && "rounded-lg bg-primary/5 p-3 ring-1 ring-primary/40")} >{!messageSearch && index > 0 && room.messages[index - 1].sequence + 1 < message.sequence && <Button variant="ghost" size="sm" disabled={pending} onClick={() => loadEarlier(message.sequence)}>Load messages between these points</Button>}{message.id === linkedMessage && <p className="text-xs font-medium text-primary">Message opened from notification</p>}<RoomMessageView message={message} room={room} />{message.authorUserId === room.currentUserId && writable && canUseAgents && message.mentionKeys.filter(key => key.startsWith('agent:') && room.participants.some(person => person.actorKey === key) && !room.turns.some(turn => turn.promptMessageId === message.id && turn.agentName === key.slice(6))).map(key => <Button key={key} size="sm" variant="outline" disabled={pending || activeTurns.length > 0} onClick={() => void act(() => runAgent(activeId, { clientRequestId: crypto.randomUUID(), promptMessageId: message.id, agentName: key.slice(6) }))}>Ask {agentDisplayName(key.slice(6))} to respond</Button>)}{room.turns.filter(turn => message.turnId === turn.id || (turn.promptMessageId === message.id && !room.messages.some(reply => reply.turnId === turn.id))).map(turn => <RoomTurnView key={turn.id} turn={turn} owner={!!owner} pending={pending} canUseAgents={canUseAgents} onDecide={(approval, choice) => setDecision({ roomId: activeId, turn, approval, choice })} onContinue={approval => void act(() => runAgent(activeId, { clientRequestId: crypto.randomUUID(), promptMessageId: turn.promptMessageId, agentName: turn.agentName, continueApprovalId: approval.id }))} onClose={() => void act(() => chatRoomService.close(activeId, turn.id))} />)}</div>)}
          <div ref={bottom} />
        </div>
        <div className="border-t p-4"><div className="mb-2 flex items-center gap-2 text-xs text-muted-foreground"><LockKeyhole className="size-3.5" /><span className="flex-1">Everyone in {room.room.title} can read this message.</span><Select value={target} onValueChange={setTarget} disabled={!writable || pending || !canUseAgents}><SelectTrigger size="sm" className="w-52" aria-label="Message recipient"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="people">Message the room</SelectItem>{room.participants.filter(person => person.agentName && agents.some(agent => agent.name === person.agentName)).map(person => <SelectItem key={person.actorKey} value={person.agentName!}>Ask {agentDisplayName(person.agentName!)}</SelectItem>)}</SelectContent></Select></div>
          <Textarea aria-label="Message shared room" placeholder={writable ? 'Message the team or ask a selected agent…' : 'You are an observer. You can read this room.'} value={draft} maxLength={16000} disabled={!writable} onChange={event => { const text = event.target.value; setDrafts(values => ({ ...values, [activeId]: text })); }} onKeyDown={event => { if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); void send(); } }} className="min-h-24 resize-none" />
          <div className="mt-2 flex flex-wrap items-center gap-2"><AtSign className="size-3.5 text-muted-foreground" /><Select value="" onValueChange={key => setMentions(values => values.includes(key) ? values : [...values, key])} disabled={!writable || pending}><SelectTrigger size="sm" className="w-40" aria-label="Mention participant"><SelectValue placeholder="Mention someone" /></SelectTrigger><SelectContent>{room.participants.map(person => <SelectItem key={person.actorKey} value={person.actorKey}>{person.agentName ? agentDisplayName(person.agentName) : person.displayName}</SelectItem>)}</SelectContent></Select>{mentions.map(key => <button key={key} className="rounded-md border px-2 py-1 text-xs" onClick={() => setMentions(values => values.filter(value => value !== key))}>@{room.participants.find(person => person.actorKey === key)?.displayName} ×</button>)}<span className="ml-auto text-xs text-muted-foreground">Enter to send · Shift+Enter for a new line</span><Button size="sm" disabled={!writable || !draft.trim() || pending || (target !== 'people' && (!canUseAgents || activeTurns.length > 0))} onClick={() => void send()}>{pending ? <Loader2 className="animate-spin" /> : <Send />}Send</Button></div>
        </div>
      </>}
    </section>
    {room && showPeople && <RoomParticipants key={room.room.id} room={room} agents={agents} onInvite={setModal} onRemove={setRemove} />}
    <Sheet open={peopleDrawer && !!room} onOpenChange={setPeopleDrawer}><SheetContent size="sm"><SheetHeader title="Room participants" subtitle="People, agents and shared context" /><SheetBody className="p-0">{room && <RoomParticipants room={room} agents={agents} className="w-full max-xl:w-full max-lg:flex border-0" onInvite={kind => { setPeopleDrawer(false); setModal(kind); }} onRemove={person => { setPeopleDrawer(false); setRemove(person); }} />}</SheetBody></SheetContent></Sheet>
    {modal && <RoomEditorDialog key={`${activeId}:${modal}`} kind={modal} room={room} agents={agents} onClose={() => setModal(null)} onCreated={openRoom} onChanged={refresh} />}
    <Dialog open={!!remove} onOpenChange={open => !open && setRemove(null)}><DialogContent><DialogHeader><DialogTitle>Remove room access?</DialogTitle><DialogDescription>{remove?.displayName} will lose access to this room and its updates. Existing messages remain in the history.</DialogDescription></DialogHeader><DialogFooter><Button variant="outline" onClick={() => setRemove(null)}>Cancel</Button><Button variant="destructive" disabled={pending} onClick={() => { const person = remove; if (person) void act(async () => { await chatRoomService.remove(activeId, person.actorKey); setRemove(null); if (person.userId === room?.currentUserId) setSearchParams({ view: 'rooms' }); }); }}>Remove participant</Button></DialogFooter></DialogContent></Dialog>
    <Dialog open={!!decision} onOpenChange={open => !open && setDecision(null)}><DialogContent><DialogHeader><DialogTitle>{decision?.choice === 'Approve' ? 'Confirm this proposed action' : 'Reject this proposed action'}</DialogTitle><DialogDescription>Review the exact request. Your decision is checked by the existing server policy; a room role does not grant financial authority.</DialogDescription></DialogHeader><div className="space-y-3"><strong className="text-sm">{decision?.approval.actionKind}</strong><pre className="max-h-72 overflow-auto rounded-lg border bg-muted p-3 font-mono text-xs whitespace-pre-wrap break-words">{decision?.approval.argumentsJson}</pre><p className="text-xs text-muted-foreground">{decision?.approval.tier === 'High' ? 'High-risk action: the durable proposal dispatcher owns execution.' : 'Medium-risk action: approval authorises only the exact recorded arguments, once.'}</p>{error && <p role="alert" className="text-sm text-destructive">{error}</p>}</div><DialogFooter><Button variant="outline" onClick={() => setDecision(null)}>Cancel</Button><Button variant={decision?.choice === 'Approve' ? 'agent' : 'destructive'} disabled={pending || !writable || !canUseAgents} onClick={() => { const item = decision; if (item) void act(async () => { await chatRoomService.decide(item.roomId, item.turn.id, item.approval.id, item.choice); setDecision(null); }); }}>{pending ? <Loader2 className="animate-spin" /> : <Check />}Confirm {decision?.choice.toLowerCase()}</Button></DialogFooter></DialogContent></Dialog>
  </div>;
}
