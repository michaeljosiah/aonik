import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it, vi } from 'vitest';
import { ReceiptEmailHistoryTable } from './ReceiptEmailHistory';
import type { ReceiptEmailHistoryItem } from '@/services/receiptEmailService';

vi.mock('@/services/receiptEmailService',()=>({receiptEmailService:{history:vi.fn()}}));

describe('receipt email history',()=>{
  it('shows historical masked recipients, UTC times and all send outcomes without claiming delivery',()=>{
    const items: ReceiptEmailHistoryItem[]=(['Accepted','Failed','Unknown','Sending'] as const).map(status=>({
      requestId:status,status,maskedRecipient:'n***@example.test',fileName:'receipt.pdf',requestedAt:'2026-11-01T12:00:00Z',statusUpdatedAt:status==='Sending'?null:'2026-11-01T12:00:02Z',
    }));
    const html=renderToStaticMarkup(<ReceiptEmailHistoryTable items={items} />);
    for(const label of ['Submitted','Not sent','Unconfirmed','Pending','Awaiting result','n***@example.test','UTC','Inbox delivery is not confirmed.']) expect(html).toContain(label);
    expect(html).toContain('dateTime="2026-11-01T12:00:00Z"');
    expect(html).not.toContain('>Delivered<');
    expect(html).not.toContain('Resend receipt');
  });

  it('offers confirmed-history resends only to callers with an action, keeping uncertain rows disabled',()=>{
    const items: ReceiptEmailHistoryItem[]=(['Accepted','Failed','Unknown','Sending'] as const).map(status=>({
      requestId:status,status,maskedRecipient:'n***@example.test',fileName:'receipt.pdf',requestedAt:'2026-11-01T12:00:00Z',statusUpdatedAt:null,
    }));
    const html=renderToStaticMarkup(<ReceiptEmailHistoryTable items={items} onResend={()=>{}} />);
    expect((html.match(/aria-label="Resend receipt for request/g) || []).length).toBe(4);
    for(const status of ['Accepted','Failed','Unknown','Sending']) {
      const button=html.match(new RegExp(`<button[^>]*aria-label="Resend receipt for request ${status}"[^>]*>`))?.[0];
      expect(button).toBeDefined();
      expect(/\sdisabled=""/.test(button!)).toBe(status==='Unknown' || status==='Sending');
    }
    expect(html).toContain('Resolve this send before sending another copy.');
  });

  it('shows provider delivery status and UTC report time independently of send acceptance',()=>{
    const items: ReceiptEmailHistoryItem[]=(['Delivered','Bounced','AwaitingReport','NotTracked'] as const).map(deliveryStatus=>({
      requestId:deliveryStatus,status:'Accepted',maskedRecipient:'n***@example.test',fileName:'receipt.pdf',requestedAt:'2026-11-01T12:00:00Z',statusUpdatedAt:'2026-11-01T12:00:02Z',deliveryStatus,deliveryUpdatedAt:deliveryStatus==='Delivered' || deliveryStatus==='Bounced'?'2026-11-01T12:01:00Z':null,
    }));
    const html=renderToStaticMarkup(<ReceiptEmailHistoryTable items={items}/>);
    for(const value of ['Delivered','Bounced','Awaiting report','Unavailable','menu-email-delivered','menu-email-bounced','Reported:','2026-11-01T12:01:00Z']) expect(html).toContain(value);
    expect((html.match(/>Submitted</g) || []).length).toBe(4);
  });
  it('offers email review only on bounced rows when authorized, keeping read-only history free of actions',()=>{
    const items: ReceiptEmailHistoryItem[]=(['Delivered','Bounced','AwaitingReport','NotTracked'] as const).map(deliveryStatus=>({
      requestId:deliveryStatus,status:'Accepted',maskedRecipient:'n***@example.test',fileName:'receipt.pdf',requestedAt:'2026-11-01T12:00:00Z',statusUpdatedAt:null,deliveryStatus,
    }));
    const html=renderToStaticMarkup(<ReceiptEmailHistoryTable items={items} onReviewEmail={()=>{}}/>);
    expect((html.match(/aria-label="Review customer email for request/g) || []).length).toBe(1);
    expect(html).toContain('Review customer email for request Bounced');
    expect(html).toContain('menu-email-bounced');
    expect(renderToStaticMarkup(<ReceiptEmailHistoryTable items={items}/>)).not.toContain('Review email');
  });
});
