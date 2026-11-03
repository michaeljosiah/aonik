import type { RoomMessage, RoomParticipant } from '@/services/chatRoomService';

export function mergeRoomMessages(older: RoomMessage[], current: RoomMessage[]): RoomMessage[] {
  return [...new Map([...older, ...current].map(message => [message.id, message])).values()].sort((a, b) => a.sequence - b.sequence);
}
export function isRoomParticipantOnline(participant: RoomParticipant, now = Date.now()): boolean {
  return !!participant.userId && !!participant.lastSeenAt && now - new Date(participant.lastSeenAt).getTime() < 60000;
}
export function initials(name: string): string { return name.trim().split(/[\s.@_-]+/).filter(Boolean).slice(0, 2).map(part => part[0]).join('').toUpperCase(); }
export function roomError(error: unknown): string {
  const detail = error as { userMessage?: string; response?: { data?: { message?: string; errors?: Record<string, string[]> } } };
  const errors = detail.response?.data?.errors;
  return (errors ? Object.values(errors).flat().join(' ') : undefined) || detail.userMessage || detail.response?.data?.message || 'The room request could not be completed. Refresh to check its status.';
}
