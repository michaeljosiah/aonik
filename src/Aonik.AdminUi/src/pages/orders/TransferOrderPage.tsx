import { useEffect, useRef, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';

import { useAuth } from '@/auth/useAuth';
import { AonikTemplateIcon } from '@/components/layout/aonik/AonikTemplateIcon';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldDescription, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { NativeSelect } from '@/components/ui/native-select';
import { Textarea } from '@/components/ui/textarea';
import { getSelectedTenant } from '@/lib/tenantContext';
import { useModules } from '@/modules/useModules';
import { catalogService } from '@/services/catalogService';
import { customerService } from '@/services/customerService';
import { remittanceService, transferWasNotStarted } from '@/services/remittanceService';
import type { TransferDestination, TransferOrder, TransferQuote } from '@/services/remittanceService';
import type { ReceiptEmailHistoryItem } from '@/services/receiptEmailService';
import type { CatalogCountryItem, CatalogCurrencyItem } from '@/types';

import { PartyPicker } from './PartyPicker';
import { TransferReceiptButton } from './TransferReceiptButton';
import { SendReceiptButton } from './SendReceiptButton';
import { ReceiptEmailHistory } from './ReceiptEmailHistory';
import { ReviewReceiptEmailDialog } from './ReviewReceiptEmailDialog';
import type { PartyPickerOption } from './PartyPicker';
import { attemptStorageKey, confirmRequest, destinationMatchesQuote, emptyTransferDraft, loadTransferAttempt, quoteIsCurrent, quoteRequest, transferDraftError } from './lib/transferEditor';
import type { TransferAttempt, TransferDraft } from './lib/transferEditor';
import { readTransferSelection, selectedTransferDestination } from './lib/transferSelection';

const money = (amount: number, currency: string) => `${currency} ${amount.toLocaleString(undefined,{minimumFractionDigits:2,maximumFractionDigits:4})}`;
const errorText = (error: unknown, fallback: string) => error && typeof error === 'object' && 'userMessage' in error ? String(error.userMessage || fallback) : error instanceof Error ? error.message : fallback;
const glyph = (name: string) => <AonikTemplateIcon name={`menu-${name}`} size={18} />;

function SummaryLine({ label, value }: { label: string; value: string }) {
  return <div className="flex items-start justify-between gap-5 py-2 text-sm"><dt className="text-muted-foreground">{label}</dt><dd className="m-0 break-all text-right font-medium">{value}</dd></div>;
}

export function TransferDetails({ order }: { order: TransferOrder }) {
  return <div className="grid gap-6 lg:grid-cols-2">
    <Card><CardHeader><CardTitle className="flex items-center gap-2">{glyph('transfer-orders')} Transfer summary</CardTitle><CardDescription>The confirmed order and its current execution state.</CardDescription></CardHeader><CardContent><dl>
      <SummaryLine label="Status" value={order.status} /><SummaryLine label="Customer sends" value={money(order.originAmount,order.originCurrency)} /><SummaryLine label="Fees" value={money(order.feesTotal,order.originCurrency)} /><SummaryLine label="Total debit" value={money(order.totalAmount,order.originCurrency)} /><SummaryLine label="Recipient receives" value={money(order.destinationAmount,order.destinationCurrency)} /><SummaryLine label="FX rate" value={`1 ${order.originCurrency} = ${order.exchangeRate} ${order.destinationCurrency}`} /><SummaryLine label="Destination" value={`${order.destinationType} · ${order.maskedAccountIdentifier}`} /><SummaryLine label="Corridor" value={`${order.originCountry} → ${order.destinationCountry}`} />
    </dl></CardContent></Card>
    <Card><CardHeader><CardTitle className="flex items-center gap-2">{glyph('audit')} Execution references</CardTitle><CardDescription>Order intent is tracked separately from payout execution.</CardDescription></CardHeader><CardContent><dl>
      <SummaryLine label="Order number" value={order.orderNumber} /><SummaryLine label="Order ID" value={order.orderId} /><SummaryLine label="Customer reference" value={order.customerPartyId} /><SummaryLine label="Pricing quote" value={order.pricingQuoteId} /><SummaryLine label="Payout ID" value={order.payoutId || 'Not assigned'} /><SummaryLine label="Provider" value={order.providerCode || 'Not assigned'} /><SummaryLine label="Transmission" value={order.transmissionStatus || 'Not yet recorded'} /><SummaryLine label="Client reference" value={order.clientReference || 'Not assigned'} /><SummaryLine label="Provider reference" value={order.providerReference || 'Not yet received'} /><SummaryLine label="Created" value={new Date(order.createdAt).toLocaleString()} />{order.submittedAt && <SummaryLine label="Submitted" value={new Date(order.submittedAt).toLocaleString()} />}{order.settledAt && <SummaryLine label="Settled" value={new Date(order.settledAt).toLocaleString()} />}
    </dl></CardContent></Card>
  </div>;
}

export function TransferOrderPage() {
  const { orderId } = useParams();
  const navigate = useNavigate();
  const { search } = useLocation();
  const { user } = useAuth();
  const { hasPermission, allowsPolicy, isPathVisible } = useModules();
  const [draft,setDraft] = useState<TransferDraft>(emptyTransferDraft);
  const [customer,setCustomer] = useState<PartyPickerOption | null>(null);
  const [countries,setCountries] = useState<CatalogCountryItem[]>([]);
  const [currencies,setCurrencies] = useState<CatalogCurrencyItem[]>([]);
  const [catalogReady,setCatalogReady] = useState(false);
  const [destinations,setDestinations] = useState<TransferDestination[]>([]);
  const [destinationError,setDestinationError] = useState('');
  const [destinationsLoading,setDestinationsLoading] = useState(false);
  const [prefilling,setPrefilling] = useState(false);
  const preloadedDestinations = useRef<string | null>(null);
  const [destinationRefresh,setDestinationRefresh] = useState(0);
  const [quote,setQuote] = useState<TransferQuote | null>(null);
  const [quoting,setQuoting] = useState(false);
  const [pending,setPending] = useState<TransferAttempt | null>(null);
  const [recoveryReady,setRecoveryReady] = useState(false);
  const [recoveryError,setRecoveryError] = useState('');
  const [error,setError] = useState('');
  const [confirmed,setConfirmed] = useState(false);
  const [reviewOpen,setReviewOpen] = useState(false);
  const [submitting,setSubmitting] = useState(false);
  const [order,setOrder] = useState<TransferOrder | null>(null);
  const [loadingOrder,setLoadingOrder] = useState(false);
  const [refresh,setRefresh] = useState(0);
  const [receiptHistoryVersion,setReceiptHistoryVersion] = useState(0);
  const [receiptReview,setReceiptReview] = useState<{scope: string; item: ReceiptEmailHistoryItem} | null>(null);
  const [receiptResend,setReceiptResend] = useState<{scope: string; item: ReceiptEmailHistoryItem} | null>(null);
  const [now,setNow] = useState(Date.now);
  const quoteGeneration = useRef(0);
  const submitLock = useRef(false);
  const workspace = getSelectedTenant();
  const tenantId = workspace?.tenantId;
  const storageKey = tenantId && user?.id ? attemptStorageKey(tenantId,user.id) : null;
  const receiptScope = `${storageKey}:${orderId}`;
  const canSendReceipt = hasPermission('Payment.Read') && hasPermission('Customers.Read') && hasPermission('Customers.Write') && allowsPolicy('AdminWritePolicy');
  const scope = useRef(storageKey);
  scope.current = storageKey;
  const canConfirm = hasPermission('Payment.Create') && hasPermission('Payment.Capture') && allowsPolicy('AdminWritePolicy');
  const selected = destinations.find(item => item.externalPayoutAccountId === draft.destinationId);
  const displayQuote = pending?.quote ?? quote;
  const currentQuote = quoteIsCurrent(quote,now);
  const locked = !!pending || submitting || !!recoveryError || prefilling;

  useEffect(() => {
    if (!storageKey || orderId) return;
    const controller = new AbortController();
    preloadedDestinations.current=null;setPrefilling(false);setDestinations([]);setDestinationsLoading(false);setDestinationError('');
    quoteGeneration.current++;setQuote(null);setDraft(emptyTransferDraft());setCustomer(null);setConfirmed(false);setReviewOpen(false);setRecoveryReady(false);setSubmitting(false);setQuoting(false);setError('');
    try {
      const recovered=loadTransferAttempt(sessionStorage,storageKey);
      setPending(recovered);
      setRecoveryError('');
      // Recovery always wins over a new beneficiary link: never replace an uncertain send.
      let selection=null;
      if (!recovered) {
        try {selection=readTransferSelection(search);}
        catch(error) {setError(errorText(error,'This transfer link is invalid. Choose a customer and saved destination.'));}
      }
      if (selection && tenantId) {
        setPrefilling(true);
        const context={headers:{'X-Tenant-Id':tenantId},signal:controller.signal};
        Promise.all([customerService.get(selection.customerId,context),remittanceService.destinations(selection.customerId,context)])
          .then(([customer,result])=>{
            if (controller.signal.aborted) return;
            const destination=selectedTransferDestination(result.beneficiaries,selection.customerId,selection.destinationId);
            if (customer.partyId.toLowerCase()!==selection.customerId.toLowerCase()) throw new Error('The selected customer is unavailable. Choose a customer and saved destination.');
            preloadedDestinations.current=customer.partyId;
            setCustomer({partyId:customer.partyId,displayName:customer.displayName,partyType:customer.partyType});
            setDestinations(result.beneficiaries);
            setDraft({...emptyTransferDraft(),customerPartyId:customer.partyId,destinationId:destination.externalPayoutAccountId,destinationCurrency:destination.currency.toUpperCase()});
          })
          .catch(error=>{if (!controller.signal.aborted) setError(errorText(error,'Unable to load the selected customer and payout destination.'));})
          .finally(()=>{if (!controller.signal.aborted) setPrefilling(false);});
      }
    } catch (error) { setRecoveryError(errorText(error,'Unable to recover the previous confirmation.')); }
    setRecoveryReady(true);
    return ()=>controller.abort();
  },[storageKey,orderId,search,tenantId]);

  useEffect(() => {
    if (orderId) return;
    setCatalogReady(false);
    let cancelled = false;
    Promise.all([catalogService.getTenantCountries(),catalogService.getTenantCurrencies()]).then(([countryResult,currencyResult]) => {
      if (cancelled) return;
      setCountries(countryResult.countries);setCurrencies(currencyResult.currencies);setCatalogReady(true);
    }).catch(error => {if (!cancelled) setError(errorText(error,'Unable to load corridor configuration.'));});
    return () => {cancelled=true;};
  },[orderId,storageKey]);

  useEffect(() => {
    if (!draft.customerPartyId || orderId || !tenantId || prefilling) return;
    if (preloadedDestinations.current===draft.customerPartyId) {preloadedDestinations.current=null;return;}
    preloadedDestinations.current=null;
    const controller = new AbortController();
    setDestinationsLoading(true);setDestinations([]);setDestinationError('');
    remittanceService.destinations(draft.customerPartyId,{headers:{'X-Tenant-Id':tenantId},signal:controller.signal}).then(result => {if (!controller.signal.aborted) setDestinations(result.beneficiaries);}).catch(error => {if (!controller.signal.aborted) setDestinationError(errorText(error,'Unable to load destinations.'));}).finally(() => {if (!controller.signal.aborted) setDestinationsLoading(false);});
    return () => controller.abort();
  },[draft.customerPartyId,orderId,storageKey,tenantId,destinationRefresh,prefilling]);

  useEffect(() => {
    if (!orderId || !tenantId) {setOrder(null);return;}
    const controller = new AbortController();
    setLoadingOrder(true);setError('');setOrder(null);setReceiptResend(null);setReceiptReview(null);
    remittanceService.get(orderId,{headers:{'X-Tenant-Id':tenantId},signal:controller.signal}).then(result => {if (!controller.signal.aborted) setOrder(result);}).catch(error => {if (!controller.signal.aborted) setError(errorText(error,'Unable to load this transfer.'));}).finally(() => {if (!controller.signal.aborted) setLoadingOrder(false);});
    return () => controller.abort();
  },[orderId,refresh,storageKey,tenantId]);

  useEffect(() => {const timer=window.setInterval(()=>setNow(Date.now()),1000);return ()=>window.clearInterval(timer);},[]);
  useEffect(() => () => {quoteGeneration.current++;},[]);

  function update(patch: Partial<TransferDraft>) {
    if (locked) return;
    quoteGeneration.current++;setQuoting(false);setQuote(null);setConfirmed(false);setError('');
    setDraft(previous=>({...previous,...patch}));
  }

  async function getQuote() {
    if (!tenantId) {setError('Choose a workspace before pricing a transfer.');return;}
    const message=transferDraftError(draft);
    if (message) {setError(message);return;}
    if (!selected?.isVerified) {setError('Choose a verified payout destination.');return;}
    const generation=++quoteGeneration.current;
    setQuoting(true);setError('');setQuote(null);setConfirmed(false);
    try {
      const result=await remittanceService.quote(quoteRequest(draft),{headers:{'X-Tenant-Id':tenantId}});
      if (generation!==quoteGeneration.current) return;
      if (!destinationMatchesQuote(selected,result,draft.customerPartyId)) throw new Error('The selected destination is not supported by this corridor. Choose another destination or corridor.');
      setQuote(result);setNow(Date.now());
    } catch (error) {if (generation===quoteGeneration.current) setError(errorText(error,'Unable to price this transfer.'));}
    finally {if (generation===quoteGeneration.current) setQuoting(false);}
  }

  async function send() {
    if (submitLock.current || !canConfirm || !confirmed || !storageKey || !recoveryReady || recoveryError) return;
    submitLock.current=true;setSubmitting(true);setError('');
    const activeScope=storageKey;
    let attempt=pending;
    try {
      if (!attempt) {
        if (!quote || !selected) throw new Error('Get a current quote before confirming.');
        attempt={key:crypto.randomUUID(),request:confirmRequest(draft,quote,selected,Date.now()),quote,maskedDestination:`${selected.destinationType} · ${selected.maskedAccountIdentifier}`};
        // Persist only references, pricing and the operator note before the first send. A retry/reload
        // reuses this exact request/key even when the original HTTP response was lost.
        sessionStorage.setItem(activeScope,JSON.stringify(attempt));
        setPending(attempt);
      }
      const result=await remittanceService.confirm(attempt.request,attempt.key,{headers:{'X-Tenant-Id':tenantId!}});
      sessionStorage.removeItem(activeScope);
      if (scope.current!==activeScope) return;
      setPending(null);setReviewOpen(false);navigate(`/orders/transfers/${result.orderId}`,{replace:true});
    } catch (error) {
      if (scope.current===activeScope) {
        if (transferWasNotStarted(error)) {
          try {sessionStorage.removeItem(activeScope);setPending(null);setQuote(null);setConfirmed(false);setReviewOpen(false);}
          catch {setRecoveryError('The rejected confirmation could not be cleared. Check transfer activity before continuing.');}
        }
        setError(errorText(error,'Confirmation did not complete. Retry the same request to recover its result.'));
      }
    }
    finally {submitLock.current=false;if (scope.current===activeScope) setSubmitting(false);}
  }

  return <div className="mx-auto flex w-full max-w-7xl flex-col gap-6 p-6 md:p-8">
    <div className="flex flex-wrap items-center justify-between gap-4"><div><Link className="mb-3 inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground" to="/orders/activity">← Transfer orders</Link><h1 className="flex items-center gap-3 text-2xl font-semibold tracking-tight">{glyph('transfer-orders')}{orderId ? 'Transfer details' : 'New transfer'}</h1><p className="mt-2 text-sm text-muted-foreground">{orderId ? 'The order, destination and execution references in one place.' : 'Choose a customer, price the corridor, and review the transfer before sending.'}</p></div>{orderId && <div className="flex flex-wrap items-start gap-3"><Button variant="outline" disabled={loadingOrder} onClick={()=>setRefresh(value=>value+1)}>{glyph('transactions')} Refresh status</Button>{order && tenantId && <><TransferReceiptButton order={order} workspace={{tenantId,name:workspace?.name}} onRefresh={setOrder} /><SendReceiptButton order={order} tenantId={tenantId} onUpdated={()=>setReceiptHistoryVersion(value=>value+1)} resendFrom={receiptResend?.scope===receiptScope ? receiptResend.item : null} onResendClose={()=>setReceiptResend(null)} /></>}</div>}</div>
    {error && <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert>}
    {canSendReceipt && order && tenantId && receiptReview?.scope===receiptScope && <ReviewReceiptEmailDialog key={`${receiptScope}:${receiptReview.item.requestId}`} orderId={order.orderId} tenantId={tenantId} item={receiptReview.item} onClose={()=>setReceiptReview(null)} onContinue={()=>{setReceiptResend(receiptReview);setReceiptReview(null);}} />}
    {orderId ? (loadingOrder ? <p role="status" className="text-sm text-muted-foreground">Loading transfer…</p> : order && <><TransferDetails order={order} />{tenantId && hasPermission('Customers.Read') && allowsPolicy('AdminReadPolicy') && <ReceiptEmailHistory key={`${storageKey}:${order.orderId}:${receiptHistoryVersion}`} orderId={order.orderId} tenantId={tenantId} onResend={canSendReceipt ? item=>setReceiptResend({scope:receiptScope,item}) : undefined} onReviewEmail={canSendReceipt ? item=>{setReceiptResend(null);setReceiptReview({scope:receiptScope,item});} : undefined} />}</>) : <>
      {recoveryError && <Alert variant="destructive"><AlertDescription>{recoveryError}</AlertDescription></Alert>}
      {prefilling && <p role="status" className="text-sm text-muted-foreground">Loading customer and payout destination…</p>}
      {pending && <Alert><AlertDescription>A transfer confirmation is awaiting a known result. Review the original request and retry it with the same reference. Editing is locked to prevent a second transfer.</AlertDescription></Alert>}
      <div className="grid items-start gap-6 lg:grid-cols-3"><div className="flex flex-col gap-6 lg:col-span-2">
        <Card><CardHeader><CardTitle className="flex items-center gap-2">{glyph('customers')} Customer & recipient</CardTitle><CardDescription>Use a saved, verified destination belonging to the selected customer.</CardDescription></CardHeader><CardContent><fieldset disabled={locked} className="flex min-w-0 flex-col gap-5">
          <PartyPicker label="Sending customer" value={draft.customerPartyId} preloaded={customer} allowCreate={false} onChange={(id,option)=>{setCustomer(option);setDestinations([]);update({customerPartyId:id,destinationId:''});}} />
          <Field><FieldLabel htmlFor="destination">Saved payout destination</FieldLabel><NativeSelect id="destination" value={draft.destinationId} disabled={!draft.customerPartyId || destinationsLoading} onChange={event=>{const destination=destinations.find(item=>item.externalPayoutAccountId===event.target.value);update({destinationId:event.target.value,destinationCurrency:destination?.currency.toUpperCase() || ''});}}><option value="">{destinationsLoading ? 'Loading destinations…' : 'Choose a destination'}</option>{destinations.map(destination=><option key={destination.externalPayoutAccountId} value={destination.externalPayoutAccountId} disabled={!destination.isVerified}>{destination.beneficiaryName} · {destination.maskedAccountIdentifier} · {destination.currency}{!destination.isVerified ? ' · Not verified' : ''}</option>)}</NativeSelect><FieldDescription>Only verified destinations can receive a transfer. Account numbers stay masked.</FieldDescription></Field>
          <div className="flex flex-wrap items-center justify-between gap-3 text-sm">{isPathVisible('/remittance/beneficiaries') && <Link className="inline-flex items-center gap-2 text-primary hover:underline" to="/remittance/beneficiaries" state={{customer}}>{glyph('beneficiaries')}Manage beneficiaries</Link>}<Button variant="ghost" size="sm" disabled={!draft.customerPartyId || destinationsLoading || locked} onClick={()=>{update({destinationId:''});setDestinationRefresh(value=>value+1);}}>Refresh destinations</Button></div>
          {destinationError && <p role="alert" className="text-sm text-destructive">{destinationError}</p>}{draft.customerPartyId && !destinationsLoading && !destinationError && destinations.length===0 && <p className="text-sm text-muted-foreground">No saved destinations are available for this customer. A verified payout beneficiary must be configured before sending.</p>}
        </fieldset></CardContent></Card>
        <Card><CardHeader><CardTitle className="flex items-center gap-2">{glyph('corridors')} Corridor & amount</CardTitle><CardDescription>Compare the sending amount, exchange rate and recipient amount.</CardDescription></CardHeader><CardContent><fieldset disabled={locked || !catalogReady} className="grid min-w-0 gap-5 sm:grid-cols-2">
          <Field><FieldLabel htmlFor="origin-country">Sending country</FieldLabel><NativeSelect id="origin-country" value={draft.originCountry} onChange={event=>update({originCountry:event.target.value})}><option value="">Select country</option>{countries.map(country=><option key={country.countryCode} value={country.countryCode}>{country.name}</option>)}</NativeSelect></Field>
          <Field><FieldLabel htmlFor="destination-country">Receiving country</FieldLabel><NativeSelect id="destination-country" value={draft.destinationCountry} onChange={event=>update({destinationCountry:event.target.value})}><option value="">Select country</option>{countries.map(country=><option key={country.countryCode} value={country.countryCode}>{country.name}</option>)}</NativeSelect></Field>
          <Field><FieldLabel htmlFor="origin-currency">Sending currency</FieldLabel><NativeSelect id="origin-currency" value={draft.originCurrency} onChange={event=>update({originCurrency:event.target.value})}><option value="">Select currency</option>{currencies.map(currency=><option key={currency.code} value={currency.code}>{currency.code} · {currency.name}</option>)}</NativeSelect></Field>
          <Field><FieldLabel htmlFor="destination-currency">Receiving currency</FieldLabel><Input id="destination-currency" value={draft.destinationCurrency} readOnly placeholder="From the saved destination" /></Field>
          <Field><FieldLabel htmlFor="amount-side">Amount basis</FieldLabel><NativeSelect id="amount-side" value={draft.amountSide} onChange={event=>update({amountSide:event.target.value as TransferDraft['amountSide']})}><option value="send">Customer sends</option><option value="receive">Recipient receives</option></NativeSelect></Field>
          <Field><FieldLabel htmlFor="transfer-amount">Amount · {draft.amountSide==='send' ? draft.originCurrency || 'sending currency' : draft.destinationCurrency || 'receiving currency'}</FieldLabel><Input id="transfer-amount" inputMode="decimal" autoComplete="off" placeholder="0.00" value={draft.amount} onChange={event=>update({amount:event.target.value})} /></Field>
          <Field><FieldLabel htmlFor="transfer-purpose">Purpose code</FieldLabel><Input id="transfer-purpose" maxLength={64} placeholder="Enter the transfer purpose code" value={draft.purposeCode} onChange={event=>update({purposeCode:event.target.value})} /></Field>
          <Field><FieldLabel htmlFor="transfer-note">Transfer note <span className="text-muted-foreground">(optional)</span></FieldLabel><Textarea id="transfer-note" maxLength={256} rows={2} value={draft.narration} onChange={event=>update({narration:event.target.value})} /></Field>
        </fieldset><Button className="mt-6" onClick={()=>void getQuote()} disabled={locked || quoting || !catalogReady || !recoveryReady || !selected?.isVerified}>{glyph('fx')}{quoting ? 'Getting quote…' : 'Get transfer quote'}</Button></CardContent></Card>
      </div>
      <Card className="lg:sticky lg:top-6"><CardHeader><CardTitle className="flex items-center gap-2">{glyph('invoices')} Transfer review</CardTitle><CardDescription>{displayQuote ? 'Review the amounts and destination.' : 'Get a quote to see fees and the total debit.'}</CardDescription></CardHeader><CardContent>
        {displayQuote ? <><Badge variant={pending || quoteIsCurrent(displayQuote,now) ? 'secondary' : 'destructive'}>{pending ? 'Confirmation pending' : currentQuote ? 'Quote available' : 'Quote expired'}</Badge><dl className="my-4 divide-y divide-border"><SummaryLine label="Customer sends" value={money(displayQuote.originAmount,displayQuote.originCurrency)} /><SummaryLine label="Fees" value={money(displayQuote.feesTotal,displayQuote.originCurrency)} /><SummaryLine label="Total debit" value={money(displayQuote.totalAmount,displayQuote.originCurrency)} /><SummaryLine label="Recipient receives" value={money(displayQuote.destinationAmount,displayQuote.destinationCurrency)} /><SummaryLine label="FX rate" value={`1 ${displayQuote.originCurrency} = ${displayQuote.exchangeRate} ${displayQuote.destinationCurrency}`} /><SummaryLine label="Destination" value={pending?.maskedDestination || `${selected?.destinationType} · ${selected?.maskedAccountIdentifier}`} />{pending && <SummaryLine label="Customer reference" value={pending.request.customerPartyId} />}{pending && <SummaryLine label="Purpose" value={pending.request.purposeCode} />}</dl>
          {displayQuote.feeBreakdown.length>0 && <div className="mb-4 rounded-lg border bg-muted p-3"><h2 className="mb-2 text-xs font-medium">Fee breakdown</h2>{displayQuote.feeBreakdown.map((fee,index)=><div key={`${fee.code}-${index}`} className="flex justify-between gap-3 py-1 text-xs"><span>{fee.description || fee.code}</span><span>{money(fee.amount,fee.currency)}</span></div>)}</div>}
          <p className="break-all text-xs text-muted-foreground">Quote {displayQuote.pricingQuoteId}<br/>Policy {displayQuote.pricingPolicyVersion}<br/>Valid until {new Date(displayQuote.expiresAt).toLocaleString()}</p>
          <div className="my-5 flex items-start gap-2"><Checkbox id="transfer-reviewed" checked={confirmed} onCheckedChange={value=>setConfirmed(value===true)} disabled={submitting} /><label htmlFor="transfer-reviewed" className="text-xs leading-relaxed">I have reviewed the customer, destination, fees and total debit, and authorise this transfer.</label></div>
          <Button className="w-full" disabled={!canConfirm || !confirmed || submitting || (!pending && !currentQuote)} onClick={()=>setReviewOpen(true)}>{glyph('transfer-orders')}{pending ? 'Review & retry confirmation' : 'Review & confirm transfer'}</Button>{!canConfirm && <p className="mt-2 text-xs text-muted-foreground">Transfer execution requires payment capture permission and staff write access.</p>}
        </> : <div className="flex flex-col gap-3 rounded-lg border border-dashed p-5 text-sm text-muted-foreground">{glyph('fx')}No transfer quote yet. Amounts and fees will appear here.</div>}
        <p className="mt-5 flex gap-2 text-xs leading-relaxed text-muted-foreground">{glyph('compliance')}Confirmation records the order, posts the debit and initiates its payout through the configured service.</p>
      </CardContent></Card></div>
      <Dialog open={reviewOpen} onOpenChange={open=>{if (!submitting) setReviewOpen(open);}}><DialogContent><DialogHeader><DialogTitle>{pending ? 'Retry this transfer confirmation?' : 'Confirm this transfer?'}</DialogTitle><DialogDescription>This action authorises the debit and payout. Confirm the exact amounts and destination below.</DialogDescription></DialogHeader>{displayQuote && <dl><SummaryLine label="Debit" value={money(displayQuote.totalAmount,displayQuote.originCurrency)} /><SummaryLine label="Receive" value={money(displayQuote.destinationAmount,displayQuote.destinationCurrency)} /><SummaryLine label="Destination" value={pending?.maskedDestination || selected?.maskedAccountIdentifier || ''} /><SummaryLine label="Purpose" value={pending?.request.purposeCode || draft.purposeCode} /></dl>}<DialogFooter><Button variant="outline" disabled={submitting} onClick={()=>setReviewOpen(false)}>Back to review</Button><Button disabled={submitting || !confirmed || !canConfirm || (!pending && !quoteIsCurrent(quote,now))} onClick={()=>void send()}>{submitting ? 'Confirming…' : pending ? 'Retry same transfer' : 'Confirm & send transfer'}</Button></DialogFooter></DialogContent></Dialog>
    </>}
  </div>;
}
