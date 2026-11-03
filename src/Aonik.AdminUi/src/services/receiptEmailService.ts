import { api } from '@/lib/api';
import type { TransferApiContext } from './remittanceService';

export interface ReceiptEmailContact { contactId: string; email: string; isPrimary: boolean }
export interface ReceiptEmailOptions { customerName: string; orderNumber: string; transferStatus: string; fileName: string; contacts: ReceiptEmailContact[] }
export interface ReceiptEmailRequest { requestId: string; contactId: string; expectedEmail: string; confirmSend: true }
export type ReceiptDeliveryStatus = 'Delivered' | 'Bounced' | 'Suppressed' | 'Quarantined' | 'FilteredSpam' | 'Failed' | 'Expanded' | 'AwaitingReport' | 'NotTracked' | 'NotSent';
export interface ReceiptEmailResult { requestId: string; status: 'Accepted' | 'Failed' | 'Unknown' | 'Sending'; maskedRecipient: string; fileName: string; requestedAt: string; deliveryStatus?: ReceiptDeliveryStatus; deliveryUpdatedAt?: string | null }
export interface ReceiptEmailHistoryItem extends ReceiptEmailResult { statusUpdatedAt: string | null }
export interface ReceiptEmailHistory { items: ReceiptEmailHistoryItem[]; totalCount: number; pageNumber: number; pageSize: number }
export interface ReceiptRecipientReview { customerName: string; orderNumber: string; contact: ReceiptEmailContact; changedSinceSend: boolean }
export interface UpdateReceiptRecipientRequest { expectedEmail: string; newEmail: string; confirmUpdate: true }
const path=(orderId: string)=>`/remittance/${encodeURIComponent(orderId)}/receipt/email`;

export const receiptEmailService={
  options:(orderId: string,context: TransferApiContext)=>api.get<ReceiptEmailOptions>(path(orderId),context),
  send:(orderId: string,request: ReceiptEmailRequest,context: TransferApiContext)=>api.post<ReceiptEmailResult>(path(orderId),request,context),
  status:(orderId: string,requestId: string,context: TransferApiContext)=>api.get<ReceiptEmailResult>(`${path(orderId)}/${encodeURIComponent(requestId)}`,context),
  history:(orderId: string,pageNumber: number,context: TransferApiContext)=>api.get<ReceiptEmailHistory>(`${path(orderId)}/history`,{...context,params:{pageNumber,pageSize:20}}),
  reviewRecipient:(orderId: string,requestId: string,context: TransferApiContext)=>api.get<ReceiptRecipientReview>(`${path(orderId)}/${encodeURIComponent(requestId)}/recipient`,context),
  updateRecipient:(orderId: string,requestId: string,request: UpdateReceiptRecipientRequest,context: TransferApiContext)=>api.put<ReceiptEmailContact>(`${path(orderId)}/${encodeURIComponent(requestId)}/recipient`,request,context),
};
