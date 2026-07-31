import { describe, expect, it } from 'vitest';

import { presentOrderType } from './orderTypePresentation';
import {
  ORDER_TYPE_BILL_PAYMENT,
  ORDER_TYPE_PRODUCT_PURCHASE,
  ORDER_TYPE_REMITTANCE,
} from './spineDerivations';

describe('presentOrderType', () => {
  it('labels the shipped order types', () => {
    expect(presentOrderType(ORDER_TYPE_PRODUCT_PURCHASE).label).toBe('Purchase');
    expect(presentOrderType(ORDER_TYPE_BILL_PAYMENT).label).toBe('Bill payment');
    expect(presentOrderType(ORDER_TYPE_REMITTANCE).label).toBe('Remittance');
  });

  it('separates purchases from payments by tone, since that is the tab-wide distinction', () => {
    expect(presentOrderType(ORDER_TYPE_PRODUCT_PURCHASE).tone).not.toBe(
      presentOrderType(ORDER_TYPE_BILL_PAYMENT).tone,
    );
  });

  it('renders an UNKNOWN code verbatim — OrderType is an open string, additive by design', () => {
    // A backend shipping a new order type must stay legible without a frontend release,
    // and relabelling it would misreport what the order is.
    expect(presentOrderType('CarbonOffset')).toEqual({ label: 'CarbonOffset', tone: 'default' });
    expect(presentOrderType('PurchaseOrder').label).toBe('PurchaseOrder');
  });

  it('does not render an empty chip for a blank code', () => {
    expect(presentOrderType('').label).toBe('—');
  });
});
