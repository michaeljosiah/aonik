import { describe, expect, it } from 'vitest';
import { isRoomParticipantOnline, mergeRoomMessages } from './roomPresentation';
import type { RoomMessage, RoomParticipant } from '@/services/chatRoomService';
const message = (id: string, sequence: number, content: string): RoomMessage => ({ id, sequence, content, actorKind: 'Human', authorUserId: 'u1', authorName: 'Oliver', agentName: null, mentionKeys: [], aiRunId: null, turnId: null, createdAt: '2026-11-02T12:00:00Z' });
describe('collaborative history and presence', () => {
  it('merges polled and older pages without duplicating messages or losing ordering', () => {
    expect(mergeRoomMessages([message('a', 1, 'older'), message('b', 2, 'old')], [message('c', 3, 'new'), message('b', 2, 'canonical')]).map(item => item.content)).toEqual(['older', 'canonical', 'new']);
  });
  it('marks only human participants with a recent heartbeat online', () => {
    const now = Date.parse('2026-11-02T12:01:00Z');
    const human = { userId: 'human', lastSeenAt: '2026-11-02T12:00:50Z' } as RoomParticipant;
    expect(isRoomParticipantOnline(human, now)).toBe(true);
    expect(isRoomParticipantOnline(human, now + 60000)).toBe(false);
    expect(isRoomParticipantOnline({ ...human, userId: null }, now)).toBe(false);
    expect(isRoomParticipantOnline({ ...human, lastSeenAt: null }, now)).toBe(false);
  });
});
