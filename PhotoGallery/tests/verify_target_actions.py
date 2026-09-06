#!/usr/bin/env python3
"""Real action-tool acceptance. Only imports/deletes this run's fixture copies.
Pass --keep-fixtures for the subsequent Aurum UI pass; report records cleanup IDs.
"""
import argparse, hashlib, json, shlex, subprocess, time, uuid
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--serial', default='emulator-26111')
parser.add_argument('--tool', default='action-tool')
parser.add_argument('--report', type=Path, default=Path('/tmp/photogallery-actions.json'))
parser.add_argument('--keep-fixtures', action='store_true')
args = parser.parse_args()
APP = 'org.tizen.photogallery'
records, imported = [], []
source_dir = '/opt/usr/home/owner/media/Images/GalleryAcceptance-' + uuid.uuid4().hex[:10]

def shell(command):
    run = subprocess.run(['sdb', '-s', args.serial, 'shell', command], capture_output=True, text=True, timeout=40)
    if run.returncode: raise RuntimeError(run.stdout + run.stderr)
    return run.stdout

def call(action, parameters=None, expected=True):
    name = action if '_' in action else 'Tv_Tizen.Action.Photo_' + action
    request = {'id': 1, 'params': {'name': name, 'appid': APP, 'arguments': parameters or {}}}
    raw = shell(args.tool + ' execute --json ' + shlex.quote(json.dumps(request)))
    response = json.JSONDecoder().raw_decode(raw.lstrip())[0]
    assert 'error' not in response, (name, response)
    result = response['result']
    data = result.get('structuredContent') or json.loads(result['content'][0]['text'])
    status = data.get('return', data)
    assert status['Success'] is expected, (name, expected, data)
    if not expected: assert status['Reason'].strip(), name
    records.append({'action': name, 'success': expected, 'typed': True})
    return data

def query(**kwargs): return call('Search', {'Id': '', 'Extra': '', 'Keyword': '', 'Category': '', 'Limit': 200, **kwargs})['result']
def check(yes, why):
    assert yes, why
    records.append({'postcondition': why})
def favorite(id, value): return call('App_Tizen.Action.PhotoGalleryCustom_SetFavorite', {'id': id, 'favorite': value})
def resolve(ids, expected=True): return call('App_Tizen.Action.PhotoGalleryCustom_GetPhotoByIds', {'ids': ids}, expected)
def add(path, title): return call('AddPhoto', {'Id': '', 'Extra': title, 'File': {'Path': path, 'StorageType': 'internal', 'MimeType': 'image/png'}})

try:
    shell('app_launcher -k ' + APP)
    shell('app_launcher -s ' + APP)
    time.sleep(1)
    call('GetCurrent', expected=False)
    call('StopSlideshow', expected=False)
    call('Search', {'Keyword': 'x' * 257}, False)
    call('Search', {'Limit': -1}, False)
    call('Search', {'Category': 'unsupported'}, False)
    call('AddPhoto', {'File': {'Path': '/etc/passwd'}}, False)
    call('AddPhoto', {'Id': 'caller-assigned-id', 'File': {'Path': '/missing.png'}}, False)
    call('DeletePhoto', {'Id': 'missing'}, False)
    call('Show', {'Id': 'missing'}, False)
    call('ToPresentation', {'Id': 'missing'}, False)
    resolve([], False)
    resolve(['id'] * 101, False)
    call('App_Tizen.Action.PhotoGalleryCustom_SetFavorite', {'id': 'missing', 'favorite': True}, False)
    before = query()
    if not before: call('StartSlideshow', expected=False)
    shell('mkdir -p ' + shlex.quote(source_dir))
    for source in reversed(list((Path(__file__).parent / 'fixtures').glob('*.png'))):
        destination = source_dir + '/' + source.name
        subprocess.run(['sdb', '-s', args.serial, 'push', str(source), destination], check=True, capture_output=True)
        previous = {p['Id'] for p in query()}
        add(destination, source.stem)
        after = query()
        created = [p for p in after if p['Id'] not in previous]
        check(len(created) == 1, 'AddPhoto is discoverable as one new MediaContent ID')
        p = created[0]; imported.append(p)
        check(p['File']['Id'] == p['Id'] and p['File']['Path'] != destination and p['File']['Size'] == source.stat().st_size, 'File entity identifies the actual imported copy')
        check(query(Id=p['Id'], Limit=1)[0]['Id'] == p['Id'], 'ID filtering precedes Search limit')
        check(hashlib.sha256(source.read_bytes()).hexdigest() in shell('sha256sum ' + shlex.quote(destination)), 'source bytes preserved')
    photo = imported[0]
    favorite(photo['Id'], True)
    check(json.loads(query(Id=photo['Id'])[0]['Extra'])['favorite'], 'favorite query postcondition')
    batch = resolve([photo['Id'], 'missing', photo['Id']])
    check([p['Id'] for p in batch['result']] == [photo['Id'], photo['Id']] and batch['unresolvedIds'] == ['missing'], 'ordered duplicate resolver and unresolved IDs')
    call('Show', photo)
    check(call('GetCurrent')['result']['Id'] == photo['Id'], 'Show/GetCurrent round trip')
    presentation = call('ToPresentation', {**photo, 'Extra': 'forged title', 'Note': 'private note'})['result']
    check('forged' not in presentation['Document'] and 'private' not in presentation['Document'] and '/opt/' not in presentation['Document'], 'presentation resolves current state and excludes paths/notes')
    check('Favorite' in presentation['Document'], 'presentation includes current favorite state')
    call('StartSlideshow')
    call('StartSlideshow', expected=False)
    time.sleep(3.5)
    check(call('GetCurrent')['result']['Id'] != photo['Id'], 'slideshow advances an actual displayed photo')
    call('StopSlideshow')
    call('StopSlideshow', expected=False)
    current = call('GetCurrent')['result']['Id']
    time.sleep(3.3)
    check(call('GetCurrent')['result']['Id'] == current, 'StopSlideshow retains current photo')
    deleted = imported.pop()
    call('DeletePhoto', deleted)
    check(query(Id=deleted['Id']) == [], 'DeletePhoto search postcondition')
    check(resolve([deleted['Id']])['unresolvedIds'] == [deleted['Id']], 'deleted ID is unresolved')
    call('DeletePhoto', deleted, False)
    check('missing' in shell('test -e ' + shlex.quote(deleted['File']['Path']) + ' || echo missing'), 'only imported copy removed')
    title = json.loads(deleted['Extra'])['title']
    check('present' in shell('test -e ' + shlex.quote(source_dir + '/' + title + '.png') + ' && echo present'), 'deletion preserves the source photo')
    # Restore the eighth fixture for UI density checks. This is a new photo with a new ID.
    previous = {p['Id'] for p in query()}
    add(source_dir + '/' + title + '.png', title)
    imported.extend(p for p in query() if p['Id'] not in previous)
    print(f'PASS: {len(records)} Action invocations and postconditions; {len(imported)} fixture photos retained for UI' if args.keep_fixtures else f'PASS: {len(records)} Action invocations and postconditions')
finally:
    if not args.keep_fixtures:
        for photo in imported: call('DeletePhoto', photo)
        shell('rm -rf ' + shlex.quote(source_dir))  # UUID directory created by this run only.
    args.report.write_text(json.dumps({'serial': args.serial, 'checks': records, 'fixtures': imported if args.keep_fixtures else [], 'sourceDirectory': source_dir}, indent=2))
