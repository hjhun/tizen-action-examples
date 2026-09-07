#!/usr/bin/env python3
"""Regression-check installed Presentation serialization without changing Calendar data.

Requires an installed Calendar and target action-tool. Writes exact requests/results
outside the repository when --output points there. Does not install or launch UI.
"""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import shlex
import subprocess
import uuid


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--serial', required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    calls = []

    def shell(command):
        return subprocess.run(['sdb', '-s', args.serial, 'shell', command],
                              check=True, text=True, capture_output=True, timeout=30).stdout.strip()

    def observe():
        return {
            'time': datetime.now(timezone.utc).isoformat(),
            'processes': shell("pgrep -f '^/opt/usr/globalapps/org.tizen.calendar/bin/Calendar.App.dll'"),
            'crashes': shell('ls -1 /opt/usr/share/crash/dump'),
        }

    def call(label, action, arguments):
        request = {'id': 1, 'params': {'name': action, 'appid': 'org.tizen.calendar', 'arguments': arguments}}
        # A target file avoids shell argument limits for the bounded oversized case.
        local = args.output / 'request.json'
        local.write_text(json.dumps(request) + '\n')
        remote = '/tmp/' + remote_name
        subprocess.run(['sdb', '-s', args.serial, 'push', str(local), remote],
                       check=True, capture_output=True, timeout=30)
        try:
            wire = json.loads(shell('action-tool execute --json -f ' + shlex.quote(remote)))
        finally:
            shell('rm -f ' + shlex.quote(remote))
        calls.append({'label': label, 'request': request, 'response': wire})
        (args.output / 'calls.json').write_text(json.dumps(calls, indent=2) + '\n')
        assert 'error' not in wire, wire
        body = wire['result']
        assert not body.get('isError'), wire
        return body.get('structuredContent') or json.loads(body['content'][0]['text'])

    def presentation(label, events, success):
        body = call(label, 'Tv_Tizen.Action.Calendar_ToPresentation', {'CalendarEvent': events})
        assert body['return']['Success'] is success, body
        assert isinstance(body['return']['Reason'], str), body
        payload = body['result']
        assert all(isinstance(payload.get(key), str) for key in ('Template', 'Document')), body
        if success:
            for key in ('Template', 'Document'):
                assert isinstance(json.loads(payload[key]), dict), body
        else:
            assert body['return']['Reason'], body
            assert payload == {'Template': '', 'Document': ''}, body
        return payload

    remote_name = 'calendar-presentation-' + uuid.uuid4().hex + '.json'
    before = observe()
    (args.output / 'before.json').write_text(json.dumps(before, indent=2) + '\n')
    query = dict(Id='', Extra='', Keyword='', Category='', Limit=100, StartDate='', EndDate='')
    search = call('Baseline current events', 'Tv_Tizen.Action.Calendar_Search', query)
    assert search['return']['Success'], search
    events = search['result']
    current = presentation('Current search result', events, True)
    (args.output / 'current-presentation.json').write_text(json.dumps(current, indent=2) + '\n')
    presentation('Empty collection', [], True)
    fixture = dict(Id='serialization-only', Extra='', Title='Serialization boundary',
                   StartDate='2026-09-08T09:00:00Z', EndDate='2026-09-08T10:00:00Z', Note='', Location='')
    # Conversion-only fixtures are never added to the repository or persisted.
    presentation('Invalid title', [{**fixture, 'Title': ''}], False)
    presentation('Invalid range', [{**fixture, 'EndDate': fixture['StartDate']}], False)
    presentation('101 events', [fixture] * 101, False)
    presentation('Valid events exceed Presentation transport bound',
                 [{**fixture, 'Id': 'serialization-' + str(i), 'Note': 'x' * 4096} for i in range(20)], False)
    for label, action, arguments in [
        ('Invalid Reminder Presentation', 'Tv_Tizen.Action.Reminder_ToPresentation',
         dict(Id='serialization-only', Extra='', Title='', DueDate='invalid', Note='', State={'State': 'To-do'})),
        ('Missing View annotation', 'Common_Tizen.Action.View_ToPresentation',
         dict(Id='serialization-only', Annotation={'EntityId': '', 'EntityType': '', 'EntityInfo': ''})),
        ('Stale View identity', 'Common_Tizen.Action.View_ToPresentation',
         dict(Id='serialization-only', Annotation={'EntityId': 'missing', 'EntityType': 'Tizen.Entity.CalendarEvent', 'EntityInfo': '{}'})),
    ]:
        body = call(label, action, arguments)
        assert body['return']['Success'] is False and body['return']['Reason'], body
        assert body['result'] == {'Template': '', 'Document': ''}, body
    after_search = call('Search unchanged after failures', 'Tv_Tizen.Action.Calendar_Search', query)
    assert after_search == search, 'Calendar changed during read-only regression run'
    ids = [event['Id'] for event in events]
    resolved = call('Resolver after failures', 'App_Tizen.Action.CalendarCustom_GetEventByIds', {'ids': ids})
    assert resolved['return']['Success'] and resolved['result'] == events and not resolved['unresolvedIds'], resolved
    after = observe()
    assert after['processes'] == before['processes'], 'Calendar PID changed during RPC checks'
    new_crashes = set(after['crashes'].splitlines()) - set(before['crashes'].splitlines())
    assert not any('calendar' in name.lower() for name in new_crashes), new_crashes
    report = dict(passed=True, before=before, after=after, call_count=len(calls),
                  new_crashes=sorted(new_crashes), persisted_fixture=False)
    (args.output / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
    print(f'Calendar Presentation RPC PASS ({len(calls)} calls): {args.output}')


if __name__ == '__main__':
    main()
