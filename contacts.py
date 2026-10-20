"""Local address book and launch history. Never stores remote passwords."""
import json
import os
import tempfile
from datetime import datetime
from pathlib import Path
from remote import normalize_peer

class Contacts:
    def __init__(self,root):
        self.path=Path(root)/'contacts.json'
        self.data={'contacts':[],'recent':[]}
        if self.path.is_file():
            # Do not silently overwrite a damaged file.
            parsed=json.loads(self.path.read_text(encoding='utf-8'))
            if not isinstance(parsed,dict) or not isinstance(parsed.get('contacts'),list) or not isinstance(parsed.get('recent'),list):
                raise ValueError('Хаягийн дэвтрийн бүтэц буруу байна.')
            for row in parsed['contacts']:
                if not isinstance(row,dict) or not isinstance(row.get('name'),str) or not row['name'].strip():
                    raise ValueError('Хадгалсан хаягийн нэр буруу байна.')
                normalize_peer(row.get('peer',''))
            for row in parsed['recent']:
                if not isinstance(row,dict) or row.get('mode') not in ('control','files') or not isinstance(row.get('time'),str):
                    raise ValueError('Холболтын түүхийн бүтэц буруу байна.')
                normalize_peer(row.get('peer',''))
                datetime.fromisoformat(row['time'])
            self.data=parsed
    def write(self):
        self.path.parent.mkdir(parents=True,exist_ok=True)
        fd,path=tempfile.mkstemp(prefix='contacts-',suffix='.tmp',dir=self.path.parent)
        try:
            with os.fdopen(fd,'w',encoding='utf-8') as stream:
                json.dump(self.data,stream,ensure_ascii=False,indent=2)
                stream.flush(); os.fsync(stream.fileno())
            os.replace(path,self.path)
        finally: Path(path).unlink(missing_ok=True)
    def save(self,name,peer):
        peer=normalize_peer(peer); name=name.strip()
        if not name: raise ValueError('Компьютерийн нэр оруулна уу.')
        if len(name)>100: raise ValueError('Нэр 100 тэмдэгтээс хэтрэхгүй байна.')
        previous=self.data['contacts'][:]
        self.data['contacts']=[r for r in previous if r['peer']!=peer]+[{'name':name,'peer':peer}]
        try: self.write()
        except OSError: self.data['contacts']=previous; raise
    def remove(self,peer):
        previous=self.data['contacts'][:]
        self.data['contacts']=[r for r in previous if r['peer']!=peer]
        try: self.write()
        except OSError: self.data['contacts']=previous; raise
    def remember(self,peer,mode='control'):
        peer=normalize_peer(peer)
        if mode not in ('control','files'): raise ValueError('Буруу холболтын төрөл')
        previous=self.data['recent'][:]
        self.data['recent']=[{'peer':peer,'mode':mode,'time':datetime.now().isoformat(timespec='seconds')}]+previous[:49]
        try: self.write()
        except OSError: self.data['recent']=previous; raise
