// Placeholder — visual port of `MoneyTransferForm` from
// templates/aonik-admin-starterkit/screens/orders.jsx, deliberately stubbed
// because the production order API does not yet support a money-transfer
// order type. The mode tab in the parent page is rendered as disabled and
// flips to this panel only via direct linking; the form itself shows the
// "coming soon" treatment instead of the real builder.

import { Banknote, ShieldCheck } from 'lucide-react';
import { Pill } from '@/components/layout/aonik';

export function MoneyTransferForm() {
  return (
    <div className="flex flex-col items-center justify-center gap-4 rounded-xl border border-dashed border-border bg-muted px-6 py-16 text-center">
      <div className="grid h-14 w-14 place-items-center rounded-2xl bg-primary/10 text-primary">
        <Banknote className="h-6 w-6" />
      </div>
      <div className="w-full max-w-[28rem]">
        <div className="text-[18px] font-bold text-foreground">
          Money transfers coming soon
        </div>
        <div className="mt-1 text-[13px] text-muted-foreground">
          The unified order builder will let you mix bank-to-bank transfers with bill payments in
          a single submission. While that ships, use bill payment orders for outbound flows.
        </div>
      </div>
      <Pill tone="pending">
        <ShieldCheck className="h-3 w-3" />
        Roadmap
      </Pill>
    </div>
  );
}
