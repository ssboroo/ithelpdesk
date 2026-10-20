// RAINY Remote rendezvous + opaque encrypted packet relay. No persistent storage.
import type { ServerWebSocket } from 'bun';
type State = {id:string,pin:string,ip:string,peer?:Socket,sid?:string,pending?:string,window:number,count:number,last:number};
type Socket=ServerWebSocket<State>;
const clients=new Map<string,Socket>();
const pending=new Map<string,{host:Socket,viewer:Socket,key:string,timer:ReturnType<typeof setTimeout>}>();
const attempts=new Map<string,{time:number,count:number}>();
const random=(digits:number)=>{const bytes=new Uint32Array(1);crypto.getRandomValues(bytes);return String(10**(digits-1)+bytes[0]%(9*10**(digits-1)))};
const send=(s:Socket,m:unknown)=>{try{s.send(JSON.stringify(m));}catch{}};
function cancel(id:string,reason='Холболтын хүсэлт цуцлагдлаа'){
 const p=pending.get(id);if(!p)return;clearTimeout(p.timer);pending.delete(id);
 p.host.data.pending=undefined;p.viewer.data.pending=undefined;
 send(p.host,{t:'cancelled',message:reason});send(p.viewer,{t:'cancelled',message:reason});
}
function end(s:Socket,reason='Холболт саллаа'){
 if(s.data.pending)cancel(s.data.pending,reason);
 const peer=s.data.peer;s.data.peer=undefined;s.data.sid=undefined;s.data.last=0;
 if(peer){peer.data.peer=undefined;peer.data.sid=undefined;peer.data.last=0;send(peer,{t:'ended',message:reason});}
 send(s,{t:'ended',message:reason});
}
const keyOK=(v:unknown)=>typeof v==='string'&&v.length>=50&&v.length<=1024&&/^[A-Za-z0-9+/=]+$/.test(v);
const server=Bun.serve<State>({
 port:Number(Bun.env.PORT||8080),
 fetch(req,server){
  const url=new URL(req.url);
  if(url.pathname==='/health')return Response.json({ok:true,protocol:3});
  if(url.pathname!=='/ws')return new Response('RAINY Remote relay v3');
  if(req.headers.get('origin'))return new Response('Native clients only',{status:403});
  const ip=server.requestIP(req)?.address||'unknown';
  if(clients.size>=200||[...clients.values()].filter(s=>s.data.ip===ip).length>=10)return new Response('Busy',{status:429});
  const upgraded=server.upgrade(req,{data:{id:'',pin:'',ip,window:Date.now(),count:0,last:0}});
  return upgraded?undefined:new Response('WebSocket upgrade required',{status:400});
 },
 websocket:{
  maxPayloadLength:1500000,idleTimeout:120,sendPings:true,backpressureLimit:3000000,closeOnBackpressureLimit:true,
  open(ws){let id=random(9);while(clients.has(id))id=random(9);ws.data.id=id;ws.data.pin=random(6);clients.set(id,ws);send(ws,{t:'registered',id,pin:ws.data.pin});},
  message(ws,raw){
   try{
    if(typeof raw!=='string'||raw.length>1500000)throw Error('Буруу өгөгдөл');
    const m=JSON.parse(raw);const d=ws.data;const now=Date.now();
    if(now-d.window>1000){d.window=now;d.count=0;}if(++d.count>140){end(ws,'Өгөгдлийн хязгаар хэтэрлээ');ws.close(1008);return;}
    if(m.t==='invite'){
     if(d.peer||d.pending)throw Error('Өөр холболт нээлттэй байна');
     for(const [key,value]of attempts)if(now-value.time>60000)attempts.delete(key);
     if(attempts.size>2000)throw Error('Дахин оролдоно уу');
     const target=typeof m.target==='string'?m.target:'';
     for(const key of ['ip:'+d.ip,'id:'+target]){const bucket=attempts.get(key)||{time:now,count:0};if(++bucket.count>8)throw Error('Олон хүсэлт илгээлээ. Нэг минут хүлээнэ үү.');attempts.set(key,bucket);}
     const host=clients.get(target);
     if(!host||host===ws||host.data.pin!==m.pin)throw Error('ID / нэг удаагийн код буруу эсвэл төхөөрөмж офлайн');
     if(host.data.peer||host.data.pending)throw Error('Төхөөрөмж завгүй байна');
     if(!keyOK(m.key))throw Error('Түлхүүр буруу');
     const id=crypto.randomUUID();d.pending=id;host.data.pending=id;
     const timer=setTimeout(()=>cancel(id,'Зөвшөөрөл хүлээх хугацаа дууслаа'),45000);
     pending.set(id,{host,viewer:ws,key:m.key,timer});
     send(host,{t:'invite',id,from:d.id});send(ws,{t:'waiting',message:'Нөгөө талын зөвшөөрлийг хүлээж байна…'});
    }else if(m.t==='accept'){
     const p=pending.get(d.pending||'');if(!p||p.host!==ws||m.id!==d.pending)throw Error('Хүсэлт хүчингүй');
     if(!keyOK(m.key))throw Error('Түлхүүр буруу');
     clearTimeout(p.timer);pending.delete(d.pending!);d.pending=undefined;p.viewer.data.pending=undefined;
     const sid=crypto.randomUUID();ws.data.peer=p.viewer;p.viewer.data.peer=ws;ws.data.sid=p.viewer.data.sid=sid;ws.data.last=p.viewer.data.last=0;
     send(ws,{t:'accepted',sid,role:'host',peer:p.viewer.data.id,key:p.key,control:m.control===true});
     send(p.viewer,{t:'accepted',sid,role:'viewer',peer:d.id,key:m.key,control:m.control===true});
    }else if(m.t==='reject'){
     const p=pending.get(d.pending||'');if(p&&p.host===ws&&m.id===d.pending)cancel(d.pending!,'Хүлээн авах тал татгалзлаа');
    }else if(m.t==='end'){end(ws);}
    else if(m.t==='data'){
     const peer=d.peer;
     if(!peer||!d.sid||m.sid!==d.sid)throw Error('Зөвшөөрөгдсөн холболт байхгүй');
     if(!Number.isSafeInteger(m.n)||m.n<=d.last||typeof m.iv!=='string'||m.iv.length!==16||typeof m.tag!=='string'||m.tag.length!==24||typeof m.body!=='string'||m.body.length>1400000)throw Error('Багцын бүтэц буруу');
     if(peer.getBufferedAmount()>2000000){end(ws,'Холболт удааширлаа. Дахин холбоно уу.');return;}
     d.last=m.n;peer.send(raw);
    }else throw Error('Танигдаагүй үйлдэл');
   }catch(e){send(ws,{t:'error',message:e instanceof Error?e.message:'Буруу хүсэлт'});}
  },
  close(ws){end(ws,'Нөгөө төхөөрөмж саллаа');clients.delete(ws.data.id);},
 }
});
console.log('RAINY Remote relay listening on',server.port);
