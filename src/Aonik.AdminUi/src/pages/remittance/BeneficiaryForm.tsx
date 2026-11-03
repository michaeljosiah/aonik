import { useRef, useState } from 'react';
import type { FormEvent, ReactNode } from 'react';

import { AonikTemplateIcon } from '@/components/layout/aonik/AonikTemplateIcon';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Field, FieldDescription, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { NativeSelect } from '@/components/ui/native-select';
import { SheetBody, SheetFooter, SheetHeader } from '@/components/ui/sheet';
import { beneficiaryService } from '@/services/beneficiaryService';
import type { ManagedBeneficiary, BeneficiaryDestination } from '@/services/beneficiaryService';
import type { CatalogCountryItem, CatalogCurrencyItem } from '@/types';

import { beneficiaryError, beneficiaryRequest, changeBeneficiaryRail, emptyBeneficiaryDraft } from './lib/beneficiaryEditor';
import type { BeneficiaryDraft } from './lib/beneficiaryEditor';


function Entry({id,label,children}: {id: string; label: string; children: ReactNode}) {
  return <Field><FieldLabel htmlFor={id}>{label}</FieldLabel>{children}</Field>;
}

export function BeneficiaryForm({customerId,tenantId,beneficiaries,countries,currencies,recipientId,onBusy,onSaved,onCancel}: {
  customerId: string; tenantId: string; beneficiaries: ManagedBeneficiary[];
  countries: CatalogCountryItem[]; currencies: CatalogCurrencyItem[]; recipientId: string;
  onBusy: (busy: boolean) => void; onSaved: (destination: BeneficiaryDestination) => void; onCancel: () => void;
}) {
  const [draft,setDraft]=useState<BeneficiaryDraft>(()=>({...emptyBeneficiaryDraft(),beneficiaryPartyId:recipientId}));
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  const requestLock=useRef(false);
  const recipients=[...new Map(beneficiaries.map(item=>[item.destination.beneficiaryPartyId,item.destination])).values()];
  const update=(patch: Partial<BeneficiaryDraft>)=>{setDraft(previous=>({...previous,...patch}));setError('');};
  async function save(event: FormEvent) {
    event.preventDefault();
    if (requestLock.current) return;
    let request;
    try {request=beneficiaryRequest(draft,customerId);} catch(error) {setError(beneficiaryError(error,'Check the destination details.'));return;}
    requestLock.current=true;setBusy(true);onBusy(true);setError('');
    try {
      const result=await beneficiaryService.register(request,{headers:{'X-Tenant-Id':tenantId}});
      setDraft(emptyBeneficiaryDraft());onSaved(result);
    } catch(error) {setError(beneficiaryError(error,'Unable to verify and save this destination.'));}
    finally {requestLock.current=false;setBusy(false);onBusy(false);}
  }
  return <>
    <SheetHeader title="Add payout destination" subtitle="Verify the recipient’s bank, mobile money or wallet details." icon={<AonikTemplateIcon name="menu-beneficiaries" />} closeAffordance={busy ? null : undefined} />
    <SheetBody><form id="beneficiary-form" onSubmit={event=>void save(event)} autoComplete="off">
      {error && <Alert variant="destructive" className="mb-5"><AlertDescription>{error}</AlertDescription></Alert>}
      <fieldset disabled={busy} className="flex min-w-0 flex-col gap-5">
        <Entry id="beneficiary-recipient" label="Recipient"><NativeSelect id="beneficiary-recipient" value={draft.beneficiaryPartyId} onChange={event=>update({beneficiaryPartyId:event.target.value})}><option value="">Create a new recipient</option>{recipients.map(recipient=><option key={recipient.beneficiaryPartyId} value={recipient.beneficiaryPartyId}>{recipient.beneficiaryName}</option>)}</NativeSelect></Entry>
        {!draft.beneficiaryPartyId && <div className="grid gap-5 sm:grid-cols-2">
          <Entry id="beneficiary-name" label="Recipient name"><Input id="beneficiary-name" value={draft.beneficiaryDisplayName} maxLength={256} onChange={event=>update({beneficiaryDisplayName:event.target.value})} /></Entry>
          <Entry id="beneficiary-party-type" label="Recipient type"><NativeSelect id="beneficiary-party-type" value={draft.beneficiaryPartyType} onChange={event=>update({beneficiaryPartyType:event.target.value as BeneficiaryDraft['beneficiaryPartyType']})}><option value="Person">Person</option><option value="Business">Business</option></NativeSelect></Entry>
        </div>}
        <Entry id="beneficiary-rail" label="Destination type"><NativeSelect id="beneficiary-rail" value={draft.destinationType} onChange={event=>setDraft(previous=>changeBeneficiaryRail(previous,event.target.value as BeneficiaryDraft['destinationType']))}><option value="Bank">Bank account</option><option value="MobileMoney">Mobile money</option><option value="Wallet">Wallet</option></NativeSelect></Entry>
        <div className="grid gap-5 sm:grid-cols-2">
          <Entry id="beneficiary-country" label="Receiving country"><NativeSelect id="beneficiary-country" value={draft.country} onChange={event=>update({country:event.target.value})}><option value="">Choose country</option>{countries.map(country=><option key={country.countryCode} value={country.countryCode}>{country.name}</option>)}</NativeSelect></Entry>
          <Entry id="beneficiary-currency" label="Receiving currency"><NativeSelect id="beneficiary-currency" value={draft.currency} onChange={event=>update({currency:event.target.value})}><option value="">Choose currency</option>{currencies.map(currency=><option key={currency.code} value={currency.code}>{currency.code} · {currency.name}</option>)}</NativeSelect></Entry>
        </div>
        <Entry id="beneficiary-account-name" label="Account holder name"><Input id="beneficiary-account-name" value={draft.accountName} maxLength={256} onChange={event=>update({accountName:event.target.value})} /></Entry>
        {draft.destinationType==='Bank' && <>
          <div className="grid gap-5 sm:grid-cols-2"><Entry id="beneficiary-bank-code" label="Bank code"><Input id="beneficiary-bank-code" value={draft.bankCode} maxLength={32} onChange={event=>update({bankCode:event.target.value})} /></Entry><Entry id="beneficiary-branch" label="Branch code (optional)"><Input id="beneficiary-branch" value={draft.branchCode} maxLength={32} onChange={event=>update({branchCode:event.target.value})} /></Entry></div>
          <Entry id="beneficiary-account-number" label="Account number"><Input id="beneficiary-account-number" value={draft.accountNumber} maxLength={64} onChange={event=>update({accountNumber:event.target.value})} autoComplete="off" spellCheck={false} /></Entry>
        </>}
        {draft.destinationType==='MobileMoney' && <>
          <Entry id="beneficiary-network" label="Mobile network"><Input id="beneficiary-network" value={draft.mobileNetwork} maxLength={50} onChange={event=>update({mobileNetwork:event.target.value})} /></Entry>
          <Entry id="beneficiary-phone" label="Mobile money number"><Input id="beneficiary-phone" type="tel" value={draft.msisdn} maxLength={32} onChange={event=>update({msisdn:event.target.value})} autoComplete="off" /></Entry>
        </>}
        {draft.destinationType==='Wallet' && <Entry id="beneficiary-wallet" label="Wallet identifier"><Input id="beneficiary-wallet" value={draft.walletId} maxLength={128} onChange={event=>update({walletId:event.target.value})} autoComplete="off" spellCheck={false} /></Entry>}
        <FieldDescription>These details are sent to the configured payout provider for verification and registration. Saved destinations show masked identifiers. No funds are moved.</FieldDescription>
      </fieldset>
    </form></SheetBody>
    <SheetFooter><Button variant="outline" disabled={busy} onClick={onCancel}>Cancel</Button><Button type="submit" form="beneficiary-form" disabled={busy}><AonikTemplateIcon name="menu-compliance" size={16} />{busy ? 'Verifying destination…' : 'Verify & save destination'}</Button></SheetFooter>
  </>;
}
