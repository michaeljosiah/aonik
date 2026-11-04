function ScreenArkePublishing({scene='queue',onBack}){
  const [m,set]=React.useState(()=>publishingExample(scene));
  const Icon=ArkePublishingUI.AonikTemplateIcon;
  const update=values=>set(old=>({...old,...values}));
  const actions={navigate:(view)=>update({view,dialog:null,error:null,notice:''}),channel:()=>{},tab:tab=>update({tab}),status:status=>update({status}),query:query=>update({query}),page:page=>update({page}),
    reload:()=>set({...publishingExample(scene),notice:'Example refreshed. No live requests were made.'}),close:()=>update({dialog:null}),download:()=>update({notice:'Private package download is shown here as an example. No creator files are included.'}),
    open:(kind,item)=>update({dialog:{kind,title:item?.title||item?.metadata?.title||m.review.submission.revision.title,receiptId:AP_IDS.receipt,revision:AP_REVISION,digest:AP_HASH,review:m.review,release:m.release,edition:kind==='withdraw'?item:undefined,operation:kind==='retry'?item:undefined,channel:m.channels[0]}}),
    confirm:values=>{
      if(m.dialog?.stale)return;
      const kind=m.dialog?.kind;
      const review=structuredClone(m.review),release=structuredClone(m.release),catalogue=structuredClone(m.catalogue);
      if(['approve','request-changes','reject'].includes(kind)){
        review.submission.status=kind==='approve'?'approved':kind==='reject'?'rejected':'changes-requested';review.blockingCode='submission-not-in-review';
        if(kind==='approve'){release.status='approved';release.decisionId=AP_IDS.decision;release.blockingCode=null;catalogue.approved=[{receiptId:AP_IDS.receipt,metadata:AP_METADATA,status:'approved',version:release.version,holdReason:null}];}
        review.decisions.unshift({id:AP_IDS.decision,operationId:'example-local-decision',revisionId:AP_IDS.revision,reviewDigest:AP_HASH,decision:kind,internalReason:values.reason,creatorMessage:values.creatorMessage,actorUserId:'editor-maya',actorPartyId:'editorial-team',policyVersion:'review-v1',decidedAt:'2026-11-04T10:12:00Z',isActive:true});
      }
      if(kind==='publish'||kind==='retry'){release.operations=[{...AP_OPERATION,status:'pending',code:'queued',canRetry:false}];release.status='publishing';catalogue.operations=release.operations;}
      if(kind==='withdraw')catalogue.operations=[{...AP_OPERATION,kind:'withdraw',editionId:AP_EDITION.id,status:'pending',code:'queued',canRetry:false}];
      update({review,release,catalogue,dialog:null,notice:['publish','withdraw','retry'].includes(kind)?'Example operation queued. Public changes require successful worker checks.':'Example decision recorded locally. No live publication occurred.'});
    }};
  const rows=m.queue.items.filter(r=>(!m.status||r.status===m.status)&&(!m.query||r.revision.title.toLowerCase().includes(m.query.toLowerCase())));
  const model={...m,queue:{...m.queue,items:rows,totalCount:rows.length}};
  return <div className="ap-example-shell"><aside className="ap-example-nav"><div className="ap-example-brand">AONIK<span>ARKE WORLDS</span></div><p>Workspace</p>{[['menu-overview','Overview','overview'],['menu-worlds','Worlds','overview'],['menu-submissions','Submissions','queue'],['menu-catalogue','Player catalogue','catalogue']].map(([icon,label,view])=><button key={label} className={m.view===(view==='queue'?'queue':view==='catalogue'?'catalogue':'none')?'active':''} onClick={()=>view==='overview'&&onBack?onBack():actions.navigate(view==='catalogue'?'catalogue':'queue')}><Icon name={icon} size={19}/>{label}</button>)}<div className="ap-example-nav-bottom"><img src={AonikAgentPortraits.resolve('aonik')} alt="AONIK assistant"/><span>AONIK<span>Here when you need me</span></span></div></aside><div className="ap-example-main"><header className="ap-example-top"><span>Arke Worlds <span className="ap-muted">/ Publishing</span></span><div><Icon name="search" size={18}/><Icon name="bell" size={18}/><span className="ap-example-human" aria-label="Maya Chen, editor">MC</span></div></header><div className="ap-example-scroll"><ArkePublishingUI.PublishingWorkspaceView model={model} actions={actions} Icon={Icon}/></div></div></div>;
}
function arkePublishingArtboards(){return AP_SCENES.map(scene=><DCArtboard key={'publishing-'+scene.id} id={'publishing-'+scene.id} label={scene.title} width={1440} height={1050}><ScreenArkePublishing scene={scene.id}/></DCArtboard>);}
