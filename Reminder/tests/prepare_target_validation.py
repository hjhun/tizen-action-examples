#!/usr/bin/env python3
"""Prepare isolated action-tool scenarios; no target connection or invocation."""
import argparse
from datetime import datetime, timedelta, timezone
import json
from pathlib import Path
import uuid

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output', required=True, type=Path)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=False)
APP = 'org.tizen.actionexamples.reminder'
DISPLAY = 'org.tizen.displaypresentation'
run = 'rem-e2e-' + uuid.uuid4().hex[:10]
due = datetime.now(timezone.utc).replace(microsecond=0) + timedelta(days=30)
item = dict(Id=run, Extra='', Title='Action validation reminder', DueDate=due.isoformat(), Note='Initial note', State={'State':'To-do'})
query = dict(Id=run, Extra='', Keyword='', Category='Reminder', Limit=1)
prefix = 'result.structuredContent.'
standard = lambda method: 'Tv_Tizen.Action.Reminder_' + method
custom = lambda method: 'App_Tizen.Action.ReminderCustom_' + method
steps = []

def step(label, action, arguments, *, single=False, success=True, app=APP, save=None, equals=None, transport=True):
    single = single and not action.startswith('App_Tizen.Action.ReminderCustom_')
    status = 'result.content.0.text.Success' if single else prefix + 'return.Success'
    expected = {'success':transport}
    if transport: expected['equals'] = {status:success, **(equals or {})}
    result = dict(label=label, action=action, appid=app, arguments=arguments, expect=expected)
    if save: result['save'] = save
    return result

def write(name, entries):
    (args.output / (name+'.json')).write_text(json.dumps({'name':run+' '+name,'steps':entries},indent=2)+'\n')

def state_query(state):
    return step('Read persisted '+state,standard('Search'),query,equals={prefix+'result.0.Id':run,prefix+'result.0.State.State':state})

steps += [
    step('Add reminder',standard('Add'),item,single=True),
    step('Reject conflicting ID',standard('Add'),{**item,'Title':'Conflicting title'},single=True,success=False),
    state_query('To-do'),
    step('Reject non-domain Category',standard('Search'),{**query,'Category':'Completed'},success=False),
    step('Reject oversized keyword',standard('Search'),{**query,'Keyword':'x'*201},success=False),
    step('Resolve request order and duplicates',custom('GetReminderByIds'),{'ids':[run,run+'-missing',run]},
         equals={prefix+'result.0.Id':run,prefix+'result.1.Id':run,prefix+'unresolvedIds':[run+'-missing']}),
    step('Reject oversized resolver',custom('GetReminderByIds'),{'ids':[run]*101},success=False),
]
for state in ['In-progress','Blocked','Done']:
    steps += [step('Update '+state,standard('Update'),{**item,'State':{'State':state},'Note':'Updated note'},single=True),state_query(state)]
steps += [
    step('Reject invalid update',standard('Update'),{**item,'Title':''},single=True,success=False),
    step('Present current state despite stale caller',standard('ToPresentation'),item,save='presentation'),
    step('Render reminder presentation','Tv_Tizen.Action.Presentation_Show',
         {field:'{{presentation.'+prefix+'result.'+field+'}}' for field in ['Template','Document']},app=DISPLAY,single=True),
    step('Reject missing presentation',standard('ToPresentation'),{**item,'Id':run+'-missing'},success=False),
]
reservations = []
for kind,method in [('viewing','Viewing'),('recording','Recording')]:
    reservation = dict(Id=run+'-'+kind,Extra='',Channel={'Id':'fixture-channel','Name':'Test channel'},
                       Program={'Id':'fixture-program','Title':'Test program'},StartTime=due.isoformat(),
                       EndTime=(due+timedelta(hours=1)).isoformat(),Repeat='once',Kind=kind)
    reservations.append((reservation,method))
    steps += [
        step('Add '+kind,custom('Add'+method),reservation,single=True),
        step('Reject invalid '+kind+' range',custom('Add'+method),{**reservation,'Id':run+'-invalid','EndTime':due.isoformat()},single=True,success=False),
        step('Read reservation after '+kind,custom('GetReservations'),{},save='reservations'),
    ]
steps.append(step('Missing reservation provider route',custom('GetReservations'),{},app=APP+'.missing',transport=False))
for reservation,method in reservations:
    wrong = {**reservation,'Id':reservations[1 if method=='Viewing' else 0][0]['Id']}
    # Both fixtures still exist when the first wrong-kind cancellation is attempted.
    steps.append(step('Reject wrong-kind cancel '+method,custom('Cancel'+method),wrong,single=True,success=False))
for reservation,method in reservations:
    steps += [step('Cancel '+method,custom('Cancel'+method),reservation,single=True),
              step('Read reservations after cancel '+method,custom('GetReservations'),{},save='remaining')]
steps += [
    step('Delete reminder',standard('Delete'),item,single=True),
    step('Read deletion postcondition',standard('Search'),query,equals={prefix+'result':[]}),
    step('Reject malformed delete ID',standard('Delete'),{**item,'Id':''},single=True,success=False),
]
write('01_actions',steps)
write('02_cleanup',[
    step('Cleanup reminder',standard('Delete'),item,single=True),
    *[step('Cleanup '+method,custom('Cancel'+method),reservation,single=True) for reservation,method in reservations],
])
(args.output/'run.json').write_text(json.dumps({'run':run,'reminder':item,'reservations':[r for r,_ in reservations]},indent=2)+'\n')
print(args.output.resolve())
