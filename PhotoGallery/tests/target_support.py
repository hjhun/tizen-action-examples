"""Scoped SDB/action-tool/Aurum client for the Gallery acceptance scripts."""
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
    def photo(self, method, arguments=None): return self.call('Tv_Tizen.Action.Photo_'+method,arguments)
    def view(self, method, arguments=None): return self.call('Common_Tizen.Action.View_'+method,arguments)
    def ui(self, *arguments):
        r=subprocess.run([str(AURUM),*map(str,arguments),'--port',str(self.port)],capture_output=True,text=True,timeout=45)
        if r.returncode: raise RuntimeError(r.stderr)
        return json.loads(r.stdout)
    def control(self, name):
        views=self.view('GetAnnotatedViews')['views']
        matches=[v for v in views if v['Id'].endswith('/'+name)]
        if not matches: matches=[v for v in views if v['Description']==name]
        assert len(matches)==1,(name,[(v['Description'],v['Id']) for v in matches])
        b=matches[0]['ScreenBounds']
        self.ui('click',round(b['X']+b['Width']/2),round(b['Y']+b['Height']/2));time.sleep(.4)
    def capture(self,path):
        health=self.ui('health');self.ui('move',health['width']-20,40)
        return self.ui('screenshot',path)
    def launch(self,restart=False):
        if restart: self.shell('app_launcher -k '+self.app)
        self.shell('app_launcher -s '+self.app);time.sleep(1)
