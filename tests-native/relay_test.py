"""Exercise two controlled WebSocket clients; no external desktop is controlled."""
import asyncio,json,os,sys
import websockets
URL=os.environ.get('RELAY_URL','wss://rainy-remote-relay-production.up.railway.app/ws')
async def receive(ws,t):
    async with asyncio.timeout(12):
        while True:
            msg=json.loads(await ws.recv())
            if msg.get('t')==t:return msg
async def send(ws,**values):await ws.send(json.dumps(values))
async def test():
    async with websockets.connect(URL,proxy=None) as host,websockets.connect(URL,proxy=None) as viewer,websockets.connect(URL,proxy=None) as stranger:
        h=await receive(host,'registered');v=await receive(viewer,'registered');await receive(stranger,'registered')
        key='A'*120
        await send(viewer,t='invite',target=h['id'],pin='000000',key=key)
        assert (await receive(viewer,'error'))['t']=='error'
        await send(viewer,t='invite',target=h['id'],pin=h['pin'],key=key)
        invite=await receive(host,'invite');await receive(viewer,'waiting')
        await send(host,t='reject',id=invite['id']);await receive(viewer,'cancelled');await receive(host,'cancelled')
        await send(viewer,t='invite',target=h['id'],pin=h['pin'],key=key)
        invite=await receive(host,'invite');await receive(viewer,'waiting')
        await send(host,t='accept',id=invite['id'],key=key,control=False)
        ha=await receive(host,'accepted');va=await receive(viewer,'accepted');assert ha['sid']==va['sid'];assert va['control'] is False
        packet=dict(t='data',sid=ha['sid'],n=1,iv='A'*16,tag='B'*24,body='payload')
        await stranger.send(json.dumps(packet));await receive(stranger,'error')
        await host.send(json.dumps(packet));assert (await receive(viewer,'data'))['body']=='payload'
        await host.send(json.dumps(packet));await receive(host,'error')
        await send(viewer,t='end');await receive(host,'ended');await receive(viewer,'ended')
        await host.send(json.dumps(packet));await receive(host,'error')
    print('Relay: PIN, consent rejection, session authorization, forwarding, replay, disconnect tests passed')
asyncio.run(test())
