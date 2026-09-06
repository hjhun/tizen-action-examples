#!/usr/bin/env python3
"""Aurum UI + every current View contract; requires installed test fixture images."""
import argparse, copy, json, math, time
from pathlib import Path
from target_support import Target
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--serial',default='emulator-26111');p.add_argument('--port',type=int,default=55061)
p.add_argument('--tool',default='action-tool');p.add_argument('--width',type=int,default=1920);p.add_argument('--height',type=int,default=1080)
p.add_argument('--output',type=Path,default=Path('/tmp/gallery-ui-acceptance'));p.add_argument('--compact',action='store_true')
a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
t=Target(a.serial,a.port,a.tool);checks=[]
def check(yes,why):
    assert yes,why
    checks.append(why)
def ok(result):return result.get('return',result).get('Success') is True
def shot(name):time.sleep(.5);t.capture(a.output/(name+'.png'))
def views():
    r=t.view('GetAnnotatedViews');check(ok(r),'GetAnnotatedViews success');return r['views']
def focused():
    r=t.view('GetFocusedView');check(ok(r),'GetFocusedView success');return r['view']
def show_presentation(presentation,name):
    check(ok(t.call('Tv_Tizen.Action.Presentation_Show',presentation,'org.tizen.displaypresentation')),name+' Presentation_Show success')
    shot(name)
    check(not ok(t.view('GetAnnotatedViews')),'paused GetAnnotatedViews bounded failure')
    check(not ok(t.view('GetFocusedView')),'paused GetFocusedView bounded failure')
    # Installed renderer has an opaque full-window Dismiss at this reference position.
    s=min(a.width/1920,a.height/1080);x=(a.width-1920*s)/2
    t.ui('click',round(x+1680*s),round(915*s));time.sleep(.4);t.launch()

health=t.ui('health');check((health['width'],health['height'])==(a.width,a.height),'native resolution matches requested profile')
if not a.compact: t.launch(restart=True)
if a.compact:
    active=t.view('GetAnnotatedViews').get('views',[])
    if any(v['Id'].endswith('/ModalCancel') for v in active): t.control('ModalCancel')
    if ok(t.photo('GetCurrent')): t.control('ViewerBack')
shot('pictures')
current=views();page=current[0]['ScreenBounds'];scale=min(a.width/1920,a.height/1080)
check(abs(page['Width']-1920*scale)<4*scale and abs(page['Height']-1080*scale)<4*scale,'measured canvas applies one uniform scale')
check(abs(page['X']-(a.width-1920*scale)/2)<4*scale,'measured centered pillarbox')
photo=next(v for v in current if v['Annotation']['EntityType']=='Tizen.Entity.Photo')
check(abs(photo['ScreenBounds']['Width']-433*scale)<12*scale,'native photo bounds scale once including focus')
for _ in range(20):
    current=t.view('GetAnnotatedViews')['views'];check(bool(current),'atomic current publication')
    check(ok(t.view('FindById',{'id':current[0]['Id']})),'FindById current success')
check(not ok(t.view('FindById',{'id':'missing'})),'FindById missing failure')
check(not ok(t.view('FindById',{'id':'x'*1025})),'FindById bounded ID failure')
for v in views():
    b=v['ScreenBounds'];check(all(math.isfinite(x) for x in b.values()) and b['Width']>0 and b['Height']>0,'finite positive native bounds')
    check(ok(t.view('ToPresentation',v)),'current page/control/photo View_ToPresentation')
forged=copy.deepcopy(photo);forged['Annotation']['EntityInfo']='forged snapshot'
check(ok(t.view('ToPresentation',forged)),'caller snapshot is ignored in favor of current state')
forged['Annotation']['EntityId']='forged-id'
check(not ok(t.view('ToPresentation',forged)),'mismatched identity rejected')
# Explicitly focus the first photo, then navigate to its neighbor using D-pad.
b=photo['ScreenBounds'];t.ui('click',round(b['X']+b['Width']/2),round(b['Y']+b['Height']/2));time.sleep(.4)
check(ok(t.photo('GetCurrent')),'pointer opens actual photo')
shot('detail');t.control('ViewerDelete');shot('delete')
check(focused()['Description']=='Cancel','Delete dialog initial Cancel focus')
check(all('gallery:delete' in v['Id'] for v in views()),'modal publication excludes underlay')
t.ui('key','right');time.sleep(.2);check(focused()['Description']=='Delete','D-pad reaches modal confirm')
t.ui('key','left');time.sleep(.2);check(focused()['Description']=='Cancel','modal D-pad returns to Cancel')
t.ui('key','back');time.sleep(.4);check(focused()['Description']=='Delete','Back restores launching Delete focus')
check(not ok(t.view('FindById',{'id':photo['Id']})),'removed picture surface ID is stale')
if not a.compact:
    t.control('ViewerInfo');shot('info');t.ui('key','back');time.sleep(.3)
    current=t.photo('GetCurrent')['result']
    pres=t.photo('ToPresentation',{**current,'Extra':'forged'})
    check(ok(pres),'current Photo_ToPresentation')
    show_presentation(pres['result'],'action-presentation')
    v=next(v for v in views() if v['Annotation']['EntityType']=='Tizen.Entity.Photo')
    pres=t.view('ToPresentation',v);check(ok(pres),'photo View_ToPresentation')
    show_presentation(pres['result'],'view-presentation')
    t.control('ViewerBack');time.sleep(.3)
    check(focused()['Annotation']['EntityType']=='Tizen.Entity.Photo','Back restores source photo focus')
    t.ui('key','right');time.sleep(.3);shot('dpad-focus');check(focused()['Annotation']['EntityType']=='Tizen.Entity.Photo','D-pad navigates pictures')
    t.control('TabAlbums');shot('albums')
    v=next(v for v in views() if v['Annotation']['EntityType']=='Tizen.Entity.Photo')
    b=v['ScreenBounds'];t.ui('click',round(b['X']+b['Width']/2),round(b['Y']+b['Height']/2));time.sleep(.3);shot('album-pictures')
    t.ui('key','back');time.sleep(.3);t.control('TabFavorites');shot('favorites')
    check(any(v['Annotation']['EntityType']=='Tizen.Entity.Photo' for v in views()),'persisted Favorites filter has photo')
    t.control('TabPictures');t.control('Search');t.control('SearchInput');t.ui('key','q');time.sleep(.3)
    draft=focused();check('q' in draft['Annotation']['EntityInfo'],'keyboard search draft is annotated')
    shot('search-keyboard');t.control('SearchApply');shot('search-empty')
    check(not any(v['Annotation']['EntityType']=='Tizen.Entity.Photo' for v in views()),'typed query changes rendered results')
    check(focused()['Description']=='Search','Search Apply hides keyboard and restores button focus')
    t.control('SearchClose');t.control('Import');shot('import')
    t.control('ModalConfirm');shot('import-error')
    check(any('path' in v['Annotation']['EntityInfo'].lower() for v in views()),'invalid import displays validation feedback')
    t.ui('key','back');time.sleep(.3);check(focused()['Description']=='Import','import cancel restores focus')
    # Page state is also a current-state A2UI producer.
    pres=t.view('ToPresentation',views()[0]);check(ok(pres),'page View_ToPresentation')
    show_presentation(pres['result'],'page-presentation')
shot('final-pictures')
(a.output/'results.json').write_text(json.dumps({'serial':a.serial,'resolution':[a.width,a.height],'checks':checks},indent=2))
print('PASS:',len(checks),'UI/View checks at',a.width,a.height)
