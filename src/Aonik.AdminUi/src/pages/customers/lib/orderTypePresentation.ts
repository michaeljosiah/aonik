// Order-type chip presentation (Spec 081 §2). OrderType is an OPEN string on the spine —
// the enum is additive by design — so an unknown code must render verbatim with a default
// tone rather than break or be swallowed.

import type { PillTone } from '@/components/layout/aonik';

import {
  ORDER_TYPE_BANK_TRANSFER,
  ORDER_TYPE_BILL_PAYMENT,
  ORDER_TYPE_PAYOUT,
  ORDER_TYPE_PRODUCT_PURCHASE,
  ORDER_TYPE_REMITTANCE,
} from './spineDerivations';

interface OrderTypePresentation {
  label: string;
  tone: PillTone;
}

const KNOWN: Record<string, OrderTypePresentation> = {
  [ORDER_TYPE_PRODUCT_PURCHASE]: { label: 'Purchase', tone: 'info' },
  [ORDER_TYPE_BILL_PAYMENT]: { label: 'Bill payment', tone: 'muted' },
  [ORDER_TYPE_REMITTANCE]: { label: 'Remittance', tone: 'pending' },
  [ORDER_TYPE_BANK_TRANSFER]: { label: 'Transfer', tone: 'pending' },
  [ORDER_TYPE_PAYOUT]: { label: 'Payout', tone: 'pending' },
};

/**
 * How one order type renders. An unknown code keeps its RAW value as the label: a new
 * order type shipped by the backend must stay legible in the admin without a frontend
 * release, and silently relabelling it would misreport what the order actually is.
 */
export function presentOrderType(orderType: string): OrderTypePresentation {
  return KNOWN[orderType] ?? { label: orderType || '—', tone: 'default' };
}
