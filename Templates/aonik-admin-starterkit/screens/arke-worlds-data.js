// Fictional product operations. Prices are versioned reference offers, not live promises.
const AW_AREAS=[
  {id:'overview',label:'Overview',icon:'overview',scene:'overview'},
  {id:'people',label:'People',icon:'family',scene:'customers'},
  {id:'worlds',label:'Worlds & stories',icon:'worlds',scene:'worlds'},
  {id:'production',label:'Production',icon:'jobs',scene:'generation'},
  {id:'billing',label:'Billing',icon:'billing',scene:'subscriptions'},
  {id:'orders',label:'Orders & print',icon:'print',scene:'orders'},
  {id:'publishing',label:'Publishing',icon:'catalogue',scene:'submissions'},
  {id:'support',label:'Support',icon:'support',scene:'support'},
  {id:'reports',label:'Reports',icon:'usage',scene:'reports'},
];
const AW_SCENES=[...ARKE_CATALOG.map(item=>({id:item.key,area:item.area,title:item.label,icon:item.icon,subtitle:item.description})),
  {id:'customers',area:'people',title:'Customers & creators',icon:'customers',subtitle:'Adult contacts and their product relationships. Child profiles stay inside a family.'},
  {id:'family',area:'people',title:'Family account',icon:'family',subtitle:'One guardian account, with clear membership, worlds and commercial terms.'},
  {id:'world',area:'worlds',title:'World details',icon:'worlds',subtitle:'Ownership, continuity and approved editions without exposing private content.'},
  {id:'edition',area:'worlds',title:'Edition review',icon:'editions',subtitle:'Check the complete manuscript, artwork and narration before delivery.'},
  {id:'job',area:'production',title:'Generation details',icon:'jobs',subtitle:'Follow execution, filing and usage without charging for the same work twice.'},
  {id:'submission',area:'publishing',title:'Submission review',icon:'submissions',subtitle:'Review rights and the exact public package before a separate publication handoff.'},
  {id:'orders',area:'orders',title:'Orders',icon:'orders',subtitle:'Print purchases and intent, with payment and fulfilment tracked separately.'},
  {id:'invoice',area:'billing',title:'Invoice details',icon:'invoices',subtitle:'Commercial terms, payment and ledger references for a household subscription.'},
  {id:'settings',area:'settings',title:'Workspace settings',icon:'settings',subtitle:'Product capabilities, staff access and service readiness in the shared AONIK workspace.'},
];
const AW_CUSTOMERS=[
  {id:'CUS-AW-101',name:'Jamie Morgan',type:'Parent / guardian',product:'Arke Kids',family:'FAM-101',email:'jamie.morgan@example.com'},
  {id:'CUS-AW-102',name:'Priya Shah',type:'Parent / guardian',product:'Arke Kids',family:'FAM-102',email:'priya.shah@example.com'},
  {id:'CUS-AW-201',name:'Casey Rowan',type:'Creator',product:'Arke Studio',family:null,email:'casey.rowan@example.com'},
];
const AW_FAMILIES=[
  {id:'FAM-101',name:'Morgan family',guardian:'Jamie Morgan',customer:'CUS-AW-101',children:2,offer:'Family · September offer',price:19.99,credits:100,status:'Active'},
  {id:'FAM-102',name:'Shah family',guardian:'Priya Shah',customer:'CUS-AW-102',children:1,offer:'Peek · One-time trial',price:0,credits:0,status:'Trial'},
];
const AW_CHILDREN=[
  {id:'CH-101',alias:'M',band:'5–7',family:'FAM-101',world:'WRL-101',auto:true},
  {id:'CH-102',alias:'N',band:'8–10',family:'FAM-101',world:'WRL-102',auto:false},
  {id:'CH-103',alias:'P',band:'5–7',family:'FAM-102',world:null,auto:false},
];
const AW_WORLDS=[
  {id:'WRL-101',name:'The Lantern Garden',owner:'FAM-101',profile:'CH-101',revision:'r18',product:'Arke Kids',visibility:'Private',edition:'ED-101'},
  {id:'WRL-102',name:'Cloud Harbour',owner:'FAM-101',profile:'CH-102',revision:'r7',product:'Arke Kids',visibility:'Private',edition:'ED-102'},
  {id:'WRL-201',name:'The Clockwork Coast',owner:'CUS-AW-201',profile:'Creator',revision:'r24',product:'Arke Studio',visibility:'Private world',edition:'ED-201'},
];
const AW_EDITIONS=[
  {id:'ED-101',title:'A Light for the Garden',world:'WRL-101',owner:'FAM-101',format:'12-page illustrated story',status:'Parent review',digest:'sha256:demo-ed101-v1',product:'Arke Kids'},
  {id:'ED-102',title:'The Small Cloud Boat',world:'WRL-102',owner:'FAM-101',format:'Illustrated story + narration',status:'Safety hold',digest:'sha256:demo-ed102-v1',product:'Arke Kids'},
  {id:'ED-201',title:'The Keeper of Tides',world:'WRL-201',owner:'CUS-AW-201',format:'EPUB + narrated edition',status:'Editorial review',digest:'sha256:demo-sub201-v1',product:'Arke Studio'},
];
const AW_OFFERS=[
  {plan:'Spark',legacy:9.99,v3:9.99,legacyCredits:45,flex:35,profiles:1,slots:1,print:'None included'},
  {plan:'Family',legacy:19.99,v3:24.99,legacyCredits:100,flex:70,profiles:3,slots:3,print:'1 softcover / 3 paid months · max 4/year'},
  {plan:'Studio',legacy:null,v3:39.99,legacyCredits:220,flex:120,profiles:5,slots:5,print:'1 softcover / 2 paid months · max 6/year'},
];
const awMoney=value=>new Intl.NumberFormat('en-US',{style:'currency',currency:'USD'}).format(value);
Object.assign(window,{AW_SCENES});
