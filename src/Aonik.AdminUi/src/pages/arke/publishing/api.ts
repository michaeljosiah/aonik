import apiClient, { api } from '@/lib/api';
import type { Catalogue, Channel, Decision, Operation, Release, ReviewDetail, ReviewPage } from './types';

const base='/publishing';
const enc=encodeURIComponent;
export const publishingApi={
  channels:(signal?:AbortSignal)=>api.get<Channel[]>(base+'/management/channels',{signal}),
  queue:(channel:string,status:string,query:string,page:number,signal?:AbortSignal)=>api.get<ReviewPage>(base+'/review/submissions',{params:{channelKey:channel,status:status||undefined,query:query||undefined,page,pageSize:25},signal}),
  review:(id:string,signal?:AbortSignal)=>api.get<ReviewDetail>(base+'/review/submissions/'+enc(id),{signal}),
  release:(id:string,signal?:AbortSignal)=>api.get<Release>(base+'/submissions/'+enc(id)+'/publication',{signal}),
  catalogue:(channel:string,page:number,signal?:AbortSignal)=>api.get<Catalogue>(base+'/management/channels/'+enc(channel)+'/catalogue',{params:{page,pageSize:25},signal}),
  decide:(id:string,body:object)=>api.post<Decision>(base+'/review/submissions/'+enc(id)+'/decisions',body),
  publish:(id:string,body:object)=>api.post<Operation>(base+'/submissions/'+enc(id)+'/publication',body),
  withdraw:(id:string,body:object)=>api.post<Operation>(base+'/editions/'+enc(id)+'/withdraw',body),
  retry:(id:string,body:object)=>api.post<Operation>(base+'/publications/'+enc(id)+'/retry',body),
  initialise:(channel:string,body:object)=>api.post<Operation>(base+'/management/channels/'+enc(channel)+'/initialise',body),
  mirror:(channel:string,body:object)=>api.post<Operation>(base+'/management/channels/'+enc(channel)+'/mirror/retry',body),
  hold:(id:string,body:object)=>api.post<Operation>(base+'/submissions/'+enc(id)+'/publication/hold',body),
  image:async(id:string,revision:string,asset:string,signal?:AbortSignal)=>{
    const response=await apiClient.get<Blob>(base+'/review/submissions/'+enc(id)+'/revisions/'+enc(revision)+'/images/'+enc(asset),{responseType:'blob',signal});
    if(!['image/png','image/jpeg'].includes(response.data.type))throw new Error('The private preview did not return a verified image.');
    return response.data;
  },
  download:async(id:string,revision:string)=>{
    const response=await apiClient.get<Blob>(base+'/review/submissions/'+enc(id)+'/revisions/'+enc(revision)+'/package',{responseType:'blob'});
    return response.data;
  },
};
export function publishingProblem(error:unknown):{code:string;message:string}{
  const e=error as {response?:{status?:number;data?:{code?:string;title?:string;detail?:string}};code?:string};
  const status=e?.response?.status;const data=e?.response?.data;
  if(status===401)return {code:'unauthenticated',message:'Sign in again to continue.'};
  if(status===403)return {code:data?.code??'forbidden',message:data?.code==='self-review-forbidden'?'A different editor must review this submission.':'Your current permissions do not allow this action.'};
  if(status===404)return {code:'unavailable',message:'This record or channel is not available in your assigned publisher scope.'};
  if(status&&((status>=400&&status<500)||(status===503&&data?.code)))return {code:data?.code??data?.title??'request-refused',message:data?.detail??'The service refused this request. Reload the current record before confirming again.'};
  return {code:'service-unavailable',message:'The service could not confirm the request. Refresh its status or retry the same confirmation with unchanged values.'};
}
