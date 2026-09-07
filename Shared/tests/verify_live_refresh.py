#!/usr/bin/env python3
"""Installed Calendar/Reminder live refresh; only this run's entities are mutated."""
import json, shlex, subprocess, time
from pathlib import Path
AURUM = Path(__file__).resolve().parents[2] / '.agents/skills/tizen-aurum-ui-automation/scripts/aurum-ui'

class Target:
    def __init__(self, serial='emulator-26111', port=55061, tool='action-tool'):
        self.serial, self.port, self.tool = serial, port, tool
        self.app = 'org.tizen.photogallery'
    def shell(self, command):
        r = subprocess.run(['sdb','-s',self.serial,'shell',command],capture_output=True,text=True,timeout=45)
        if r.returncode: raise RuntimeError(r.stdout+r.stderr)
        return r.stdout
    def call(self, name, arguments=None, app=None):
        request={'id':1,'params':{'name':name,'appid':app or self.app,'arguments':arguments or {}}}
        response=json.JSONDecoder().raw_decode(self.shell(self.tool+' execute --json '+shlex.quote(json.dumps(request))).lstrip())[0]
        if 'error' in response: return response
        result=response['result']
        return result.get('structuredContent') or json.loads(result['content'][0]['text'])
    def view(self, method, arguments=None): return self.call('Common_Tizen.Action.View_'+method,arguments)
    def ui(self, *arguments):
        r=subprocess.run([str(AURUM),*map(str,arguments),'--port',str(self.port)],capture_output=True,text=True,timeout=45)
        if r.returncode: raise RuntimeError(r.stderr)
        return json.loads(r.stdout)
    def control(self, name):
        end=time.monotonic()+3
        while True:
            response=self.view('GetAnnotatedViews');views=response.get('views',[])
            matches=[v for v in views if v['Id'].endswith('/'+name)]
            if not matches: matches=[v for v in views if v['Description']==name]
            if len(matches)==1:break
            if time.monotonic()>=end:raise AssertionError((name,response.get('return'),[(v['Description'],v['Id']) for v in views]))
            time.sleep(.08)
        b=matches[0]['ScreenBounds']
        self.ui('click',round(b['X']+b['Width']/2),round(b['Y']+b['Height']/2));time.sleep(.4)
    def capture(self,path):
        health=self.ui('health');self.ui('move',health['width']-20,40)
        return self.ui('screenshot',path)
    def launch(self,restart=False):
        if restart: self.shell('app_launcher -k '+self.app)
        self.shell('app_launcher -s '+self.app);time.sleep(1)

import argparse, uuid
from datetime import datetime, timedelta
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--serial',default='emulator-26111')
parser.add_argument('--port',type=int,default=55061)
parser.add_argument('--app',choices=['calendar','reminder','both'],default='both')
parser.add_argument('--related-only',action='store_true')
parser.add_argument('--output',type=Path,default=Path('/tmp/provider-live-refresh'))
a=parser.parse_args();a.output.mkdir(parents=True,exist_ok=True)
t=Target(a.serial,a.port);checks=[];owned=[];frame=0
now=datetime.fromisoformat(t.shell('date -Iseconds').strip());due=now+timedelta(minutes=30)
def check(value,label):
    assert value,label
    checks.append(label)
def ok(r):return r.get('return',r).get('Success') is True
def views():return t.view('GetAnnotatedViews').get('views',[])
def entities(id):return [v for v in views() if v['Annotation']['EntityId']==id]
def until(predicate,label):
    end=time.monotonic()+3
    while time.monotonic()<end:
        if predicate():check(True,label);return
        time.sleep(.08)
    raise AssertionError(label)
def capture(label):
    global frame
    frame+=1;t.capture(a.output/(str(frame).zfill(2)+'-'+label+'.png'))
