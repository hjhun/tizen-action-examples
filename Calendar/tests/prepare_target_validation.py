#!/usr/bin/env python3
"""Write action-tool scenarios locally; never install, connect, or invoke providers."""
import argparse
from datetime import datetime, timedelta, timezone
import json
from pathlib import Path
import uuid

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output', required=True, type=Path, help='New directory for this isolated test run')
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=False)
calendar = 'org.tizen.actionexamples.calendar'
display = 'org.tizen.displaypresentation'
run_id = 'interop-' + uuid.uuid4().hex[:12]
start = datetime.now(timezone.utc).replace(microsecond=0) + timedelta(days=30)
event = dict(Id=run_id+'-event', Extra='', Title=run_id, StartDate=start.isoformat(),
             EndDate=(start+timedelta(hours=1)).isoformat(), Note='Initial note', Location='Room')
reminder = dict(Id=run_id+'-reminder', Extra='', Title=run_id, DueDate=start.isoformat(),
                Note='Initial note', State={'State':'To-do'})
query = dict(Id=event['Id'], Extra='', Keyword='', Category='', Limit=100, StartDate='', EndDate='')
rq = dict(Id=reminder['Id'], Extra='', Keyword='', Category='', Limit=100)
prefix = 'result.structuredContent.'

def ref(name, path):
    return '{{'+name+'.'+prefix+path+'}}'

def presentation_ref(name):
    return {field:ref(name,'result.'+field) for field in ['Template','Document']}

def reminder_ref(name):
    return {field:ref(name,'result.0.'+field) for field in reminder}

def step(label, action, arguments, *, app=calendar, success=True, single=False, save=None, equals=None):
    assert isinstance(arguments, dict), 'action-tool requires an object before reference substitution'
    # Whole-Entity outputs are JSON in content[0].text; composite outputs use structuredContent.
    status_path = 'result.content.0.text.Success' if single else prefix+'return.Success'
    result = dict(label=label, action=action, appid=app, arguments=arguments,
                  expect={'success':True, 'equals':{status_path:success, **(equals or {})}})
    if save: result['save'] = save
    return result

def write(name, steps):
    (args.output/(name+'.json')).write_text(json.dumps({'name':run_id+' '+name,'steps':steps}, indent=2)+'\n')

def cal(method): return 'Tv_Tizen.Action.Calendar_'+method
def rem(method): return 'Tv_Tizen.Action.Reminder_'+method
def view(method): return 'Common_Tizen.Action.View_'+method
def custom(method): return 'App_Tizen.Action.CalendarCustom_'+method
show = 'Tv_Tizen.Action.Presentation_Show'
steps = [
    step('Create owned event',cal('AddEvent'),event,single=True),
    step('Reject duplicate event',cal('AddEvent'),event,single=True,success=False),
    step('Resolve order, duplicates and missing ID',custom('GetEventByIds'),
         {'ids':[event['Id'],run_id+'-missing',event['Id']]},equals={
             prefix+'result.0.Id':event['Id'],prefix+'result.1.Id':event['Id'],
             prefix+'unresolvedIds':[run_id+'-missing']}),
    step('Reject oversized resolver',custom('GetEventByIds'),{'ids':[event['Id']]*101},success=False),
    step('Update event',cal('UpdateEvent'),{**event,'Note':'Updated note'},single=True),
    step('Reject invalid event update',cal('UpdateEvent'),{**event,'EndDate':event['StartDate']},single=True,success=False),
    step('Verify persisted event update',cal('Search'),query,save='events',equals={prefix+'result.0.Note':'Updated note'}),
    step('Reject excessive search keyword',cal('Search'),{**query,'Keyword':'x'*513},success=False),
    step('Search only notes',custom('SearchInPeriod'),{**query,'Keyword':'Updated note',
         'SearchTitle':False,'SearchLocation':False,'SearchNote':True},equals={prefix+'result.0.Id':event['Id']}),
    step('Reject invalid search range',custom('SearchInPeriod'),{**query,'StartDate':'invalid',
         'SearchTitle':True,'SearchLocation':False,'SearchNote':False},success=False),
    step('Convert actual search result',cal('ToPresentation'),{'CalendarEvent':ref('events','result')},save='eventPresentation'),
    step('Render actual event presentation',show,presentation_ref('eventPresentation'),app=display,single=True),
    step('Reject 101 event presentation',cal('ToPresentation'),{'CalendarEvent':[event]*101},success=False),
    step('Convert empty event collection',cal('ToPresentation'),{'CalendarEvent':[]}),
    step('Create owned reminder',rem('Add'),reminder,single=True),
    step('Reject duplicate reminder',rem('Add'),reminder,single=True,success=False),
]
for state in ['To-do','In-progress','Blocked','Done']:
    steps += [
        step('Set reminder '+state,rem('Update'),{**reminder,'State':{'State':state}},single=True),
        step('Verify reminder '+state,rem('Search'),rq,save='reminders',equals={prefix+'result.0.State.State':state}),
        step('Convert reminder '+state,rem('ToPresentation'),reminder_ref('reminders'),save='reminderPresentation'),
        step('Render reminder '+state,show,presentation_ref('reminderPresentation'),app=display,single=True),
    ]
