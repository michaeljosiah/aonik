import { useEffect, useRef, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';

import { AonikTemplateIcon } from '@/components/layout/aonik/AonikTemplateIcon';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from '@/components/ui/alert-dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { DataTable, DataTableRowActions } from '@/components/ui/data-table';
import type { ColumnDef } from '@/components/ui/data-table';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { NativeSelect } from '@/components/ui/native-select';
import { Sheet, SheetBody, SheetContent, SheetFooter, SheetHeader } from '@/components/ui/sheet';
import { Textarea } from '@/components/ui/textarea';
import { getSelectedTenant } from '@/lib/tenantContext';
import { useModules } from '@/modules/useModules';
import { PartyPicker } from '@/pages/orders/PartyPicker';
import type { PartyPickerOption } from '@/pages/orders/PartyPicker';
import { transferEditorPath } from '@/pages/orders/lib/transferSelection';
import { beneficiaryService } from '@/services/beneficiaryService';
import type { ManagedBeneficiary } from '@/services/beneficiaryService';
import { catalogService } from '@/services/catalogService';
import type { CatalogCountryItem, CatalogCurrencyItem } from '@/types';

import { BeneficiaryForm } from './BeneficiaryForm';
import { beneficiaryError } from './lib/beneficiaryEditor';

const icon=(name: string,size=18)=><AonikTemplateIcon name={`menu-${name}`} size={size} />;
const railName=(rail: string)=>rail==='MobileMoney' ? 'Mobile money' : rail==='Bank' ? 'Bank account' : rail;
function Detail({label,value}: {label: string; value: string | null | undefined}) {
  return <div className="flex justify-between gap-5 border-b py-3 text-sm"><dt className="text-muted-foreground">{label}</dt><dd className="m-0 max-w-2/3 break-all text-right font-medium">{value || '—'}</dd></div>;
}

export function BeneficiariesPage() {
  const location=useLocation();
  const navigate=useNavigate();
  const {hasPermission,allowsPolicy,isPathVisible}=useModules();
  const tenantId=getSelectedTenant()?.tenantId;
  const [customer,setCustomer]=useState<PartyPickerOption | null>(()=>(location.state as {customer?: PartyPickerOption} | null)?.customer ?? null);
  const [items,setItems]=useState<ManagedBeneficiary[]>([]);
  const [loading,setLoading]=useState(false);
  const [refresh,setRefresh]=useState(0);
  const [error,setError]=useState('');
  const [notice,setNotice]=useState('');
  const [search,setSearch]=useState('');
  const [status,setStatus]=useState('all');
  const [countries,setCountries]=useState<CatalogCountryItem[]>([]);
  const [currencies,setCurrencies]=useState<CatalogCurrencyItem[]>([]);
  const [catalogError,setCatalogError]=useState('');
  const [creating,setCreating]=useState(false);
  const [recipientId,setRecipientId]=useState('');
  const [busy,setBusy]=useState(false);
  const [selected,setSelected]=useState<ManagedBeneficiary | null>(null);
  const [removing,setRemoving]=useState<ManagedBeneficiary | null>(null);
  const [editingNotes,setEditingNotes]=useState<ManagedBeneficiary | null>(null);
  const [notes,setNotes]=useState('');
  const [actionError,setActionError]=useState('');
  const actionLock=useRef(false);
  const customerId=customer?.partyId || '';
  const scope=useRef('');scope.current=`${tenantId}:${customerId}`;
  const canManage=hasPermission('Payment.Create') && hasPermission('Customers.Write') && allowsPolicy('AdminWritePolicy');
  const canOpenTransfer=isPathVisible('/orders/transfers/new');

  useEffect(()=>{
    if (!customerId || !tenantId) return;
    const controller=new AbortController();
    setLoading(true);setItems([]);setError('');
    beneficiaryService.list(customerId,{headers:{'X-Tenant-Id':tenantId},signal:controller.signal})
      .then(response=>{if (!controller.signal.aborted) setItems(response.beneficiaries);})
      .catch(error=>{if (!controller.signal.aborted) setError(beneficiaryError(error,'Unable to load saved destinations.'));})
      .finally(()=>{if (!controller.signal.aborted) setLoading(false);});
    return ()=>controller.abort();
  },[customerId,tenantId,refresh]);

  useEffect(()=>{
    if (!creating) return;
    let cancelled=false;setCatalogError('');setCountries([]);setCurrencies([]);
    Promise.all([catalogService.getTenantCountries(),catalogService.getTenantCurrencies()])
      .then(([countries,currencies])=>{if (!cancelled) {setCountries(countries.countries);setCurrencies(currencies.currencies);}})
      .catch(error=>{if (!cancelled) setCatalogError(beneficiaryError(error,'Unable to load receiving countries and currencies.'));});
    return ()=>{cancelled=true;};
  },[creating]);

  function chooseCustomer(_id: string,option: PartyPickerOption) {
    if (busy) return;
    setCustomer(option);setItems([]);setSelected(null);setSearch('');setStatus('all');setNotice('');setError('');
  }
  function addDestination(id='') {
    setRecipientId(id);setSelected(null);setActionError('');setCreating(true);
  }
  function sendTransfer(item: ManagedBeneficiary) {
    if (!customerId || !canOpenTransfer || !item.destination.isVerified || busy) return;
    navigate(transferEditorPath(customerId,item.destination.externalPayoutAccountId));
  }
  async function mutate(action: ()=>Promise<void>,success: string) {
    if (!canManage || !tenantId || actionLock.current) return;
    const activeScope=scope.current;actionLock.current=true;setBusy(true);setActionError('');
    try {
      await action();
      if (scope.current!==activeScope) return;
      setRemoving(null);setEditingNotes(null);setSelected(null);setNotice(success);setRefresh(value=>value+1);
    } catch(error) {if (scope.current===activeScope) setActionError(beneficiaryError(error,'The change could not be saved.'));}
    finally {actionLock.current=false;if (scope.current===activeScope) setBusy(false);}
  }
  const visible=items.filter(({destination})=>(status==='all' || destination.isVerified===(status==='verified')) &&
    [destination.beneficiaryName,destination.accountName,destination.maskedAccountIdentifier,destination.currency,destination.destinationType].some(value=>value.toLowerCase().includes(search.trim().toLowerCase())));
  const columns: ColumnDef<ManagedBeneficiary>[]=[
    {id:'recipient',header:'Recipient',accessorFn:item=>item.destination.beneficiaryName,sortable:true,cell:({destination})=><div><div className="font-medium">{destination.beneficiaryName}</div><div className="mt-1 text-xs text-muted-foreground">{destination.accountName}</div></div>},
    {id:'rail',header:'Destination',accessorFn:item=>item.destination.destinationType,cell:({destination})=><div className="flex items-center gap-3">{icon(destination.destinationType==='Bank' ? 'accounts' : destination.destinationType==='MobileMoney' ? 'mobile-money' : 'wallet')}<div><div>{railName(destination.destinationType)}</div><div className="mt-1 font-mono text-xs text-muted-foreground">{destination.maskedAccountIdentifier}</div></div></div>},
    {id:'currency',header:'Currency',accessorFn:item=>item.destination.currency,sortable:true},
    {id:'provider',header:'Payout provider',accessorFn:item=>item.destination.providerCode || 'Not assigned'},
    {id:'status',header:'Status',cell:({destination})=><Badge variant={destination.isVerified ? 'success' : 'secondary'}>{destination.isVerified ? 'Verified' : 'Needs verification'}</Badge>},
  ];
  return <div className="mx-auto flex w-full max-w-7xl flex-col gap-6 p-6 md:p-8">
    <div className="flex flex-wrap items-center justify-between gap-4"><div><h1 className="flex items-center gap-3 text-2xl font-semibold tracking-tight">{icon('beneficiaries',24)} Beneficiaries</h1><p className="mt-2 text-sm text-muted-foreground">Manage each customer’s recipients and saved payout destinations.</p></div><div className="flex gap-3"><Button variant="outline" disabled={!customerId || loading || busy} onClick={()=>setRefresh(value=>value+1)}>{icon('transactions',16)} Refresh</Button>{canManage && <Button disabled={!customerId || loading || !!error || busy} onClick={()=>addDestination()}>{icon('beneficiaries',16)} Add destination</Button>}</div></div>
    {notice && <Alert role="status"><AlertDescription>{notice}</AlertDescription></Alert>}
    {error && <Alert variant="destructive"><AlertDescription>{error} <Button variant="link" onClick={()=>setRefresh(value=>value+1)}>Try again</Button></AlertDescription></Alert>}
    <Card><CardHeader><CardTitle className="flex items-center gap-2">{icon('customers')} Sending customer</CardTitle><CardDescription>Destinations belong to one customer, even when recipients are shared.</CardDescription></CardHeader><CardContent><fieldset disabled={busy} className="max-w-lg"><PartyPicker label="Customer" value={customerId} preloaded={customer} allowCreate={false} onChange={chooseCustomer} /></fieldset></CardContent></Card>
    {customerId ? <Card><CardHeader><div className="flex flex-wrap items-center justify-between gap-4"><div><CardTitle>Saved payout destinations</CardTitle><CardDescription className="mt-2">{loading ? 'Loading destinations…' : `${items.length} saved · ${items.filter(item=>item.destination.isVerified).length} verified`}</CardDescription></div>{isPathVisible('/orders/transfers/new') && <Button variant="outline" asChild><Link to="/orders/transfers/new">{icon('transfer-orders',16)} New transfer</Link></Button>}</div></CardHeader><CardContent>
      <div className="mb-5 flex flex-wrap gap-3"><Input aria-label="Search destinations" placeholder="Search recipient, destination or currency" className="max-w-sm" value={search} onChange={event=>setSearch(event.target.value)} /><div className="w-52"><NativeSelect aria-label="Destination status" value={status} onChange={event=>setStatus(event.target.value)}><option value="all">All statuses</option><option value="verified">Verified</option><option value="unverified">Needs verification</option></NativeSelect></div></div>
      <DataTable data={visible} columns={columns} getRowId={item=>item.destination.externalPayoutAccountId} loading={loading} showCheckboxes={false} emptyIcon={icon('beneficiaries',28)} emptyTitle={error ? 'Destinations unavailable' : items.length ? 'No matching destinations' : 'No saved destinations yet'} emptyDescription={error ? 'Retry loading the customer’s destinations.' : items.length ? 'Try a different search or status.' : 'Add and verify a payout destination before creating a transfer.'} onRowClick={item=>{setSelected(item);setActionError('');}} rowActions={item=><div className="flex items-center justify-end gap-2">{canOpenTransfer && <Button size="sm" variant="outline" aria-label={`Send transfer to ${item.destination.beneficiaryName}`} disabled={!item.destination.isVerified || busy} title={!item.destination.isVerified ? 'Verify this destination before sending' : undefined} onClick={event=>{event.stopPropagation();sendTransfer(item);}}>{icon('transfer-orders',16)} Send transfer</Button>}<DataTableRowActions actions={[{label:'View destination',onClick:()=>setSelected(item)},...(canManage ? [{label:'Add another destination',onClick:()=>addDestination(item.destination.beneficiaryPartyId)},{label:'Edit recipient notes',onClick:()=>{setNotes(item.notes || '');setEditingNotes(item);setActionError('');}},{label:'Remove destination',onClick:()=>{setRemoving(item);setActionError('');},variant:'danger' as const}] : [])]} /></div>} />
    </CardContent></Card> : <Card><CardContent className="flex flex-col items-center gap-3 py-12 text-center text-muted-foreground">{icon('beneficiaries',32)}<p className="font-medium text-foreground">Choose a customer to manage beneficiaries</p><p className="text-sm">View saved destinations, verify new rails and maintain recipient notes.</p></CardContent></Card>}
    <Sheet open={creating} onOpenChange={open=>{if (!busy) setCreating(open);}}><SheetContent size="lg">{creating && tenantId && (catalogError ? <><SheetHeader title="Add payout destination" subtitle="Receiving configuration is unavailable." /><SheetBody><Alert variant="destructive"><AlertDescription>{catalogError}</AlertDescription></Alert><Button variant="outline" onClick={()=>setCreating(false)}>Close and try again</Button></SheetBody></> : <BeneficiaryForm customerId={customerId} tenantId={tenantId} beneficiaries={items} countries={countries} currencies={currencies} recipientId={recipientId} onBusy={setBusy} onCancel={()=>setCreating(false)} onSaved={destination=>{setCreating(false);setNotice(`${destination.beneficiaryName}’s payout destination is verified and saved.`);setRefresh(value=>value+1);}} />)}</SheetContent></Sheet>
    <Sheet open={!!selected} onOpenChange={open=>{if (!open) setSelected(null);}}><SheetContent size="md">{selected && <><SheetHeader title={selected.destination.beneficiaryName} subtitle="Saved payout destination" icon={icon('beneficiaries')} /><SheetBody><Badge className="self-start" variant={selected.destination.isVerified ? 'success' : 'secondary'}>{selected.destination.isVerified ? 'Verified' : 'Needs verification'}</Badge><dl><Detail label="Account holder" value={selected.destination.accountName} /><Detail label="Destination type" value={railName(selected.destination.destinationType)} /><Detail label="Identifier" value={selected.destination.maskedAccountIdentifier} /><Detail label="Currency" value={selected.destination.currency} /><Detail label="Bank code" value={selected.destination.bankCode} /><Detail label="Mobile network" value={selected.destination.mobileNetwork} /><Detail label="Payout provider" value={selected.destination.providerCode} /><Detail label="Destination reference" value={selected.destination.externalPayoutAccountId} /></dl><div><h2 className="mb-2 text-sm font-medium">Recipient notes</h2><p className="whitespace-pre-wrap break-words text-sm text-muted-foreground">{selected.notes || 'No notes for this customer’s recipient.'}</p></div><p className="text-xs leading-relaxed text-muted-foreground">To change bank, mobile-money or wallet details, add a newly verified destination. Removing a saved destination does not cancel transfers already confirmed.</p></SheetBody>{(canManage || canOpenTransfer) && <SheetFooter className="flex-wrap">{canManage && <><Button variant="outline" onClick={()=>{setNotes(selected.notes || '');setEditingNotes(selected);setSelected(null);setActionError('');}}>Edit recipient notes</Button><Button variant="outline" onClick={()=>addDestination(selected.destination.beneficiaryPartyId)}>Add destination</Button></>}{canOpenTransfer && <Button disabled={!selected.destination.isVerified || busy} onClick={()=>sendTransfer(selected)}>{icon('transfer-orders',16)} Send transfer</Button>}</SheetFooter>}</>}</SheetContent></Sheet>
    <Dialog open={!!editingNotes} onOpenChange={open=>{if (!open && !busy) setEditingNotes(null);}}><DialogContent><DialogHeader><DialogTitle>Edit recipient notes</DialogTitle><DialogDescription>Notes apply to this customer’s recipient across their saved destinations.</DialogDescription></DialogHeader>{actionError && <Alert variant="destructive"><AlertDescription>{actionError}</AlertDescription></Alert>}<Field><FieldLabel htmlFor="recipient-notes">Notes for {editingNotes?.destination.beneficiaryName}</FieldLabel><Textarea id="recipient-notes" rows={5} maxLength={1024} value={notes} disabled={busy} onChange={event=>setNotes(event.target.value)} /></Field><DialogFooter><Button variant="outline" disabled={busy} onClick={()=>setEditingNotes(null)}>Cancel</Button><Button disabled={busy} onClick={()=>{if (editingNotes && tenantId) void mutate(()=>beneficiaryService.updateNotes(customerId,editingNotes.destination.beneficiaryPartyId,notes,{headers:{'X-Tenant-Id':tenantId}}),'Recipient notes updated.');}}>{busy ? 'Saving…' : 'Save notes'}</Button></DialogFooter></DialogContent></Dialog>
    <AlertDialog open={!!removing} onOpenChange={open=>{if (!open && !busy) setRemoving(null);}}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>Remove this payout destination?</AlertDialogTitle><AlertDialogDescription>{removing?.destination.beneficiaryName} · {removing?.destination.maskedAccountIdentifier}. This removes the saved destination from future transfers. Other destinations and transfers already confirmed are preserved.</AlertDialogDescription></AlertDialogHeader>{actionError && <Alert variant="destructive"><AlertDescription>{actionError}</AlertDescription></Alert>}<AlertDialogFooter><AlertDialogCancel disabled={busy}>Keep destination</AlertDialogCancel><AlertDialogAction variant="destructive" disabled={busy} onClick={event=>{event.preventDefault();if (removing && tenantId) void mutate(()=>beneficiaryService.remove(customerId,removing.destination.externalPayoutAccountId,{headers:{'X-Tenant-Id':tenantId}}),'Payout destination removed.');}}>{busy ? 'Removing…' : 'Remove destination'}</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
  </div>;
}
