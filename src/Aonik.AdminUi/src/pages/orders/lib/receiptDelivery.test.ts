import { describe, expect, it } from 'vitest';
import { receiptDeliveryState } from './receiptDelivery';

describe('receipt delivery presentation',()=>{
  it('distinguishes delivery proof, rejection, waiting and unavailable older records',()=>{
    expect(receiptDeliveryState('Delivered')).toMatchObject({label:'Delivered',icon:'email-delivered'});
    expect(receiptDeliveryState('Delivered').description).toContain('does not confirm reading');
    expect(receiptDeliveryState('Bounced')).toMatchObject({label:'Bounced',variant:'destructive',icon:'email-bounced'});
    expect(receiptDeliveryState('AwaitingReport').label).toBe('Awaiting report');
    expect(receiptDeliveryState().label).toBe('Unavailable');
    expect(receiptDeliveryState('Expanded').description).toContain('Final delivery is not confirmed');
    expect(receiptDeliveryState('future-value').label).toBe('Unconfirmed');
  });
});
