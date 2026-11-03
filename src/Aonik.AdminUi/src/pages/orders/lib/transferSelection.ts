import type { TransferDestination } from '@/services/remittanceService';

export const transferEditorPath = (customerId: string, destinationId: string) =>
  `/orders/transfers/new?${new URLSearchParams({customerPartyId:customerId,destinationId})}`;

export function readTransferSelection(search: string): {customerId: string; destinationId: string} | null {
  const params=new URLSearchParams(search);
  if (!params.has('customerPartyId') && !params.has('destinationId')) return null;
  const customerId=params.get('customerPartyId') || '';
  const destinationId=params.get('destinationId') || '';
  const guid=/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
  if (!guid.test(customerId) || !guid.test(destinationId)) throw new Error('This transfer link is incomplete or invalid. Choose a customer and saved destination.');
  return {customerId,destinationId};
}

/** Navigation supplies references only; current tenant/customer ownership and verification come from the API. */
export function selectedTransferDestination(destinations: TransferDestination[], customerId: string, destinationId: string): TransferDestination {
  const destination=destinations.find(item=>item.externalPayoutAccountId.toLowerCase()===destinationId.toLowerCase() && item.customerPartyId.toLowerCase()===customerId.toLowerCase());
  if (!destination?.isVerified) throw new Error('The selected payout destination is unavailable or needs verification. Choose another verified destination from Beneficiaries.');
  return destination;
}
