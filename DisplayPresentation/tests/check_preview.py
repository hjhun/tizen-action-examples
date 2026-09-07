#!/usr/bin/env python3
"""Exercise the executable preview in an isolated real Chromium session."""
import argparse
from pathlib import Path
import subprocess
import uuid

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--browser-path', required=True, help='Existing Chrome/Chromium executable')
parser.add_argument('--screenshots', type=Path, help='Optional directory for browser evidence')
args = parser.parse_args()
app = Path(__file__).resolve().parents[1]
session = 'presentation-check-' + uuid.uuid4().hex[:12]
command = ['npx', '--yes', '--package', 'agent-browser', 'agent-browser', '--session', session]

def run(*arguments, script=None):
    result = subprocess.run(command + list(arguments), input=script, text=True, capture_output=True, timeout=45)
    if result.returncode: raise RuntimeError(result.stdout + result.stderr)
    print(result.stdout.strip())

try:
    run('--executable-path', args.browser_path, 'open', (app / 'refs/one-ui-sample.html').as_uri())
    run('set', 'viewport', '1920', '1080')
    run('eval', '--stdin', script='''
const results=[];
for(const name of ['calendar0','calendar1','calendar7','calendar100','reminder']){
  preview.show(presentationFixtures[name]);const values=[];
  for(let i=0;i<preview.count;i++){
    const nodes=[...document.querySelectorAll('#surface .text')];
    if(nodes.length>4)throw Error('More than four visible text fields');
    const bounds=document.querySelector('#surface').getBoundingClientRect();
    if(nodes.some(n=>n.getBoundingClientRect().bottom>bounds.bottom))throw Error('Offscreen text');
    values.push(...nodes.map(n=>n.textContent));
    if(i<preview.count-1)document.querySelector('#next').click();
  }
  if(name==='calendar100'&&(values.length!==400||values.at(-1)!=='Note 99'))throw Error('Lost calendar content');
  results.push({name,pages:preview.count,fields:values.length});
}
preview.show(presentationFixtures.calendar7);JSON.stringify(results);
''')
    run('press', 'Enter')
    run('eval', "if(preview.page!==1||document.activeElement.id!=='next')throw Error('Enter/focus transition failed'); 'keyboard PASS'")
    if args.screenshots:
        args.screenshots.mkdir(parents=True, exist_ok=True)
        run('screenshot', str(args.screenshots / 'presentation-page-2-fhd.png'))
    for width, height in [(1280, 720), (3840, 2160), (7680, 4320), (1440, 1080)]:
        run('set', 'viewport', str(width), str(height))
        run('eval', '''(()=>{const b=document.querySelector('#canvas').getBoundingClientRect();
if(b.left<-.1||b.top<-.1||b.right>innerWidth+.1||b.bottom>innerHeight+.1||preview.page!==1)throw Error('Resize lost the page or bounds'); return 'resize PASS';})()''')
    run('set', 'viewport', '1920', '1080')
    run('press', 'Escape')
    run('eval', "if(preview.current!==null||document.querySelector('#surface').innerText.includes('Note 1'))throw Error('Back resurrected content'); 'dismiss PASS'")
    run('eval', '''preview.show({Template:'{',Document:'{}'});
if(preview.current!==null||!document.querySelector('#surface').classList.contains('error'))throw Error('Malformed input rendered');
preview.show({...presentationFixtures.calendar1,Template:presentationFixtures.calendar1.Template.replaceAll('"Text"','"Image"')});
if(preview.current!==null||!document.querySelector('#surface').classList.contains('error'))throw Error('Unsupported input rendered'); 'invalid/unsupported PASS';''')
    run('errors')
    print('Browser preview: PASS (not native NUI evidence)')
finally:
    run('close')