steps += [
    step('Reject invalid reminder update',rem('Update'),{**reminder,'Title':''},single=True,success=False),
    step('Reject excessive reminder query',rem('Search'),{**rq,'Keyword':'x'*513},success=False),
    step('Reject invalid reminder presentation',rem('ToPresentation'),{**reminder,'DueDate':'invalid'},success=False),
    step('Reject malformed presentation',show,{'Template':'{','Document':'{}'},app=display,single=True,success=False),
    step('Restore event presentation for UI inspection',show,presentation_ref('eventPresentation'),app=display,single=True),
]
write('01_actions',steps)

for name, app in [('02_calendar_views',calendar),('03_display_views',display)]:
    bad_view = dict(Id=run_id+'-missing-view',Extra='',Type='',Description='',IsFocused=False,IsEnabled=True,
                    Annotation={'EntityType':'invalid','EntityId':'invalid','EntityInfo':'{}'})
    write(name,[
        step('Read measured visible views',view('GetAnnotatedViews'),{},app=app,save='visible'),
        step('Resolve first visible view',view('FindById'),{'id':ref('visible','views.0.Id')},app=app,save='found'),
        step('Read actual annotated focus',view('GetFocusedView'),{},app=app),
        step('Reject missing view',view('FindById'),{'id':run_id+'-missing-view'},app=app,success=False),
        step('Reject invalid view snapshot',view('ToPresentation'),bad_view,app=app,success=False),
        step('Convert actual visible snapshot',view('ToPresentation'),
             {'Id':ref('found','view.Id'),'Annotation':ref('found','view.Annotation')},app=app,save='pagePresentation'),
        step('Render current page snapshot',show,presentation_ref('pagePresentation'),app=display,single=True),
    ])

# Keep deletion steps separate: action-tool stops at a failed assertion. These requests only
# address this run's IDs and should be used even when the main scenario failed partway through.
write('04_cleanup',[
    step('Delete owned reminder',rem('Delete'),reminder,single=True),
    step('Verify reminder deletion',rem('Search'),rq,equals={prefix+'result':[]}),
    step('Reject missing reminder deletion',rem('Delete'),reminder,single=True,success=False),
    step('Delete owned event',cal('DeleteEvent'),event,single=True),
    step('Verify event deletion',custom('GetEventByIds'),{'ids':[event['Id']]},
         equals={prefix+'result':[],prefix+'unresolvedIds':[event['Id']]}),
    step('Reject missing event deletion',cal('DeleteEvent'),event,single=True,success=False),
])
for name, action, entity in [('cleanup-reminder',rem('Delete'),reminder),('cleanup-event',cal('DeleteEvent'),event)]:
    (args.output/(name+'.json')).write_text(json.dumps({'id':1,'params':{
        'name':action,'appid':calendar,'arguments':entity}},indent=2)+'\n')
(args.output/'run.json').write_text(json.dumps({'run':run_id,'eventId':event['Id'],'reminderId':reminder['Id'],
    'status':'Prepared only; no provider calls performed.','fixtureSideEffects':'Creates app-owned event/reminder and reminder alarm until cleanup.'},indent=2)+'\n')
print(args.output.resolve())
