import { api } from '@/lib/api';

export interface RoomReference { type: string; reference: string }
export interface RoomSummary { id: string; title: string; role: 'Owner' | 'Participant' | 'Observer'; revision: number; lastActivityAt: string; humanCount: number; agentCount: number }
export interface RoomParticipant { actorKey: string; userId: string | null; agentName: string | null; displayName: string; role: string; lastSeenAt: string | null }
export interface RoomApproval { id: string; proposalId: string | null; tool: string; tier: string; actionKind: string; status: string; argumentsJson: string; canDecide: boolean; canContinue: boolean }
export interface RoomMessage { id: string; sequence: number; actorKind: 'Human' | 'Agent' | 'System'; authorUserId: string | null; agentName: string | null; authorName: string; content: string; mentionKeys: string[]; aiRunId: string | null; turnId: string | null; createdAt: string }
export interface RoomTurn { id: string; promptMessageId: string; requestedByUserId: string; agentName: string; status: string; aiRunId: string | null; startedAt: string; error: string | null; approvals: RoomApproval[]; canClose: boolean }
export interface RoomDetail { room: RoomSummary; participants: RoomParticipant[]; context: RoomReference[]; messages: RoomMessage[]; turns: RoomTurn[]; hasEarlier: boolean; currentUserId: string }
export interface RoomAgent { name: string; description: string; scope: string }
export interface RoomPerson { userId: string; displayName: string }
export interface RoomSendRequest { clientRequestId: string; content: string; mentionKeys: string[] }
export interface RoomRunRequest { clientRequestId: string; promptMessageId: string; agentName: string; continueApprovalId?: string }
const roomPath = (id: string) => `/ai/rooms/${encodeURIComponent(id)}`;
export const chatRoomService = {
  list: (signal?: AbortSignal) => api.get<RoomSummary[]>('/ai/rooms', { signal }),
  get: (id: string, before?: number, signal?: AbortSignal) => api.get<RoomDetail>(`${roomPath(id)}${before ? `?before=${before}` : ''}`, { signal }),
  create: (title: string, context: RoomReference[]) => api.post<RoomDetail>('/ai/rooms', { title, context }),
  people: (search: string, signal?: AbortSignal) => api.get<RoomPerson[]>(`/ai/rooms/people?search=${encodeURIComponent(search)}`, { signal }),
  agents: (signal?: AbortSignal) => api.get<RoomAgent[]>('/ai/rooms/agents', { signal }),
  add: (id: string, actorKey: string, role: string, shareHistory: boolean) => api.post(`${roomPath(id)}/members`, { actorKey, role, shareHistory }),
  remove: (id: string, actorKey: string) => api.delete(`${roomPath(id)}/members/${encodeURIComponent(actorKey)}`),
  heartbeat: (id: string) => api.post(`${roomPath(id)}/presence`),
  send: (id: string, request: RoomSendRequest) => api.post<RoomMessage>(`${roomPath(id)}/messages`, request),
  run: (id: string, request: RoomRunRequest) => api.post<RoomTurn>(`${roomPath(id)}/turns`, request, { timeout: 210000 }),
  decide: (id: string, turnId: string, approvalId: string, decision: 'Approve' | 'Reject') => api.post(`${roomPath(id)}/approvals/decide`, { turnId, approvalId, decision }),
  close: (id: string, turnId: string) => api.post(`${roomPath(id)}/turns/${encodeURIComponent(turnId)}/close`),
};
