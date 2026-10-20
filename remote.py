"""Attended RustDesk integration: verified upstream download and explicit CLI launch.

No passwords, unattended configuration, remote shell, or service installation.
"""
import hashlib
import json
import os
import re
import subprocess
import tempfile
import time
from pathlib import Path
from urllib.request import Request, urlopen

IS_WINDOWS = os.name == 'nt'
VERSION = '1.5.0'
DOWNLOAD_URL = 'https://github.com/rustdesk/rustdesk/releases/download/1.5.0/rustdesk-1.5.0-x86_64.exe'
SHA256 = '8555777215510d83d2d61c9dc984e4fcc838bd7e79f9d18a42585431f5e8bb47'
DOWNLOAD_SIZE = 25887600
RELEASE_URL = 'https://github.com/rustdesk/rustdesk/releases/tag/1.5.0'
SOURCE_URL = 'https://github.com/rustdesk/rustdesk/tree/1.5.0'

def normalize_peer(value):
    # Only numeric RustDesk IDs. No URI, IP, password, or CLI options accepted.
    value=re.sub(r'[ \t-]', '', str(value).strip())
    if not re.fullmatch(r'[0-9]{6,16}',value):
        raise ValueError('RustDesk ID нь 6–16 цифр байна. Жишээ: 123 456 789')
    return value

def file_digest(path):
    digest=hashlib.sha256()
    with open(path,'rb') as stream:
        for chunk in iter(lambda:stream.read(1024*1024),b''): digest.update(chunk)
    return digest.hexdigest()

class RemoteEngine:
    def __init__(self,root):
        self.root=Path(root)/'engine'
        self.binary=self.root/f'rustdesk-{VERSION}.exe'
        self.config=self.root/'settings.json'

    def find(self):
        if self.config.is_file():
            try:
                path=Path(json.loads(self.config.read_text(encoding='utf-8'))['executable'])
                if path.is_absolute() and path.is_file() and path.suffix.lower()=='.exe' and path.name.lower().startswith('rustdesk'):
                    return path
            except (OSError,ValueError,KeyError,TypeError): pass
        if self.binary.is_file() and file_digest(self.binary)==SHA256: return self.binary
        return None

    def choose(self,path):
        path=Path(path).resolve()
        if not path.is_file() or path.suffix.lower()!='.exe' or not path.name.lower().startswith('rustdesk'):
            raise ValueError('RustDesk-ийн rustdesk*.exe файлыг сонгоно уу.')
        self.root.mkdir(parents=True,exist_ok=True)
        temp=self.config.with_suffix('.tmp')
        temp.write_text(json.dumps({'executable':str(path)},ensure_ascii=False),encoding='utf-8')
        temp.replace(self.config)

    def download(self,progress=None):
        self.root.mkdir(parents=True,exist_ok=True)
        fd,temp=tempfile.mkstemp(prefix='download-',suffix='.part',dir=self.root)
        os.close(fd)
        digest=hashlib.sha256(); count=0; started=time.monotonic()
        try:
            request=Request(DOWNLOAD_URL,headers={'User-Agent':'RainyRemote/2.0'})
            with urlopen(request,timeout=30) as response, open(temp,'wb') as target:
                while True:
                    chunk=response.read(256*1024)
                    if not chunk: break
                    count+=len(chunk)
                    if count>DOWNLOAD_SIZE or time.monotonic()-started>300:
                        raise ValueError('Таталт хэмжээ / хугацааны хязгаараас хэтэрлээ.')
                    target.write(chunk); digest.update(chunk)
                    if progress: progress(count,DOWNLOAD_SIZE)
            if count!=DOWNLOAD_SIZE or digest.hexdigest()!=SHA256:
                raise ValueError('RustDesk файлын SHA256 тохирохгүй. Файлыг ажиллуулахгүй.')
            Path(temp).replace(self.binary)
            # Clear a previous custom selection only after a successful download.
            self.config.unlink(missing_ok=True)
            return self.binary
        finally:
            Path(temp).unlink(missing_ok=True)

    def command(self,peer=None,mode="control"):
        executable=self.find()
        if not executable: raise FileNotFoundError('Эхлээд RustDesk хөдөлгүүрээ бэлдэнэ үү.')
        command=[str(executable)]
        if mode not in ('control','files'): raise ValueError('Буруу холболтын төрөл')
        if peer is not None: command+=['--file-transfer' if mode=='files' else '--connect',normalize_peer(peer)]
        return command

    def launch(self,peer=None,mode="control"):
        if not IS_WINDOWS: raise OSError('Алсын тусламжийн клиент Windows x64 дээр ажиллана.')
        return subprocess.Popen(self.command(peer,mode),shell=False)

    def own_id(self):
        executable=self.find()
        if not executable: raise FileNotFoundError('Эхлээд RustDesk хөдөлгүүрээ бэлдэнэ үү.')
        result=subprocess.run([str(executable),'--get-id'],capture_output=True,text=True,timeout=12,
                              creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0),shell=False)
        lines=(result.stdout or '').strip().splitlines()
        for line in reversed(lines):
            try: return normalize_peer(line)
            except ValueError: pass
        raise ValueError('ID хараахан үүсээгүй. RustDesk-ээ нээгээд бэлэн болсны дараа дахин шалгана уу. ID-г RustDesk цонхноос мөн харж болно.')
