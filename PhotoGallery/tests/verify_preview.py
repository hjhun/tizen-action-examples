#!/usr/bin/env python3
"""Browser-hosted fixture UI acceptance; captures are evidence, not product assets."""
import argparse, json, subprocess
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--output',type=Path,default=Path('/tmp/gallery-preview-acceptance'));p.add_argument('--fixtures',type=Path)
a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
base=['npx','--yes','agent-browser','--session','gallery-preview']
def cli(*args):
 r=subprocess.run(base+list(args),capture_output=True,text=True,timeout=40)
 if r.returncode:raise RuntimeError(r.stderr+r.stdout)
 return r.stdout
checks=[]
def js(code):return cli('eval',code)
def check(code,label):
 assert js('Boolean('+code+')').strip()=='true',label
 checks.append(label)
def shot(name):cli('screenshot',str(a.output/(name+'.png')))
def click(text):js('[...document.querySelectorAll("button")].find(b=>b.textContent==='+json.dumps(text)+').click()')
cli('open',(Path(__file__).resolve().parents[1]/'refs/one-ui-sample.html').as_uri());cli('set','viewport','1920','1080')
if a.fixtures:js('window.galleryPreview.library('+a.fixtures.read_text()+')')
check('document.querySelectorAll(".photo").length===8','eight photo grid');shot('pictures')
js('document.querySelector(".photo").focus()');cli('press','ArrowRight');check('document.activeElement===document.querySelectorAll(".photo")[1]','D-pad grid navigation');shot('dpad-focus')
js('document.querySelector(".photo").click()');check('document.querySelector(".viewer")','detail');shot('detail')
click('Info');check('document.querySelector(".dialog").textContent.includes("Album")','information');shot('info');cli('press','Escape')
click('Delete');check('document.activeElement.textContent==="Cancel"','cancel default focus');cli('press','ArrowRight');check('document.activeElement.textContent==="Delete"','modal focus bounded');shot('delete');cli('press','Escape');cli('press','Escape')
click('Albums');check('document.querySelectorAll(".photo").length>=1','albums');shot('albums');js('document.querySelector(".photo").click()');shot('album-pictures');cli('press','Escape')
click('Favorites');check('document.querySelectorAll(".photo").length>=1','favorites');shot('favorites')
click('Pictures');click('Search');js('document.querySelector(".search").value="q"');js('[...document.querySelectorAll("button")].filter(b=>b.textContent==="Search").at(-1).click()');check('document.body.textContent.includes("No results")','applied search empty');shot('search-empty');cli('press','Escape')
click('Import');shot('import');js('[...document.querySelector(".dialog").querySelectorAll("button")].find(b=>b.textContent==="Import").click()');check('document.body.textContent.includes("Choose an image")','invalid import feedback');shot('import-error');cli('press','Escape')
js('window.galleryPreview.state("loading")');check('document.body.textContent.includes("Loading")','loading');shot('loading')
js('window.galleryPreview.state("error")');check('document.body.textContent.includes("unavailable")','storage unavailable');shot('error')
js('window.galleryPreview.state("ready");window.galleryPreview.library([])');check('document.body.textContent.includes("No pictures yet")','empty library');shot('empty')
for width,height,expected in [(3840,2160,0),(4096,2160,128),(7680,4320,0)]:
 cli('set','viewport',str(width),str(height));check('Math.abs(document.querySelector("#app").getBoundingClientRect().x-'+str(expected)+')<1','centered viewport '+str(width))
cli('set','viewport','1920','1080')
(a.output/'results.json').write_text(json.dumps(checks,indent=2));print('PASS:',len(checks),'browser UI/scaling checks')
