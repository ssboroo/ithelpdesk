"""Persistent helpdesk domain. Standard-library only; all SQL values parameterized."""
import sqlite3
from datetime import date, datetime
from pathlib import Path

STATUSES = ('Шинэ', 'Ажиллаж байна', 'Хүлээгдэж байна', 'Шийдвэрлэсэн', 'Хаасан')
PRIORITIES = ('Бага', 'Хэвийн', 'Өндөр', 'Яаралтай')
SCHEMA = '''
CREATE TABLE IF NOT EXISTS assets (
 id INTEGER PRIMARY KEY, code TEXT NOT NULL UNIQUE, name TEXT NOT NULL,
 serial TEXT NOT NULL DEFAULT '', owner TEXT NOT NULL DEFAULT '', department TEXT NOT NULL DEFAULT '',
 specs TEXT NOT NULL DEFAULT '', state TEXT NOT NULL DEFAULT 'Ашиглаж байна');
CREATE TABLE IF NOT EXISTS technicians (
 id INTEGER PRIMARY KEY, name TEXT NOT NULL, contact TEXT NOT NULL DEFAULT '');
CREATE TABLE IF NOT EXISTS licenses (
 id INTEGER PRIMARY KEY, name TEXT NOT NULL, asset_id INTEGER REFERENCES assets(id),
 seats INTEGER NOT NULL DEFAULT 1 CHECK(seats>0), expires TEXT NOT NULL DEFAULT '',
 vendor TEXT NOT NULL DEFAULT '', note TEXT NOT NULL DEFAULT '');
CREATE TABLE IF NOT EXISTS tickets (
 id INTEGER PRIMARY KEY, title TEXT NOT NULL, requester TEXT NOT NULL DEFAULT '',
 asset_id INTEGER REFERENCES assets(id), technician_id INTEGER REFERENCES technicians(id),
 priority TEXT NOT NULL, status TEXT NOT NULL, description TEXT NOT NULL DEFAULT '',
 created TEXT NOT NULL, updated TEXT NOT NULL, closed TEXT);
CREATE TABLE IF NOT EXISTS history (
 id INTEGER PRIMARY KEY, ticket_id INTEGER NOT NULL REFERENCES tickets(id),
 created TEXT NOT NULL, technician_id INTEGER REFERENCES technicians(id),
 note TEXT NOT NULL, minutes INTEGER NOT NULL DEFAULT 0 CHECK(minutes>=0));
PRAGMA user_version=1;
'''

