import { Card as AonikCard } from '@/components/layout/aonik';
import { formatCalendarDate } from '@/lib/format';
import type { OrderDeliveryDto } from '@/types/commerce';

export function OrderDeliveryDetails({ delivery }: { delivery: OrderDeliveryDto | null }) {
  return (
    <AonikCard title="Delivery details" padding={12}>
      {delivery ? (
        <dl className="grid gap-3 text-[12.5px]">
          <div>
            <dt className="text-muted-foreground">Delivery date</dt>
            <dd>{formatCalendarDate(delivery.deliveryDate)} <span className="text-muted-foreground">({delivery.timezone})</span></dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Purchaser</dt>
            <dd>{delivery.purchaser.firstName} {delivery.purchaser.lastName}</dd>
            <dd className="break-words">{delivery.purchaser.email}</dd>
            <dd>{delivery.purchaser.phone}</dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Recipient</dt>
            <dd>{delivery.recipient.name}</dd>
            <dd>{delivery.recipient.phone}</dd>
          </div>
          <div>
            <dt className="text-muted-foreground">Delivery address</dt>
            <dd className="whitespace-pre-line break-words">{[
              delivery.address.line1, delivery.address.line2, delivery.address.city,
              delivery.address.region, delivery.address.postcode, delivery.address.countryCode,
            ].filter(Boolean).join('\n')}</dd>
          </div>
          {delivery.notes && (
            <div>
              <dt className="text-muted-foreground">Delivery instructions</dt>
              <dd className="whitespace-pre-line break-words">{delivery.notes}</dd>
            </div>
          )}
        </dl>
      ) : <p className="text-[12.5px] text-muted-foreground">No delivery details were recorded for this order.</p>}
    </AonikCard>
  );
}