def click(name):t.control(name);capture(name.replace(' ','-'))
def click_entity(id):
    b=entities(id)[0]['ScreenBounds']
    t.ui('click',round(b['X']+b['Width']/2),round(b['Y']+b['Height']/2));time.sleep(.3)
    capture('entity-'+id)
def draft(description):
    v=next(v for v in views() if v['Description']==description)
    envelope=next(iter(json.loads(v['Annotation']['EntityInfo']).values()))
    return json.loads(envelope['Extra'])['draft']['text'],v['Id']
def change(category,method,entity):
    r=t.call(action(category,method),entity);check(ok(r),category+' '+method);return r
def action(category,method):
    return ('App' if category.endswith('Custom') else 'Tv')+'_Tizen.Action.'+category+'_'+method
def search(category,id):return t.call('Tv_Tizen.Action.'+category+'_Search',{'Id':id,'Limit':100})['result']
def key(name):t.ui('key',name);time.sleep(.2);capture('key-'+name)
def related(app):
    if app=='calendar':
        click('CalendarOverlayClose');click('OpenReminders')
    else:click('ReminderNav-All')
    items=[]
    for index in range(2):
        e={'Id':'live-related-'+uuid.uuid4().hex[:10],'Title':'Live related '+str(index),
           'Extra':'','Note':'owned related refresh acceptance','DueDate':due.isoformat(),'State':{'State':'To-do'}}
        items.append(e);owned.append((t.app,'Reminder','Delete',e));change('Reminder','Add',e)
        until(lambda:bool(entities(e['Id'])),app+' related reminder added to visible list')
        check(search('Reminder',e['Id'])[0]['Title']==e['Title'],app+' related Add Search postcondition')
        capture(app+'-related-add-'+str(index))
    snapshot=views()
    check(len(snapshot)==len({v['Id'] for v in snapshot}),app+' distinct live control IDs for multiple rows')
    if app=='calendar':
        check(sum(v['Description']=='Done' for v in snapshot)>=len(items),'Calendar publishes a completion control for each reminder')
    key('down');focus=t.view('GetFocusedView')['view']['Id']
    items[0]['Title']='Related title updated';change('Reminder','Update',items[0])
    until(lambda:any(items[0]['Title'] in v['Annotation']['EntityInfo'] for v in entities(items[0]['Id'])),app+' related title updates')
    check(t.view('GetFocusedView')['view']['Id']==focus,app+' D-pad focus retained after update')
    capture(app+'-related-dpad-focus')
    if app=='calendar':
        click('CalendarOverlayAction-Add reminder');click('Title');t.ui('key','q');time.sleep(.2)
        before=draft('Title');capture('calendar-reminder-draft-before')
        items[0]['State']={'State':'Done'};change('Reminder','Update',items[0])
        check(draft('Title')==before and before[0]=='q','Calendar reminder editor retains draft and actor ID')
        check(t.view('GetFocusedView')['view']['Id']==before[1],'Calendar reminder editor retains input focus')
        check(search('Reminder',items[0]['Id'])[0]['State']['State']=='Done','Calendar reminder completion Search postcondition')
        capture('calendar-reminder-draft-after');click('CalendarOverlayClose')
    else:
        click('ReminderSearch');t.ui('key','z');time.sleep(.2);before=draft('Search title or note')
        items[0]['Title']='Search draft preserved';change('Reminder','Update',items[0])
        check(draft('Search title or note')==before and before[0]=='z','Reminder unapplied search draft retained')
        until(lambda:any(items[0]['Title'] in v['Annotation']['EntityInfo'] for v in entities(items[0]['Id'])),'Reminder uses applied search while typing')
        capture('reminder-live-search')
        click_entity(items[0]['Id']);click('ReminderDetailEdit')
        original=draft('Title');change('Reminder','Delete',items[0]);time.sleep(.2)
        check(draft('Title')==original,'Reminder retains editor after external deletion')
        capture('reminder-deleted-while-editing');click('ReminderEditorSave')
        check(draft('Title')==original and not search('Reminder',items[0]['Id']),'Reminder stale editor save rejected without recreating item')
        click('ReminderEditorCancel')
    for e in items:
        change('Reminder','Delete',e);until(lambda:not entities(e['Id']),app+' related deletion leaves views')
        check(not search('Reminder',e['Id']),app+' related Delete Search postcondition')
    if app=='calendar':return
    click('ReminderNav-Reservations')
    for kind in ['Viewing','Recording']:
        e={'Id':'live-reservation-'+uuid.uuid4().hex[:10],'Extra':'',
           'Channel':{'Id':'fixture-channel','Name':'Test channel'},'Program':{'Id':'fixture-program','Title':'Live '+kind},
           'StartTime':due.isoformat(),'EndTime':(due+timedelta(hours=1)).isoformat(),'Repeat':'once','Kind':kind.lower()}
        owned.append((t.app,'ReminderCustom','Cancel'+kind,e));change('ReminderCustom','Add'+kind,e)
        until(lambda:bool(entities(e['Id'])),kind+' reservation appears immediately');capture('reminder-live-'+kind)
        result=t.call(action('ReminderCustom','GetReservations'))['result']
        check(any(x['Id']==e['Id'] for x in result),kind+' GetReservations postcondition')
        check(not ok(t.call(action('ReminderCustom','Add'+kind),{**e,'EndTime':e['StartTime']})),kind+' invalid range rejected')
        check(bool(entities(e['Id'])),kind+' failure keeps live reservation')
        click_entity(e['Id']);key('right');focus=t.view('GetFocusedView')['view']['Id']
        check(focus.endswith('/ReminderDetailCancelReservation'),kind+' D-pad enters detail')
        change('ReminderCustom','Cancel'+kind,e);until(lambda:not entities(e['Id']),kind+' cancelled reservation leaves list and detail')
        check(not any(x['Id']==e['Id'] for x in t.call(action('ReminderCustom','GetReservations'))['result']),kind+' cancellation query postcondition')
        check(t.view('GetFocusedView')['view']['Id'].endswith('/ReminderSearchApply'),kind+' removed focus falls back to visible search')
        capture('reminder-cancelled-'+kind)
