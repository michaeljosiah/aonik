import { Button } from '@/components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { formatCalendarDate } from '@/lib/format';
import type { DeliveryAvailabilityDto, DeliveryCapacityDto } from '@/services/commerceDeliveryCapacityService';
import { availabilityLabel } from './deliveryCalendarState';

export function DeliveryCapacityTable({ dates, rows, availability, canWrite, saving, onSelect }: {
  dates: string[]; rows: DeliveryCapacityDto[]; availability: DeliveryAvailabilityDto | null;
  canWrite: boolean; saving: boolean; onSelect: (date: string, row?: DeliveryCapacityDto) => void;
}) {
  return <div className="max-h-96 overflow-auto">
    <Table><TableHeader><TableRow><TableHead>Date</TableHead><TableHead>Capacity</TableHead><TableHead>Occupied</TableHead><TableHead>Storefront availability</TableHead></TableRow></TableHeader>
      <TableBody>{dates.map((date) => {
        const row = rows.find((item) => item.deliveryDate === date);
        const status = availability?.availability?.find((item) => item.deliveryDate === date)?.status;
        return <TableRow key={date}>
          <TableCell>{canWrite ? <Button variant="link" size="sm" className="h-auto p-0" disabled={saving}
            onClick={() => onSelect(date, row)}>{formatCalendarDate(date)}</Button> : formatCalendarDate(date)}</TableCell>
          <TableCell>{row ? row.capacity : 'Unconfigured'}</TableCell><TableCell>{row ? row.occupied : '—'}</TableCell>
          <TableCell>{availabilityLabel(status)}</TableCell>
        </TableRow>;
      })}</TableBody></Table>
  </div>;
}
