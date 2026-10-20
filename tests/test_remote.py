import hashlib
import io
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch, Mock
import remote
from remote import RemoteEngine, normalize_peer
from contacts import Contacts

class EngineTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(); self.root=Path(self.temp.name);self.engine=RemoteEngine(self.root)
    def tearDown(self): self.temp.cleanup()
    def fake_executable(self):
        path=self.root/'RustDesk.exe';path.write_bytes(b'MZfake');self.engine.choose(path);return path.resolve()
    def test_peer_validation_blocks_arguments_and_urls(self):
        self.assertEqual(normalize_peer('123 456-789'),'123456789')
        for value in ('--connect','123456 --password abc','rustdesk://123456','1.2.3.4','１２３４５６','123\n456','12345','1'*17):
            with self.subTest(value=value),self.assertRaises(ValueError): normalize_peer(value)
    def test_launch_arguments_have_no_password_or_shell(self):
        path=self.fake_executable()
        self.assertEqual(self.engine.command('123456789'),[str(path),'--connect','123456789'])
        self.assertEqual(self.engine.command('123456789','files'),[str(path),'--file-transfer','123456789'])
        with patch('remote.IS_WINDOWS',True),patch('remote.subprocess.Popen') as popen:
            self.engine.launch('123 456 789');popen.assert_called_once_with([str(path),'--connect','123456789'],shell=False)
        with self.assertRaises(ValueError): self.engine.command('123456','terminal')
    def test_unprepared_and_invalid_selection(self):
        with self.assertRaises(FileNotFoundError):self.engine.command('123456')
        path=self.root/'bad.exe';path.touch()
        with self.assertRaises(ValueError):self.engine.choose(path)
    def test_verified_atomic_download(self):
        blob=b'MZupstream engine bytes';response=io.BytesIO(blob)
        with patch('remote.urlopen',return_value=response),patch('remote.DOWNLOAD_SIZE',len(blob)),patch('remote.SHA256',hashlib.sha256(blob).hexdigest()):
            path=self.engine.download();self.assertEqual(path.read_bytes(),blob);self.assertEqual(self.engine.find(),path)
        self.assertEqual(list(self.engine.root.glob('*.part')),[])
    def test_bad_download_preserves_existing_engine(self):
        self.engine.root.mkdir();self.engine.binary.write_bytes(b'previous')
        with patch('remote.urlopen',return_value=io.BytesIO(b'tampered')),patch('remote.DOWNLOAD_SIZE',8):
            with self.assertRaises(ValueError):self.engine.download()
        self.assertEqual(self.engine.binary.read_bytes(),b'previous');self.assertEqual(list(self.engine.root.glob('*.part')),[])
    def test_corrupted_cached_engine_not_executed(self):
        self.engine.root.mkdir();self.engine.binary.write_bytes(b'corrupted')
        self.assertIsNone(self.engine.find())
    def test_own_id_timeout_and_parsing(self):
        self.fake_executable()
        with patch('remote.subprocess.run',return_value=Mock(stdout='log line\n123456789\n')) as run:
            self.assertEqual(self.engine.own_id(),'123456789');self.assertEqual(run.call_args.kwargs['timeout'],12)
        with patch('remote.subprocess.run',return_value=Mock(stdout='No ID')):
            with self.assertRaises(ValueError):self.engine.own_id()

class ContactTests(unittest.TestCase):
    def setUp(self):self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name);self.book=Contacts(self.root)
    def tearDown(self):self.temp.cleanup()
    def test_save_update_remove_and_reopen(self):
        self.book.save('Батын компьютер','123 456 789');self.book.save('Санхүү','123456789')
        self.assertEqual(len(self.book.data['contacts']),1)
        other=Contacts(self.root);self.assertEqual(other.data['contacts'][0]['name'],'Санхүү')
        other.remove('123456789');self.assertEqual(Contacts(self.root).data['contacts'],[])
    def test_recent_is_bounded_and_no_credentials(self):
        for i in range(55):self.book.remember('123456789','files')
        self.assertEqual(len(self.book.data['recent']),50)
        self.assertEqual(set(self.book.data['recent'][0]),{'peer','mode','time'})
        with self.assertRaises(ValueError):self.book.save('','123456')
    def test_corruption_is_not_overwritten(self):
        path=self.root/'contacts.json';path.write_text('broken',encoding='utf-8')
        with self.assertRaises(ValueError):Contacts(self.root)
        self.assertEqual(path.read_text(),'broken')
    def test_disk_error_rolls_back_in_memory(self):
        with patch.object(self.book,'write',side_effect=OSError('full')):
            with self.assertRaises(OSError):self.book.save('PC','123456')
        self.assertEqual(self.book.data['contacts'],[])
