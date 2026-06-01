// Partner Network hub — Updates tab.
//
// Async partner callbacks (payout settled, collection authorized, bill paid) are
// how all three services actually settle. The translation contract
// (IPartnerWebhookTranslator) is defined, but there is no inbound webhook
// endpoint or event store persisting those events yet (Spec 031 gap C4). Rather
// than fake an event feed, this tab is honest about the missing backend and
// describes exactly what it will render once the inbox lands.

import { Inbox, ShieldCheck } from 'lucide-react';
import { EmptyState, InfoNote } from './components';

export function UpdatesTab() {
  return (
    <div className="flex flex-col gap-4">
      <EmptyState
        icon={Inbox}
        title="Webhook updates aren't wired up yet"
        description={
          <>
            Partner callbacks will appear here once the webhook inbox is built. The translation contract{' '}
            <code className="font-[family-name:var(--font-mono)] text-[11.5px]">IPartnerWebhookTranslator</code> is
            defined, but there is no inbound endpoint or event store persisting events yet.
          </>
        }
      />
      <InfoNote icon={ShieldCheck}>
        When live, each row will show the normalized event: provider, service category (payout / collection / bill
        payment), event type, client and provider references, signature-verified state, and the mapped partner
        status.
      </InfoNote>
    </div>
  );
}
