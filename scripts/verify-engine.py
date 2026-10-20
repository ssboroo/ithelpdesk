"""CI download compatibility check. Does not connect to a remote computer."""
import sys
import tempfile
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent.parent))
from remote import RemoteEngine, VERSION, file_digest, SHA256

with tempfile.TemporaryDirectory() as root:
    engine=RemoteEngine(root)
    path=engine.download()
    assert file_digest(path)==SHA256
    with path.open('rb') as stream: assert stream.read(2)==b'MZ'
    assert engine.command('123456789')==[str(path),'--connect','123456789']
    print(f'Official RustDesk {VERSION} download, PE header and SHA256 verified')