try:
    for app in (['calendar','reminder'] if a.app=='both' else [a.app]):
        t.app='org.tizen.actionexamples.'+app;t.launch(restart=True)
        if a.related_only:
            if app=='calendar':click('SearchControl')
            related(app)
            print(app,'related PASS',len(checks),'cumulative checks',flush=True)
            continue
        category='Calendar' if app=='calendar' else 'Reminder'
        add,update,delete=('AddEvent','UpdateEvent','DeleteEvent') if app=='calendar' else ('Add','Update','Delete')
        id='live-ui-'+uuid.uuid4().hex[:10]
        e={'Id':id,'Title':'Live UI '+app,'Extra':'','Note':'owned live refresh acceptance'}
        e.update({'StartDate':due.isoformat(),'EndDate':(due+timedelta(hours=1)).isoformat(),'Location':''} if app=='calendar' else {'DueDate':due.isoformat(),'State':{'State':'To-do'}})
        owned.append((t.app,category,delete,e));focus=t.view('GetFocusedView')['view']['Id']
        change(category,add,e);until(lambda:bool(entities(id)),app+' visible immediately after add')
        check(search(category,id)[0]['Title']==e['Title'],app+' Add Search postcondition')
        capture(app+'-added')
        check(t.view('GetFocusedView')['view']['Id']==focus,app+' stable initial focus across add')
        e['Title']='Updated live '+app;change(category,update,e)
        until(lambda:any(e['Title'] in v['Annotation']['EntityInfo'] for v in entities(id)),app+' UI annotation updates with title')
        check(search(category,id)[0]['Title']==e['Title'],app+' Update Search postcondition')
        check(not ok(t.call('Tv_Tizen.Action.'+category+'_'+update,{**e,'Title':''})),app+' invalid update rejected')
        check(search(category,id)[0]['Title']==e['Title'],app+' rejected update preserves data')
        presentation=t.view('ToPresentation',entities(id)[0])
        check(ok(presentation) and e['Title'] in presentation['result']['Document'],app+' View presentation contains current title')
        capture(app+'-updated')
        payload={k:presentation['result'][k] for k in ['Template','Document']}
        check(ok(t.call('Tv_Tizen.Action.Presentation_Show',payload,app='org.tizen.displaypresentation')),app+' display current View presentation')
        time.sleep(.3);capture(app+'-display-current');t.launch();capture(app+'-returned-from-display')
        for sample in range(20):
            current=views()
            check(bool(current) and sum(v['IsFocused'] for v in current)<=1 and
                  any(v['Annotation']['EntityId']==id and e['Title'] in v['Annotation']['EntityInfo'] for v in current),
                  app+' consistent annotation snapshot '+str(sample+1))
        click('AddEvent' if app=='calendar' else 'ReminderAdd')
        click('Title');t.ui('key','q');time.sleep(.15)
        text,field=draft('Title');check(text=='q',app+' editor text entered');capture(app+'-draft-before')
        due_field='Start HH:mm' if app=='calendar' else 'RFC 3339 due time; leave blank for no alert'
        click(due_field);t.ui('key','x');time.sleep(.2)
        invalid_due,field=draft(due_field);check('x' in invalid_due,app+' partial invalid date entered')
        capture(app+'-invalid-date-before')
        e['Title']='Changed during draft '+app;change(category,update,e);time.sleep(.3)
        check(draft('Title')[0]=='q',app+' live change preserves draft')
        check(draft(due_field)[0]==invalid_due,app+' live change preserves invalid date draft')
        check(t.view('GetFocusedView')['view']['Id']==field,app+' live change preserves input focus')
        capture(app+'-draft-after')
        # Action RPC itself may request a provider resume on this runtime.
        # Verify the real covered frame and resume draft, not an assumed RPC pause.
        t.shell('app_launcher -s org.tizen.photogallery');time.sleep(.7)
        capture(app+'-background')
        e['Title']='Changed while paused '+app;change(category,update,e)
        t.launch();time.sleep(.2)
        check(draft('Title')[0]=='q',app+' resume preserves draft')
        check(draft(due_field)[0]==invalid_due,app+' resume preserves invalid date draft')
        capture(app+'-resume-draft')
        # The first Back may be consumed by the IME; explicitly cancel the editor.
        click('CalendarOverlayClose' if app=='calendar' else 'ReminderEditorCancel')
        until(lambda:any(e['Title'] in v['Annotation']['EntityInfo'] for v in entities(id)),app+' latest saved state after editor close')
        if app=='calendar':
            click('SearchControl');click('CalendarOverlayAction-Search')
            until(lambda:bool(entities(id)),'Calendar applied search finds event')
            click('CalendarSearchKeyword');t.ui('key','z');time.sleep(.15)
            e['Title']='Live search result';change(category,update,e)
            until(lambda:any(e['Title'] in v['Annotation']['EntityInfo'] for v in entities(id)),'Calendar applied results refresh while typing')
            check(draft('Search events')[0]=='z','Calendar search draft retained')
            capture('calendar-live-search')
        change(category,delete,e);until(lambda:not entities(id),app+' deleted entity leaves annotations')
        check(not search(category,id),app+' Delete Search postcondition')
        capture(app+'-deleted')
        print(app,'PASS',len(checks),'cumulative checks',flush=True)
        related(app)
        print(app,'related PASS',len(checks),'cumulative checks',flush=True)
finally:
    for app,category,method,entity in owned:
        t.app=app;t.call(action(category,method),entity)
    (a.output/'results.json').write_text(json.dumps({'checks':checks,'serial':a.serial},indent=2))
print('PASS:',len(checks),'live UI/Action checks')
