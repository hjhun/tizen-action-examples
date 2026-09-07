#!/usr/bin/env python3
"""Read-only installed Browser P1 RPC checks. Requires a loaded, focused Browser page.

Saves actual wire inputs and typed outputs. Does not claim empty queries are failures
or verify native UI/renderer/crash gates; those are recorded separately.
"""
import argparse
import json
from pathlib import Path
import shlex
import subprocess

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--serial', required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
records = []

def call(label, name, inputs, success=True, allow_transport_error=False):
    request = {'id': len(records)+1, 'params': {'name': name, 'appid': 'org.tizen.browser', 'arguments': inputs}}
    local = args.output / (label+'-request.json')
    local.write_text(json.dumps(request, indent=2))
    remote = '/opt/usr/home/owner/share/tmp/p5-browser-contract-request.json'
    subprocess.run(['sdb','-s',args.serial,'push',str(local),remote],check=True,capture_output=True)
    run = subprocess.run(['sdb','-s',args.serial,'shell','action-tool execute --json -f '+shlex.quote(remote)],check=True,capture_output=True,text=True)
    (args.output/(label+'-response.json')).write_text(run.stdout)
    wire, end = json.JSONDecoder().raw_decode(run.stdout.lstrip())
    trailing = run.stdout.lstrip()[end:].strip()
    assert trailing in ("", "the action reported a failure"), trailing
    payload = wire.get('result', {})
    if 'error' in wire or (payload.get('isError') and not payload.get('structuredContent')):
        assert allow_transport_error, wire
        records.append({'label':label,'transportError':wire})
        return None
    body = payload.get('structuredContent') or json.loads(payload['content'][0]['text'])
    (args.output/(label+'-body.json')).write_text(json.dumps(body,indent=2))
    status = body.get('return',body)
    assert status['Success'] is success, body
    assert isinstance(status['Reason'],str) and len(status['Reason'])<=256, body
    def nonnull(value):
        assert value is not None, body
        if isinstance(value,dict):
            for v in value.values(): nonnull(v)
        if isinstance(value,list):
            for v in value: nonnull(v)
    nonnull(body)
    records.append({'label':label,'success':status['Success'],'reason':status['Reason']})
    return body

B='Tv_Tizen.Action.Browser_'
V='Common_Tizen.Action.View_'
C='App_Tizen.Action.BrowserCustom_GetPageByIds'
page=call('current',B+'GetCurrentPage',{})['result']
tabs=call('tabs',B+'GetTabs',{})['result']
assert [t['Ordinal'] for t in tabs]==list(range(1,len(tabs)+1))
assert sum(t['Focused'] for t in tabs)==1
resolution=call('resolver',C,{'ids':[page['Id'],'p1-missing',page['Id'],'p1-missing']})
assert resolution['result']==[page,page] and resolution['unresolvedIds']==['p1-missing','p1-missing']
call('resolver-invalid',C,{'ids':['']},False)
call('resolver-limit',C,{'ids':[page['Id']]*51},False)
views=call('views',V+'GetAnnotatedViews',{})['views']
focused=call('focus',V+'GetFocusedView',{})['view']
assert focused in views and focused['IsFocused']
assert focused['Annotation']['EntityType']=='Tizen.Entity.WebPageInfo'
assert json.loads(focused['Annotation']['EntityInfo'])['TizenEntityWebPageInfo']==page
assert focused['ScreenBounds']['Width']>0 and focused['ScreenBounds']['Height']>0
assert call('find',V+'FindById',{'id':focused['Id']})['view']==focused
call('find-missing',V+'FindById',{'id':'p1-missing'},False)
call('find-invalid',V+'FindById',{'id':''},False)
presentation=call('presentation',B+'ToPresentation',page)['result']
view_presentation=call('view-presentation',V+'ToPresentation',focused)['result']
assert presentation==view_presentation
stale=dict(page,Url='https://example.invalid/p1-stale')
assert call('presentation-stale',B+'ToPresentation',stale,False)['result']=={'Template':'','Document':''}
assert call('presentation-invalid',B+'ToPresentation',{},False)['result']=={'Template':'','Document':''}
bad_view=dict(focused,Annotation=dict(focused['Annotation'],EntityInfo='{}'))
assert call('view-presentation-invalid',V+'ToPresentation',bad_view,False)['result']=={'Template':'','Document':''}
# Exact app routing may reject non-advertised methods before reaching the provider.
for name in ['ControlMedia','ControlTab','Engage','Exit','GetMedia','GoToScreen','Navigate','OpenPage','Search','SearchPageList','SelectItem','SetMediaOption','SetScreenOption','ToCalendar','UpdatePageList']:
    call('unadvertised-'+name,B+name,{},False,True)
call('followup',B+'GetCurrentPage',{})
(args.output/'report.json').write_text(json.dumps({'calls':records,'count':len(records),'limitations':['GetTabs/GetAnnotatedViews natural target failure gate remains open','UI/renderer/crash verification is separate']},indent=2))
print(f'PASS: {len(records)} RPC checks; target empty-query failure/UI/crash gates are separate')