class Store:
    def __init__(self, path):
        self.path = Path(path)
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.conn = sqlite3.connect(self.path, timeout=10)
        self.conn.row_factory = sqlite3.Row
        self.conn.execute('PRAGMA foreign_keys=ON')
        self.conn.executescript(SCHEMA)

    def rows(self, sql, args=()):
        return [dict(r) for r in self.conn.execute(sql, args)]

    def all(self, table):
        if table not in ('assets','technicians','licenses','tickets','history'):
            raise ValueError('Буруу хүснэгт')
        return self.rows(f'SELECT * FROM {table} ORDER BY id DESC')

    def get(self, table, ident):
        return next((r for r in self.all(table) if r['id']==ident), None)

    def save(self, table, data, ident=None):
        fields = {
            'assets': {'code','name','serial','owner','department','specs','state'},
            'technicians': {'name','contact'},
            'licenses': {'name','asset_id','seats','expires','vendor','note'},
            'tickets': {'title','requester','asset_id','technician_id','priority','status','description'},
        }
        if table not in fields or set(data) != fields[table]:
            raise ValueError('Талбарууд тохирохгүй байна')
        data = {k:v.strip() if isinstance(v,str) else v for k,v in data.items()}
        required = 'title' if table=='tickets' else 'name'
        if not data[required] or (table=='assets' and not data['code']):
            raise ValueError('Заавал бөглөх талбарыг оруулна уу')
        if table=='licenses':
            data['seats']=int(data['seats'])
            if data['seats']<1: raise ValueError('Суудлын тоо 1-ээс багагүй байна')
            if data['expires']:
                parsed=date.fromisoformat(data['expires'])
                if parsed.isoformat()!=data['expires']: raise ValueError('Огноо YYYY-MM-DD байна')
        now=datetime.now().isoformat(timespec='seconds')
        with self.conn:
            if table=='tickets':
                if data['status'] not in STATUSES or data['priority'] not in PRIORITIES:
                    raise ValueError('Төлөв эсвэл зэрэглэл буруу')
                old=self.get(table,ident) if ident else None
                data.update(updated=now)
                data['closed']=(old['closed'] if old and old['closed'] else now) if data['status'] in STATUSES[-2:] else None
                if not ident: data['created']=now
            if ident:
                if not self.get(table,ident): raise ValueError('Бүртгэл олдсонгүй')
                self.conn.execute(f'UPDATE {table} SET '+','.join(f'{k}=?' for k in data)+' WHERE id=?', (*data.values(),ident))
            else:
                cur=self.conn.execute(f'INSERT INTO {table} ({",".join(data)}) VALUES ({",".join("?" for _ in data)})',tuple(data.values()))
                ident=cur.lastrowid
            if table=='tickets':
                note='Хүсэлт бүртгэв' if not old else f"Хүсэлт шинэчлэв · {old['status']} → {data['status']}"
                self.conn.execute('INSERT INTO history(ticket_id,created,technician_id,note) VALUES(?,?,?,?)',(ident,now,data['technician_id'],note))
        return ident

    def add_work(self, ticket, technician, note, minutes):
        minutes=int(minutes)
        if minutes<0 or not note.strip(): raise ValueError('Ажлын тайлбар, зөв минут оруулна уу')
        if not self.get('tickets',ticket): raise ValueError('Хүсэлт олдсонгүй')
        now=datetime.now().isoformat(timespec='seconds')
        with self.conn:
            self.conn.execute('INSERT INTO history(ticket_id,created,technician_id,note,minutes) VALUES(?,?,?,?,?)',(ticket,now,technician,note.strip(),minutes))
            self.conn.execute('UPDATE tickets SET updated=? WHERE id=?',(now,ticket))

    def history(self, ticket=None, asset=None):
        sql='''SELECT h.*, t.title, t.asset_id, COALESCE(n.name,'—') AS technician FROM history h
          JOIN tickets t ON t.id=h.ticket_id LEFT JOIN technicians n ON n.id=h.technician_id'''
        args=()
        if ticket is not None: sql+=' WHERE h.ticket_id=?'; args=(ticket,)
        elif asset is not None: sql+=' WHERE t.asset_id=?'; args=(asset,)
        return self.rows(sql+' ORDER BY h.id DESC',args)

    def reminders(self, days=30):
        today=date.today()
        output=[]
        for item in self.all('licenses'):
            if item['expires']:
                remaining=(date.fromisoformat(item['expires'])-today).days
                if remaining<=days: output.append(dict(item,remaining=remaining))
        return sorted(output,key=lambda x:x['remaining'])

    def report(self, start, end):
        start=date.fromisoformat(start).isoformat(); end=date.fromisoformat(end).isoformat()
        if start>end: raise ValueError('Эхлэх огноо дуусахаас хойш байна')
        return self.rows('''SELECT n.name,
          (SELECT COUNT(*) FROM tickets t WHERE t.technician_id=n.id AND substr(t.created,1,10) BETWEEN ? AND ?) AS assigned,
          (SELECT COUNT(*) FROM tickets t WHERE t.technician_id=n.id AND substr(t.closed,1,10) BETWEEN ? AND ?) AS resolved,
          (SELECT COALESCE(SUM(h.minutes),0) FROM history h WHERE h.technician_id=n.id AND substr(h.created,1,10) BETWEEN ? AND ?) AS minutes
          FROM technicians n ORDER BY n.name''',(start,end,start,end,start,end))

    def backup(self, destination):
        if Path(destination).resolve()==self.path.resolve(): raise ValueError('Өөр файл сонгоно уу')
        with sqlite3.connect(destination) as target: self.conn.backup(target)

    def close(self): self.conn.close()
