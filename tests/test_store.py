import sqlite3
import tempfile
import unittest
from datetime import date, timedelta
from pathlib import Path
from store import Store

class StoreTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory(); self.path=Path(self.tmp.name)/'test.db'; self.db=Store(self.path)
        self.asset=self.db.save('assets',dict(code='PC-001',name='Dell',serial='ABC',owner='Болд',department='Санхүү',specs='16GB',state='Ашиглаж байна'))
        self.tech=self.db.save('technicians',dict(name='Бат',contact=''))
    def tearDown(self): self.db.close(); self.tmp.cleanup()
    def ticket(self,**overrides):
        values=dict(title='Асахгүй',requester='Болд',asset_id=self.asset,technician_id=self.tech,priority='Өндөр',status='Шинэ',description=''); values.update(overrides)
        return values
    def test_ticket_lifecycle_and_history(self):
        ident=self.db.save('tickets',self.ticket())
        self.db.add_work(ident,self.tech,'Цэнэглэгч сольсон',35)
        self.db.save('tickets',self.ticket(status='Шийдвэрлэсэн'),ident)
        self.assertIsNotNone(self.db.get('tickets',ident)['closed'])
        self.assertEqual(len(self.db.history(asset=self.asset)),3)
        today=date.today().isoformat(); report=self.db.report(today,today)[0]
        self.assertEqual((report['assigned'],report['resolved'],report['minutes']),(1,1,35))
        self.db.save('tickets',self.ticket(status='Ажиллаж байна'),ident)
        self.assertIsNone(self.db.get('tickets',ident)['closed'])
        self.assertEqual(self.db.report(today,today)[0]['resolved'],0)
    def test_invalid_and_atomic(self):
        with self.assertRaises(ValueError): self.db.save('tickets',self.ticket(title=' '))
        with self.assertRaises(sqlite3.IntegrityError): self.db.save('tickets',self.ticket(asset_id=999))
        self.assertEqual(self.db.all('history'),[])
        self.assertEqual(self.db.all('tickets'),[])
        with self.assertRaises(ValueError): self.db.add_work(999,self.tech,'test',-1)
    def test_license_boundaries_and_perpetual(self):
        for days in (-1,0,30,31,None):
            self.db.save('licenses',dict(name=str(days),asset_id=self.asset,seats=1,expires=(date.today()+timedelta(days=days)).isoformat() if days is not None else '',vendor='',note=''))
        self.assertEqual([r['remaining'] for r in self.db.reminders()],[-1,0,30])
        with self.assertRaises(ValueError): self.db.save('licenses',dict(name='Bad',asset_id=None,seats=0,expires='',vendor='',note=''))
        with self.assertRaises(ValueError): self.db.save('licenses',dict(name='Bad',asset_id=None,seats=1,expires='2026-02-30',vendor='',note=''))
    def test_backup_and_reopen(self):
        self.db.save('tickets',self.ticket(title="Тест ' ; DROP TABLE assets; --"))
        target=Path(self.tmp.name)/'backup.db'; self.db.backup(target)
        copy=Store(target)
        self.assertEqual(len(copy.all('assets')),1); self.assertEqual(len(copy.all('tickets')),1); copy.close()
        with self.assertRaises(ValueError): self.db.backup(self.path)
    def test_unique_asset_and_date_range(self):
        with self.assertRaises(sqlite3.IntegrityError): self.db.save('assets',{k:v for k,v in self.db.get('assets',self.asset).items() if k!='id'})
        with self.assertRaises(ValueError): self.db.report('2026-10-20','2026-01-01')
    def test_reopen_persistence(self):
        self.db.close(); self.db=Store(self.path); self.assertEqual(self.db.get('assets',self.asset)['owner'],'Болд')

if __name__=='__main__': unittest.main()
