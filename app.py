"""RAINY Remote — Mongolian attended remote desktop client for Windows."""
import os
import tkinter as tk
from tkinter import ttk, messagebox, simpledialog
from pathlib import Path
from contacts import Contacts
from remote import RemoteEngine, normalize_peer, VERSION
from engine_panel import EnginePanel

ROOT=Path(os.environ.get('LOCALAPPDATA',Path.home()/'.local/share'))/'RainyRemote'
BG='#eef2f7'; INK='#16263c'; MUTED='#65758b'; BLUE='#2265e8'

class App(tk.Tk):
    def __init__(self,data_root=None):
        super().__init__()
        self.title('RAINY Remote · Алсын удирдлага'); self.geometry('1120x790'); self.minsize(1040,740); self.configure(bg=BG)
        self.root_path=Path(data_root or ROOT)
        self.engine=RemoteEngine(self.root_path)
        try: self.contacts=Contacts(self.root_path)
        except (OSError,ValueError) as e:
            messagebox.showerror('Хаягийн дэвтэр уншиж чадсангүй',str(e)+'\nПрограммыг хааж contacts.json файлаа нөөцлөөд шалгана уу.',parent=self)
            self.destroy(); raise
        self.peer=tk.StringVar(); self.allowed=tk.BooleanVar(value=False)
        self.mode=tk.StringVar(value='Дэлгэц удирдах')
        style=ttk.Style(self);style.theme_use('clam')
        style.configure('.',font=('Segoe UI',10));style.configure('TButton',padding=(12,9))
        style.configure('TEntry',padding=10);style.configure('TCombobox',padding=8)
        style.configure('Treeview',rowheight=36,background='white',fieldbackground='white',foreground=INK,borderwidth=0)
        style.configure('Treeview.Heading',padding=10,background='#e7edf5',foreground=MUTED)
        style.configure('TNotebook',background=BG,borderwidth=0)
        style.configure('TNotebook.Tab',padding=(23,12),background='#e5ebf4')
        header=tk.Frame(self,bg='#142840',padx=28,pady=20);header.pack(fill='x')
        tk.Label(header,text='↗  RAINY REMOTE',bg='#142840',fg='white',font=('Segoe UI',22,'bold')).pack(side='left')
        tk.Label(header,text='АЛСЫН УДИРДЛАГА\nWindows · Монгол',bg='#142840',fg='#99b2d0',font=('Segoe UI',10),justify='right').pack(side='right')
        self.notebook=ttk.Notebook(self); self.notebook.pack(fill='both',expand=True,padx=24,pady=20)
        self.connect_tab=tk.Frame(self.notebook,bg=BG); self.book_tab=tk.Frame(self.notebook,bg=BG); self.settings_tab=tk.Frame(self.notebook,bg=BG)
        self.notebook.add(self.connect_tab,text='Алсын холболт');self.notebook.add(self.book_tab,text='Хаягийн дэвтэр');self.notebook.add(self.settings_tab,text='Тохиргоо, заавар')
        columns=tk.Frame(self.connect_tab,bg=BG); columns.pack(fill='x',pady=(18,12))
        columns.columnconfigure(0,weight=1,uniform='cards');columns.columnconfigure(1,weight=1,uniform='cards')
        self.panel=EnginePanel(columns,self.engine); self.panel.grid(row=0,column=0,sticky='nsew',padx=(0,12))
        card=tk.Frame(columns,bg='white',padx=24,pady=22);card.grid(row=0,column=1,sticky='nsew')
        self.label(card,'БУСАД КОМПЬЮТЕРТ ХОЛБОГДОХ',10,MUTED,True).pack(anchor='w')
        self.label(card,'Алсын төхөөрөмжийн ID',20,INK,True).pack(anchor='w',pady=(15,8))
        entry=ttk.Entry(card,textvariable=self.peer,font=('Segoe UI',21));entry.pack(fill='x',pady=7)
        entry.bind('<Return>',lambda e:self.connect())
        self.label(card,'Жишээ: 123 456 789',10,MUTED).pack(anchor='w')
        combo=ttk.Combobox(card,textvariable=self.mode,values=('Дэлгэц удирдах','Файл дамжуулах'),state='readonly');combo.pack(fill='x',pady=(22,12))
        tk.Checkbutton(card,text='Эзэмшигчтэй тохиролцож холбогдож байна',variable=self.allowed,bg='white',fg=INK,activebackground='white',font=('Segoe UI',10)).pack(anchor='w',pady=5)
        tk.Button(card,text='Холбогдох  →',command=self.connect,bg=BLUE,fg='white',relief='flat',font=('Segoe UI',13,'bold'),pady=13,cursor='hand2').pack(fill='x',pady=(13,8))
        ttk.Button(card,text='Энэ ID-г хаягийн дэвтэрт хадгалах',command=self.save_contact).pack(fill='x',pady=5)
        self.connection_status=tk.StringVar(value='Хоёр компьютерт RustDesk нээлттэй, интернэттэй байна.')
        self.label(card,'Холболт тусдаа RustDesk цонхонд нээгдэнэ. Дэлгэц / хулганы эрхийг хүлээн авах тал RustDesk дээр зөвшөөрнө.',10,MUTED,wraplength=430,justify='left').pack(anchor='w',pady=(16,5))
        tk.Label(self.connect_tab,textvariable=self.connection_status,bg=BG,fg=MUTED,font=('Segoe UI',10),wraplength=1000,justify='left').pack(anchor='w',pady=(0,12))
        self.label(self.connect_tab,'Сүүлд нээсэн холболтууд',13,INK,True).pack(anchor='w',pady=(0,10))
        self.recent=self.tree(self.connect_tab,[('peer','ТӨХӨӨРӨМЖИЙН ID',220),('mode','ТӨРӨЛ',210),('time','НЭЭСЭН ОГНОО',260)],4)
        self.recent.bind('<Double-1>',self.reuse_recent)
        self.label(self.connect_tab,'Түүх нь холболтын цонх нээснийг тэмдэглэнэ. Онлайн төлөв болон холболт амжилттай болсныг батлахгүй.',9,MUTED).pack(anchor='w',pady=10)
        self.build_book();self.build_settings();self.refresh_lists()
        self.protocol('WM_DELETE_WINDOW',self.shutdown)

    def label(self,parent,text,size=10,color=INK,bold=False,**kwargs):
        return tk.Label(parent,text=text,bg=parent.cget('bg'),fg=color,font=('Segoe UI',size,'bold' if bold else 'normal'),**kwargs)
    def tree(self,parent,columns,height=12):
        frame=tk.Frame(parent,bg='white');frame.pack(fill='both',expand=True)
        tree=ttk.Treeview(frame,columns=[c[0] for c in columns],show='headings',height=height,selectmode='browse')
        for key,title,width in columns: tree.heading(key,text=title);tree.column(key,width=width,anchor='w')
        scroll=ttk.Scrollbar(frame,orient='vertical',command=tree.yview);tree.configure(yscrollcommand=scroll.set)
        tree.pack(side='left',fill='both',expand=True);scroll.pack(side='right',fill='y');return tree
    def refresh_lists(self):
        self.recent.delete(*self.recent.get_children());self.book.delete(*self.book.get_children())
        for i,row in enumerate(self.contacts.data['recent']):
            self.recent.insert('','end',iid=str(i),values=(row['peer'],'Дэлгэц удирдах' if row.get('mode','control')=='control' else 'Файл дамжуулах',row['time'].replace('T',' ')))
        for row in sorted(self.contacts.data['contacts'],key=lambda r:r['name'].casefold()): self.book.insert('','end',iid=row['peer'],values=(row['name'],row['peer']))
    def build_book(self):
        self.label(self.book_tab,'Хадгалсан компьютерууд',20,INK,True).pack(anchor='w',pady=(25,8))
        self.label(self.book_tab,'Нэр, ID хадгална. Холболтын нууц үг хадгалахгүй.',10,MUTED).pack(anchor='w',pady=(0,20))
        self.book=self.tree(self.book_tab,[('name','КОМПЬЮТЕР / ЭЗЭМШИГЧ',380),('peer','RUSTDESK ID',300)])
        self.book.bind('<Double-1>',lambda e:self.select_contact())
        buttons=tk.Frame(self.book_tab,bg=BG);buttons.pack(fill='x',pady=16)
        ttk.Button(buttons,text='ID сонгох',command=self.select_contact).pack(side='left')
        ttk.Button(buttons,text='Нэмэх / нэр өөрчлөх',command=self.save_contact).pack(side='left',padx=10)
        ttk.Button(buttons,text='Хасах',command=self.delete_contact).pack(side='right')
    def build_settings(self):
        card=tk.Frame(self.settings_tab,bg='white',padx=28,pady=25);card.pack(fill='both',expand=True,pady=(18,0))
        self.label(card,'RAINY Remote · 2.0',23,INK,True).pack(anchor='w')
        self.label(card,'Алсын дэлгэц, хулгана, гар болон файл дамжуулах',13,MUTED).pack(anchor='w',pady=(6,24))
        text=(
          'Хэрэглэх дараалал\n\n'
          '1. Хоёр компьютерт RAINY Remote нээнэ.\n'
          '2. «Хөдөлгүүр бэлдэх» дарж, дараа нь «RustDesk нээх» сонгоно.\n'
          '3. Тусламж авах хүн «ID шалгах» дарж ID-гаа техникчдээ өгнө.\n'
          '4. Техникч ID оруулж «Холбогдох» дарна. Хүлээн авах тал RustDesk дээр зөвшөөрнө.\n'
          '5. Ажил дуусахад RustDesk холболтын цонхны × товчоор салгана.\n\n'
          f'Хөдөлгүүр: RustDesk {VERSION}, албан ёсны Windows x64 binary.\n'
          'Эхний таталтын дараа хөдөлгүүр тухайн компьютер дээр хадгалагдана.\n'
          'Өөрийн суулгасан RustDesk байвал .exe файлыг сонгож ашиглаж болно.\n'
          'Хоёр тал ижил public эсвэл ижил байгууллагын RustDesk сервер ашиглана.\n'
          'Өөрийн серверийг RustDesk → Settings → Network хэсгээс тохируулна.\n\n'
          'Холболтын төлөв, эрх, чат, дэлгэц сонголт, файл дамжуулалтыг RustDesk удирдана.\n'
          'RAINY Remote цонх хаах нь тусдаа нээгдсэн RustDesk холболтыг салгахгүй.\n'
          'Нууц үг, байнгын нэвтрэх эрхийг энэ программ тохируулахгүй.\n\n'
          'Хаягийн дэвтэр: '+str(self.contacts.path))
        self.label(card,text,11,INK,justify='left',wraplength=930).pack(anchor='w')
    def connect(self):
        try:
            peer=normalize_peer(self.peer.get())
            if not self.allowed.get(): raise ValueError('Эзэмшигчтэй тохиролцсон эсэхээ тэмдэглэнэ үү.')
            if self.panel.busy: raise ValueError('Хөдөлгүүрийн үйлдэл дуусахыг хүлээнэ үү.')
            mode='files' if self.mode.get()=='Файл дамжуулах' else 'control'
            self.engine.launch(peer,mode)
            self.peer.set(peer);self.allowed.set(False)
            self.connection_status.set(f'{peer} · RustDesk холболтын цонх нээгдлээ. Нөгөө талын зөвшөөрлийг RustDesk дээр хүлээнэ.')
            try: self.contacts.remember(peer,mode);self.refresh_lists()
            except OSError as e: messagebox.showwarning('Түүх хадгалагдсангүй',f'RustDesk нээгдсэн боловч түүх хадгалахад алдаа гарлаа: {e}',parent=self)
        except (OSError,ValueError) as e: messagebox.showerror('Холбогдож чадсангүй',str(e),parent=self)
    def save_contact(self):
        try:
            peer=normalize_peer(self.peer.get())
        except ValueError:
            peer=simpledialog.askstring('Хаягийн дэвтэр','RustDesk ID:',parent=self)
            if peer is None: return
        try:
            peer=normalize_peer(peer)
            previous=next((r['name'] for r in self.contacts.data['contacts'] if r['peer']==peer),'')
            name=simpledialog.askstring('Компьютер хадгалах','Компьютер / эзэмшигчийн нэр:',initialvalue=previous,parent=self)
            if name is None: return
            self.contacts.save(name,peer);self.refresh_lists()
        except (OSError,ValueError) as e: messagebox.showerror('Алдаа',str(e),parent=self)
    def select_contact(self):
        selected=self.book.selection()
        if not selected: return
        self.peer.set(selected[0]);self.allowed.set(False);self.notebook.select(self.connect_tab)
    def delete_contact(self):
        selected=self.book.selection()
        if selected and messagebox.askyesno('Хаяг хасах','Сонгосон хаягийг хасах уу?',parent=self):
            try: self.contacts.remove(selected[0]);self.refresh_lists()
            except OSError as e: messagebox.showerror('Алдаа',str(e),parent=self)
    def reuse_recent(self,event=None):
        selected=self.recent.selection()
        if selected:
            row=self.contacts.data['recent'][int(selected[0])];self.peer.set(row['peer']);self.mode.set('Файл дамжуулах' if row.get('mode')=='files' else 'Дэлгэц удирдах');self.allowed.set(False)
    def shutdown(self): self.destroy()

if __name__=='__main__': App().mainloop()
