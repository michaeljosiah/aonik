export interface PendingReceiptEmail { requestId: string; contactId: string; emailHash: string }
export const receiptEmailStorageKey=(tenantId: string,userId: string,orderId: string)=>
  `aonik:receipt-email:${encodeURIComponent(tenantId)}:${encodeURIComponent(userId)}:${encodeURIComponent(orderId)}`;

export function readPendingReceiptEmail(storage: Pick<Storage,'getItem'>,key: string): PendingReceiptEmail | null {
  const raw=storage.getItem(key);
  if (!raw) return null;
  const value=JSON.parse(raw) as PendingReceiptEmail;
  if (!value?.requestId || !value.contactId || !/^[a-f0-9]{64}$/i.test(value.emailHash)) throw new Error('A previous receipt email request could not be recovered. Check its delivery status before sending another copy.');
  return value;
}

export async function receiptEmailHash(email: string): Promise<string> {
  const bytes=await crypto.subtle.digest('SHA-256',new TextEncoder().encode(email.trim().toLowerCase()));
  return Array.from(new Uint8Array(bytes),byte=>byte.toString(16).padStart(2,'0')).join('');
}
