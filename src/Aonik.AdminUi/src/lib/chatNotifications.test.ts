import { describe, expect, it } from 'vitest';
import { chatNotificationContext, isChatNotification } from './chatNotifications';

describe('chat notification identity', () => {
  it('uses structured human and room metadata only for chat notifications', () => {
    const item = { type: 'chat.mention', metadataJson: JSON.stringify({ authorName: 'Maria Gomez', roomTitle: 'Collections review', actorKind: 'Human' }) };
    expect(chatNotificationContext(item)).toEqual({ authorName: 'Maria Gomez', roomTitle: 'Collections review', human: true });
    expect(isChatNotification(item)).toBe(true);
    expect(chatNotificationContext({ ...item, type: 'SystemAlert' })).toBeUndefined();
  });
  it('rejects malformed metadata and keeps agent identities distinct from humans', () => {
    for (const metadataJson of ['null', '[]', 'invalid', '{}', '42', JSON.stringify({ authorName: 'Maria', roomTitle: 'Room', actorKind: 'System' })]) {
      expect(chatNotificationContext({ type: 'chat.message', metadataJson })).toBeUndefined();
    }
    expect(chatNotificationContext({ type: 'chat.message', metadataJson: JSON.stringify({ authorName: 'AONIK', roomTitle: 'Collections', actorKind: 'Agent' }) })?.human).toBe(false);
  });
});
