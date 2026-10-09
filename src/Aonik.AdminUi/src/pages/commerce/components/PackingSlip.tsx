import { formatCalendarDate, formatCurrency } from '@/lib/format';
import type { AdminOrderPackingDto } from '@/types/commerce';

/** This component receives only the recipient projection, never the financial order detail. */
export function PackingSlip({ packing }: { packing: AdminOrderPackingDto }) {
  const charge = packing.prices?.charge;
  const itemIndexes = new Set(packing.items.map(item => item.itemIndex));
  const orphaned = packing.selections.filter(selection => !itemIndexes.has(selection.orderItemIndex));
  return (
    <article className="space-y-5 break-words" aria-label="Packing slip">
      <header>
        <h1 className="text-xl font-semibold">{packing.gift ? 'Gift box packing slip' : 'Packing slip'}</h1>
        <p className="text-sm">Order {packing.orderId}</p>
        {packing.boxSize != null && <p>Box of {packing.boxSize}</p>}
      </header>
      {packing.recipient && <section><h2 className="font-semibold">Recipient</h2><p>{packing.recipient.name}</p><p>{packing.recipient.phone}</p></section>}
      {packing.address && <section><h2 className="font-semibold">Delivery address</h2><p className="whitespace-pre-line">{[
        packing.address.line1, packing.address.line2, packing.address.city, packing.address.region,
        packing.address.postcode, packing.address.countryCode,
      ].filter(Boolean).join('\n')}</p></section>}
      {packing.deliveryDate && <p>Delivery: {formatCalendarDate(packing.deliveryDate)} ({packing.timezone})</p>}
      {packing.notes && <section><h2 className="font-semibold">Delivery instructions</h2><p className="whitespace-pre-line">{packing.notes}</p></section>}
      <section>
        <h2 className="font-semibold">Contents</h2>
        <ul className="space-y-3">
          {packing.items.filter(item => item.itemType !== 'DeliveryFee').map(item => {
            const price = packing.prices?.items.find(line => line.itemIndex === item.itemIndex);
            return <li key={item.itemIndex} className="break-inside-avoid">
              <p>{item.quantity != null && `${item.quantity} × `}{item.name}{item.sku && ` (${item.sku})`}
                {price && charge && ` — ${formatCurrency(price.amount, charge.currency)}`}</p>
              {packing.selections.filter(selection => selection.orderItemIndex === item.itemIndex).map((selection, index) => (
                <p key={`${selection.sku}-${index}`} className="pl-4 text-sm whitespace-pre-line">
                  {selection.quantity} × {selection.name ?? selection.sku}
                  {selection.personalisationSummary && ` — ${selection.personalisationSummary}`}
                </p>
              ))}
            </li>;
          })}
        </ul>
        {orphaned.length > 0 && <div className="mt-3">
          <h3 className="font-semibold">Unmatched preparations — check before packing</h3>
          {orphaned.map((selection, index) => <p key={`${selection.sku}-${index}`} className="whitespace-pre-line">
            {selection.quantity} × {selection.name ?? selection.sku}
            {selection.personalisationSummary && ` — ${selection.personalisationSummary}`}
          </p>)}
        </div>}
      </section>
      {packing.gift?.includeGreetingCard && <section className="break-inside-avoid">
        <h2 className="font-semibold">Greeting card</h2>
        <p className="whitespace-pre-line">{packing.gift.greetingCardMessage ?? 'Include a blank greeting card.'}</p>
      </section>}
      {charge && <section className="break-inside-avoid">
        <h2 className="font-semibold">Charges</h2>
        <p>Subtotal: {formatCurrency(charge.subtotal, charge.currency)}</p>
        {charge.discountTotal !== 0 && <p>Discount: {formatCurrency(-charge.discountTotal, charge.currency)}</p>}
        {charge.taxTotal !== 0 && <p>Tax: {formatCurrency(charge.taxTotal, charge.currency)}</p>}
        <p className="font-semibold">Total: {formatCurrency(charge.total, charge.currency)}</p>
      </section>}
    </article>
  );
}
