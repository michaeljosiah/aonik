import { useCallback, useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { AonikTemplateIcon } from '@/components/layout/aonik/AonikTemplateIcon';
import { useAuth } from '@/auth';
import { getSelectedTenant } from '@/lib/tenantContext';
import { PublishingWorkspaceView } from './PublishingViews';
import { reasonLabel } from './presentation';
import { publishingApi, publishingProblem } from './api';
import type { ActionKind, ApprovedRelease, DialogState, DialogValues, Edition, Operation, WorkspaceActions, WorkspaceModel } from './types';
import './publishing.css';

const initial:WorkspaceModel={view:'queue',channels:[],channelKey:'',queue:null,review:null,release:null,catalogue:null,tab:'Overview',status:'in-review',query:'',page:1,loading:true,error:null,notice:'',dialog:null,busy:false,dialogError:''};
export function PublishingPage(){
  const {pathname}=useLocation();const {id}=useParams();const navigate=useNavigate();const auth=useAuth();
  const tenant=getSelectedTenant();const scope=auth.user?.id+'|'+(typeof tenant==='string'?tenant:JSON.stringify(tenant));
  const view:WorkspaceModel['view']=pathname.startsWith('/arke/catalogue')?'catalogue':id?'detail':'queue';
  const [m,set]=useState<WorkspaceModel>({...initial,view});const [refresh,setRefresh]=useState(0);const [search,setSearch]=useState('');
  const generation=useRef(0);const latest=useRef(m);latest.current=m;
  const operation=useRef<{key:string;id:string}|null>(null);const dialogRef=useRef<DialogState|null>(null);const pending=useRef(false);
  const scopeRef=useRef(scope);scopeRef.current=scope;const actionSequence=useRef(0);
  useEffect(()=>{const timer=setTimeout(()=>{set(old=>({...old,query:search,page:1}));},250);return()=>clearTimeout(timer);},[search]);
  useEffect(()=>{generation.current++;actionSequence.current++;pending.current=false;set({...initial,view:latest.current.view,loading:true});setSearch('');operation.current=null;dialogRef.current=null;},[scope]); // user/tenant scope never carries private view state forward
  useEffect(()=>{set(old=>({...old,view,queue:null,review:null,release:null,catalogue:null,dialog:null,error:null,artworkUrl:undefined,artworkError:undefined,page:1,tab:'Overview'}));dialogRef.current=null;operation.current=null;},[view,id]);
  const channelKey=m.channelKey;const page=m.page;const status=m.status;const query=m.query;
  useEffect(()=>{
    const controller=new AbortController();const current=++generation.current;let timer:ReturnType<typeof setTimeout>|undefined;
    const load=async(poll=false)=>{
      if(!poll)set(old=>({...old,loading:true,error:null}));
      try{
        const channels=await publishingApi.channels(controller.signal);
        const channel=channels.find(c=>c.key===channelKey)??channels[0];const key=channel?.key??'';
        if(controller.signal.aborted||current!==generation.current)return;
        if(key!==channelKey){set(old=>({...old,channels,channelKey:key,loading:!!key,queue:null,review:null,release:null,catalogue:null}));if(key)return;}
        if(!channel){set(old=>({...old,channels:[],loading:false,queue:null,review:null,release:null,catalogue:null}));return;}
        let queue=null,review=null,release=null,catalogue=null;
        if(view==='queue'&&channel.canReview)queue=await publishingApi.queue(key,status,query,page,controller.signal);
        if(view==='catalogue')catalogue=await publishingApi.catalogue(key,page,controller.signal);
        if(view==='detail'&&id){[review,release]=await Promise.all([publishingApi.review(id,controller.signal),publishingApi.release(id,controller.signal)]);}
        if(controller.signal.aborted||current!==generation.current)return;
        if(review&&review.submission.channelKey!==key){set(old=>({...old,channelKey:review.submission.channelKey,review:null,release:null,channels,loading:true}));return;}
        set(old=>({...old,view,channels,queue,review,release,catalogue,loading:false,error:null,
          dialog:old.dialog&&review&&old.dialog.review&&old.dialog.review.submission.version!==review.submission.version?{...old.dialog,stale:true}:old.dialog}));
        const ops=release?.operations??catalogue?.operations??[];
        if(ops.some(o=>['pending','copying','uncertain'].includes(o.status))&&document.visibilityState==='visible')timer=setTimeout(()=>void load(true),5000);
      }catch(error){if(!controller.signal.aborted&&current===generation.current)set(old=>({...old,loading:false,error:publishingProblem(error),queue:null,review:null,release:null,catalogue:null}));}
    };
    void load();return()=>{controller.abort();if(timer)clearTimeout(timer);};
  },[scope,view,id,channelKey,page,status,query,refresh]);
  const imageRevision=m.review?.submission.revision;const imageRevisionId=imageRevision?.id;const imageAsset=imageRevision?.editorialMaterial?.artworkAssetId;
  useEffect(()=>{
    const controller=new AbortController();let url:string|undefined;
    set(old=>({...old,artworkUrl:undefined,artworkError:undefined}));
    if(id&&imageRevisionId&&imageAsset&&m.review?.validation?.status==='passed')void publishingApi.image(id,imageRevisionId,imageAsset,controller.signal).then(blob=>{
      if(controller.signal.aborted)return;url=URL.createObjectURL(blob);set(old=>({...old,artworkUrl:url}));
    }).catch(()=>{if(!controller.signal.aborted)set(old=>({...old,artworkError:'Private artwork preview unavailable'}));});
    return()=>{controller.abort();if(url)URL.revokeObjectURL(url);};
  },[scope,id,imageRevisionId,imageAsset,m.review?.validation?.status]);
  const reload=useCallback(()=>setRefresh(n=>n+1),[]);
  const open=async(kind:ActionKind,item?:Edition|Operation|ApprovedRelease)=>{
    if(pending.current)return;
    const current=latest.current;const review=current.review;const channel=current.channels.find(c=>c.key===current.channelKey);
    let preparedRelease=current.release;
    if(item&&'receiptId'in item){
      const sourceScope=scopeRef.current;const sequence=++actionSequence.current;pending.current=true;set(old=>({...old,busy:true}));
      try{preparedRelease=await publishingApi.release(item.receiptId);if(scopeRef.current!==sourceScope||sequence!==actionSequence.current)return;}
      catch(error){if(scopeRef.current===sourceScope)set(old=>({...old,notice:publishingProblem(error).message}));return;}
      finally{if(sequence===actionSequence.current){pending.current=false;set(old=>({...old,busy:false}));}}
      if(preparedRelease.blockingCode&&kind==='publish'){set(old=>({...old,notice:reasonLabelForRelease(preparedRelease!.blockingCode)}));return;}
    }
    const dialog:DialogState={kind,title:review?.submission.revision.title??(item&&'title'in item?item.title:channel?.key??'Publication operation'),
      receiptId:item&&'receiptId'in item?item.receiptId:review?.submission.id,
      revision:item&&'receiptId'in item?preparedRelease?.metadata:review?.submission.revision,digest:review?.snapshot?.digest??preparedRelease?.reviewDigest,review:review??undefined,release:preparedRelease??undefined,
      channel,edition:kind==='withdraw'?item as Edition:undefined,operation:kind==='retry'?item as Operation:undefined};
    if(item&&'receiptId'in item)dialog.title=preparedRelease?.metadata.title??item.metadata.title;
    dialogRef.current=dialog;operation.current=null;set(old=>({...old,dialog,dialogError:''}));
  };
  const confirm=async(values:DialogValues)=>{
    if(pending.current)return;const d=dialogRef.current;if(!d||d.stale||latest.current.dialog?.stale)return;
    const receipt=d.review?.submission;const revision=d.revision;const release=d.release;
    const targetReceipt=d.receiptId??receipt?.id;
    const key=JSON.stringify({scope:scopeRef.current,kind:d.kind,receiptId:targetReceipt,revisionId:revision?.id,digest:d.digest,
      version:release?.version??receipt?.version??d.edition?.version??d.operation?.version??d.channel?.version,channel:d.channel?.key,edition:d.edition?.id,operation:d.operation?.id,values});
    if(!operation.current||operation.current.key!==key)operation.current={key,id:crypto.randomUUID()};
    const sourceScope=scopeRef.current;const sequence=++actionSequence.current;
    const operationId=operation.current.id;pending.current=true;set(old=>({...old,busy:true,dialogError:''}));
    try{
      let message='The service recorded this request.';
      if(['approve','request-changes','reject'].includes(d.kind)&&receipt&&revision&&d.digest){
        await publishingApi.decide(receipt.id,{operationId,revisionId:revision.id,reviewDigest:d.digest,expectedVersion:receipt.version,decision:d.kind,
          internalReason:values.reason,creatorMessage:values.creatorMessage,checks:{distributionRightsReviewed:values.rights,attributionReviewed:values.attribution,audienceSafetyReviewed:values.audience}});
        message=d.kind==='approve'?'Revision approved. Publication is a separate confirmed action.':d.kind==='request-changes'?'Changes requested. The creator can submit a fresh revision.':'Rejection recorded for this exact revision.';
      }else if(d.kind==='publish'&&targetReceipt&&release){
        const result=await publishingApi.publish(targetReceipt,{operationId,revisionId:release.revisionId,decisionId:release.decisionId,reviewDigest:release.reviewDigest,expectedVersion:release.version,
          expectedSerial:release.serial,expectedBaseFingerprint:release.currentBaseFingerprint,expectedSlotFingerprint:release.currentSlotFingerprint,confirmBaseReplacement:values.replace,reason:values.reason});
        message=result.status==='committed'?'Publication confirmed at serial '+result.committedSerial+'.':'Publication queued. The catalogue updates after delivery checks pass.';
      }else if(d.kind==='withdraw'&&d.edition){await publishingApi.withdraw(d.edition.id,{operationId,expectedVersion:d.edition.version,reason:values.reason});message='Withdrawal queued. Existing copies and cached links may remain.';
      }else if(d.kind==='retry'&&d.operation){await publishingApi.retry(d.operation.id,{operationId,expectedVersion:d.operation.version,reason:values.reason});message='The same delivery operation is queued for reconciliation.';
      }else if(d.kind==='initialise'&&d.channel){await publishingApi.initialise(d.channel.key,{operationId,baselineSha256:d.channel.baselineSha256,reason:values.reason});message='The qualified catalogue baseline is initialised.';
      }else if(d.kind==='mirror'&&d.channel){await publishingApi.mirror(d.channel.key,{operationId,expectedVersion:d.channel.version,reason:values.reason});message='The latest canonical head is queued for mirror reconciliation.';
      }else if((d.kind==='hold'||d.kind==='unhold')&&targetReceipt){await publishingApi.hold(targetReceipt,{operationId,expectedVersion:release?.version??receipt?.version,reason:values.reason,hold:d.kind==='hold'});message=d.kind==='hold'?'Future publication is held. Existing public entries require a separate withdrawal.':'The publication hold is cleared; all release checks still apply.';
      }else throw new Error('The confirmation context is unavailable.');
      if(scopeRef.current!==sourceScope||sequence!==actionSequence.current)return;
      dialogRef.current=null;operation.current=null;set(old=>({...old,dialog:null,dialogError:'',notice:message}));reload();
    }catch(error){if(scopeRef.current!==sourceScope||sequence!==actionSequence.current)return;const problem=publishingProblem(error);const stale=problem.code.includes('changed')||problem.code.includes('conflict');
      if(stale&&dialogRef.current)dialogRef.current={...dialogRef.current,stale:true};
      const uncertain=problem.code==='service-unavailable';if(uncertain&&dialogRef.current)dialogRef.current={...dialogRef.current,uncertain:true};
      set(old=>({...old,dialog:old.dialog?{...old.dialog,stale:stale||old.dialog.stale,uncertain:uncertain||old.dialog.uncertain}:null,dialogError:problem.message}));
    }finally{if(sequence===actionSequence.current){pending.current=false;if(scopeRef.current===sourceScope)set(old=>({...old,busy:false}));}}
  };
  const download=async()=>{const r=latest.current.review?.submission;if(!r)return;try{const blob=await publishingApi.download(r.id,r.revision.id);const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download=r.revision.packageFileName;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}catch(error){set(old=>({...old,notice:publishingProblem(error).message}));}};
  const actions:WorkspaceActions={navigate:(target,record)=>navigate(target==='detail'?'/arke/submissions/'+encodeURIComponent(record??''):target==='catalogue'?'/arke/catalogue':'/arke/submissions'),
    channel:key=>{set(old=>({...old,channelKey:key,page:1,queue:null,review:null,release:null,catalogue:null,dialog:null,notice:''}));if(view==='detail')navigate('/arke/submissions');},
    tab:tab=>set(old=>({...old,tab})),status:status=>set(old=>({...old,status,page:1})),query:setSearch,page:page=>set(old=>({...old,page})),reload,open,
    close:()=>{if(!pending.current){dialogRef.current=null;operation.current=null;set(old=>({...old,dialog:null,dialogError:''}));}},confirm:values=>void confirm(values),download:()=>void download()};
  return <div className="h-full overflow-auto" data-publishing-runtime><PublishingWorkspaceView model={{...m,view,query:search}} actions={actions} Icon={AonikTemplateIcon}/></div>;
}
function reasonLabelForRelease(code:string|null){return reasonLabel(code);}
