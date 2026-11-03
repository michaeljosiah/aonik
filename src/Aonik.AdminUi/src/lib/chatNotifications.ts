import type { AdminNotification } from '@/services/notificationService';

export function isChatNotification(item: Pick<AdminNotification, 'type'>): boolean {
  return item.type === 'chat.mention' || item.type === 'chat.message';
}

export function chatNotificationContext(item: Pick<AdminNotification, 'type' | 'metadataJson'>): { authorName: string; roomTitle: string; human: boolean } | undefined {
  if (!isChatNotification(item)) return;
  try {
    const data = JSON.parse(item.metadataJson) as Record<string, unknown> | null;
    if (!data || Array.isArray(data)) return;
    const valid = (value: unknown): value is string => typeof value === 'string' && value.trim().length > 0 && value.length <= 200 && !Array.from(value).some(character => character.charCodeAt(0) < 32);
    if (!valid(data.authorName) || !valid(data.roomTitle) || (data.actorKind !== 'Human' && data.actorKind !== 'Agent')) return;
    return { authorName: data.authorName, roomTitle: data.roomTitle, human: data.actorKind === 'Human' };
  } catch { return; }
}
