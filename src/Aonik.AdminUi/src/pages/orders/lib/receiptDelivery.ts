const states = {
  Delivered:{label:'Delivered',description:'Accepted by the recipient’s mail server; this does not confirm reading.',variant:'success',icon:'email-delivered'},
  Bounced:{label:'Bounced',description:'The recipient’s mail server rejected the email. Review the saved address.',variant:'destructive',icon:'email-bounced'},
  Suppressed:{label:'Suppressed',description:'The provider suppressed this address after an earlier hard bounce.',variant:'destructive',icon:'email-bounced'},
  Quarantined:{label:'Quarantined',description:'The receiving system quarantined the message.',variant:'warning',icon:'email-bounced'},
  FilteredSpam:{label:'Blocked as spam',description:'The message was rejected or blocked as spam.',variant:'destructive',icon:'email-bounced'},
  Failed:{label:'Delivery failed',description:'The provider reported that the message was not delivered.',variant:'destructive',icon:'email-bounced'},
  Expanded:{label:'Group expanded',description:'The distribution list was expanded. Final delivery is not confirmed.',variant:'outline',icon:'communication'},
  AwaitingReport:{label:'Awaiting report',description:'No delivery report has been received.',variant:'outline',icon:'communication'},
  NotTracked:{label:'Unavailable',description:'This earlier send has no delivery tracking reference.',variant:'outline',icon:'communication'},
  NotSent:{label:'Not sent',description:'The send was not accepted, so delivery is not expected.',variant:'outline',icon:'communication'},
} as const;

export function receiptDeliveryState(status?: string) {
  return status && Object.hasOwn(states,status) ? states[status as keyof typeof states] : status ? {label:'Unconfirmed',description:'The delivery status is not recognized.',variant:'outline',icon:'communication'} as const : states.NotTracked;
}
