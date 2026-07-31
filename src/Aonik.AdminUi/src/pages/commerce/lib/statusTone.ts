// Status → pill tone, shared by the orders list, carts list and both drawers (Spec 083) so a
// status never renders one colour in the table and another in the drawer.
//
// Every one of these vocabularies is an OPEN string server-side, so the default arm is not a
// fallback for completeness — it is the normal path for a value this build has not seen. An
// unknown status renders neutrally with its own text, never coerced into a known bucket.

import type { PillTone } from '@/components/layout/aonik';

export function paymentTone(status: string): PillTone {
  switch (status) {
    case 'Captured':
      return 'success';
    case 'Failed':
    case 'Cancelled':
      return 'danger';
    case 'Pending':
    case 'RequiresAction':
      return 'warning';
    default:
      return 'default';
  }
}

export function fulfilmentTone(status: string): PillTone {
  switch (status) {
    // Kept for the day fulfilment is actually tracked; `DeriveFulfilment` cannot return it yet.
    case 'Fulfilled':
      return 'success';
    case 'Cancelled':
      return 'danger';
    // MUTED, not warning. Every order that was not cancelled is "Unfulfilled" today, so a
    // warning tone would put an alert on every row and mean nothing. The only signal this
    // column actually carries is cancellation.
    case 'Unfulfilled':
      return 'muted';
    default:
      return 'default';
  }
}

export function cartStatusTone(status: string): PillTone {
  switch (status) {
    case 'Open':
      return 'info';
    case 'CheckedOut':
      return 'success';
    case 'Abandoned':
      return 'warning';
    case 'Expired':
      return 'muted';
    default:
      return 'default';
  }
}
