import type { ConnectorActionRef } from '@/services/connectorService';

export const gmailReadActionKeys = ['gmail.messages.search', 'gmail.messages.get', 'gmail.attachments.get'];
export const gmailReadActions: ConnectorActionRef[] = gmailReadActionKeys.map(key => ({ key, version: 1 }));

const gmailActionLabels: Record<string, string> = {
  'gmail.messages.search': 'Search email',
  'gmail.messages.get': 'Read email',
  'gmail.attachments.get': 'Read attachments',
  'gmail.drafts.get': 'Read drafts',
  'gmail.messages.prepare': 'Prepare email content',
  'gmail.drafts.create': 'Create drafts',
  'gmail.drafts.update': 'Edit drafts',
  'gmail.drafts.delete': 'Delete drafts',
  'gmail.messages.send': 'Send email',
  'gmail.messages.reply': 'Reply to email',
  'gmail.messages.forward': 'Forward email',
  'gmail.drafts.send': 'Send drafts',
  'gmail.messages.modify-labels': 'Change email labels',
  'gmail.messages.mark-read': 'Mark email as read',
  'gmail.messages.mark-unread': 'Mark email as unread',
  'gmail.messages.archive': 'Archive email',
  'gmail.messages.trash': 'Move email to Trash',
  'gmail.messages.restore': 'Restore email from Trash',
  'gmail.messages.permanently-delete': 'Permanently delete email',
};

export function connectorActionLabel(action: ConnectorActionRef): string {
  return gmailActionLabels[action.key] ?? action.key;
}
