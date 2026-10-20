"""Local device ID and verified remote-engine setup panel."""
import os
import queue
import threading
import tkinter as tk
from tkinter import ttk, filedialog, messagebox
from pathlib import Path
from remote import RemoteEngine

ROOT=Path(os.environ.get('LOCALAPPDATA',Path.home()/'.local/share'))/'RainyRemote'

class EnginePanel(tk.Frame):
    def __init__(self,parent,engine):
        super().__init__(parent,bg='white',padx=22,pady=18)
        self.engine=engine; self.events=queue.Queue(); self.busy=False; self.timer=None
        self.status=tk.StringVar(value='Хөдөлгүүр бэлэн' if engine.find() else 'Эхний удаа хөдөлгүүр бэлдэнэ · 25.9 MB')
        self.my_id=tk.StringVar(value='ID хараахан аваагүй')
        tk.Label(self,text='ЭНЭ КОМПЬЮТЕРИЙН ID',bg='white',fg='#64748b',font=('Segoe UI',9,'bold')).pack(anchor='w')
        tk.Label(self,textvariable=self.my_id,bg='white',fg='#325de8',font=('Segoe UI',22,'bold')).pack(anchor='w',pady=(12,8))
        tk.Label(self,textvariable=self.status,bg='white',fg='#64748b',font=('Segoe UI',10),wraplength=440,justify='left').pack(anchor='w')
        actions=tk.Frame(self,bg='white'); actions.pack(fill='x',pady=(15,8))
        for title,action in [('Хөдөлгүүр бэлдэх',self.download),('RustDesk нээх',self.open_engine),('ID шалгах',self.read_id),('ID хуулах',self.copy_id)]:
            ttk.Button(actions,text=title,command=action).grid(row=0 if title in ('Хөдөлгүүр бэлдэх','RustDesk нээх') else 1,column=0 if title in ('Хөдөлгүүр бэлдэх','ID шалгах') else 1,sticky='ew',padx=(0,8),pady=4)
        ttk.Button(self,text='Суулгасан RustDesk файлаа сонгох…',command=self.choose).pack(anchor='w',pady=7)
        tk.Label(self,text='1. Хөдөлгүүр бэлдээд RustDesk нээнэ.\n2. ID-гаа техникчдээ өгч, ирсэн хүсэлтийг RustDesk дээр зөвшөөрнө.\n3. Ажил дуусахад RustDesk-ийн холболтын цонхыг хаана.',bg='white',fg='#17233a',font=('Segoe UI',10),justify='left',wraplength=440).pack(anchor='w',pady=10)
        tk.Label(self,text='Нууц үгээ энэ программд оруулахгүй. Зөвшөөрөл, дэлгэцийн болон хулганы эрхийг RustDesk цонхонд удирдана. Энэ цонхыг хаах нь RustDesk холболтыг салгахгүй.',bg='white',fg='#64748b',font=('Segoe UI',9),justify='left',wraplength=440).pack(anchor='w')
        self.timer=self.after(150,self.poll)
        self.bind('<Destroy>',self.on_destroy)

    def on_destroy(self,event):
        if event.widget is self and self.timer:
            self.after_cancel(self.timer); self.timer=None

    def poll(self):
        try:
            while True:
                kind,value=self.events.get_nowait()
                if kind=='progress': self.status.set(f'Татаж байна… {value}%')
                elif kind=='done': self.busy=False; self.status.set(str(value))
                elif kind=='id': self.busy=False; self.my_id.set(value); self.status.set('ID-г техникчдээ өгнө үү.')
                elif kind=='error':
                    self.busy=False; self.status.set('Үйлдэл амжилтгүй. Дахин оролдож болно.'); messagebox.showerror('Алсын тусламж',value,parent=self)
        except queue.Empty: pass
        self.timer=self.after(150,self.poll)

    def background(self,operation,kind='done'):
        if self.busy: return
        self.busy=True
        def run():
            try: self.events.put((kind,operation()))
            except Exception as e: self.events.put(('error',str(e)))
        threading.Thread(target=run,daemon=True).start()

    def download(self):
        def fetch():
            self.engine.download(lambda done,total:self.events.put(('progress',int(done*100/total))))
            return 'Хөдөлгүүр бэлэн. RustDesk нээгээд ID шалгана уу.'
        self.background(fetch)
    def open_engine(self):
        if self.busy: return
        try: self.engine.launch(); self.status.set('RustDesk нээгдлээ. Бэлэн болсны дараа ID шалгана уу.')
        except (OSError,ValueError) as e: messagebox.showerror('Алсын тусламж',str(e),parent=self)
    def read_id(self): self.background(self.engine.own_id,'id')
    def copy_id(self):
        value=self.my_id.get()
        if not value.isascii() or not value.isdigit(): return
        self.clipboard_clear(); self.clipboard_append(value); self.status.set('ID хуулагдлаа.')
    def choose(self):
        if self.busy: return
        path=filedialog.askopenfilename(parent=self,title='RustDesk сонгох',filetypes=[('RustDesk','rustdesk*.exe')])
        if path:
            try: self.engine.choose(path); self.status.set('Сонгосон RustDesk бэлэн.')
            except (OSError,ValueError) as e: messagebox.showerror('Алдаа',str(e),parent=self)
