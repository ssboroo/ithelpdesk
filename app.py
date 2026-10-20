"""Монгол IT Helpdesk — offline Windows desktop application."""
import csv
import os
import sqlite3
import sys
import tkinter as tk
from tkinter import ttk, messagebox, filedialog
from pathlib import Path
from datetime import date
from store import Store, STATUSES, PRIORITIES

ROOT=Path(os.environ.get('LOCALAPPDATA',Path.home()/'.local/share'))/'RainyHelpdesk'
BG='#f3f5f9'; INK='#17233a'; MUTED='#64748b'; BLUE='#325de8'

class App(tk.Tk):
    def __init__(self, data_path=None):
        super().__init__()
        self.title('IT Helpdesk · Монгол')
        self.geometry('1280x800'); self.minsize(1000,650); self.configure(bg=BG)
        self.store=Store(data_path or ROOT/'helpdesk.db')
        self.view='Хяналтын самбар'; self.query=tk.StringVar(); self.filter=tk.StringVar(value='Бүгд')
        self.style=ttk.Style(self); self.style.theme_use('clam')
        self.style.configure('.', font=('Segoe UI',10))
        self.style.configure('Treeview',background='white',fieldbackground='white',foreground=INK,rowheight=38,borderwidth=0)
        self.style.configure('Treeview.Heading',background='#edf1f7',foreground=MUTED,font=('Segoe UI',10,'bold'),padding=12)
        self.style.map('Treeview',background=[('selected','#dce5ff')],foreground=[('selected',INK)])
        self.style.configure('TCombobox',padding=7)
        self.style.configure('TEntry',padding=8)
        sidebar=tk.Frame(self,bg='#15233b',width=225); sidebar.pack(side='left',fill='y'); sidebar.pack_propagate(False)
        tk.Label(sidebar,text='▣  IT HELPDESK',fg='white',bg='#15233b',font=('Segoe UI',17,'bold')).pack(anchor='w',padx=22,pady=(32,5))
        tk.Label(sidebar,text='ТЕХНИКИЙН УДИРДЛАГА',fg='#91a4bf',bg='#15233b',font=('Segoe UI',8)).pack(anchor='w',padx=24,pady=(0,36))
        self.nav={}
        for symbol, name in [('◈','Хяналтын самбар'),('▤','Асуудлын хүсэлт'),('▣','Төхөөрөмжүүд'),('◇','Программ, лиценз'),('♙','Техникчид'),('▥','Ажлын тайлан'),('⚙','Тохиргоо')]:
            b=tk.Button(sidebar,text=f'{symbol}   {name}',anchor='w',command=lambda n=name:self.navigate(n),bg='#15233b',fg='#b9c7dd',activebackground='#2b4060',activeforeground='white',relief='flat',font=('Segoe UI',11),padx=22,pady=13,cursor='hand2')
            b.pack(fill='x',padx=10,pady=2); self.nav[name]=b
        tk.Label(sidebar,text='LOCAL WORKSPACE\n●  Офлайн ажиллана\nv1.0 · Монгол',justify='left',bg='#15233b',fg='#91a4bf',font=('Segoe UI',10)).pack(side='bottom',anchor='w',padx=24,pady=26)
        self.main=tk.Frame(self,bg=BG); self.main.pack(side='left',fill='both',expand=True,padx=30,pady=24)
        self.protocol('WM_DELETE_WINDOW',self.shutdown)
        self.navigate(self.view)
        self.after(1600,self.startup_reminder)
        self.after(60000,self.daily_check)
        self.last_reminder=date.today()

    def shutdown(self): self.store.close(); self.destroy()
    def label(self,parent,text,size=10,color=INK,bold=False,**kw):
        return tk.Label(parent,text=text,bg=parent.cget('bg'),fg=color,font=('Segoe UI',size,'bold' if bold else 'normal'),**kw)
    def button(self,parent,text,command,primary=False):
        return tk.Button(parent,text=text,command=command,bg=BLUE if primary else 'white',fg='white' if primary else INK,activebackground='#dbe5ff',relief='flat',padx=17,pady=10,font=('Segoe UI',10,'bold'),cursor='hand2')
    def navigate(self,name):
        self.view=name; self.query.set(''); self.filter.set('Бүгд'); self.refresh()
    def refresh(self):
        for child in self.main.winfo_children(): child.destroy()
        for n,b in self.nav.items(): b.configure(bg='#2b4060' if n==self.view else '#15233b',fg='white' if n==self.view else '#b9c7dd')
        top=tk.Frame(self.main,bg=BG); top.pack(fill='x')
        self.label(top,'БАЙГУУЛЛАГЫН АЖЛЫН ОРЧИН',9,MUTED).pack(side='left')
        self.label(top,date.today().strftime('%Y.%m.%d')+'   •   Өгөгдөл хадгалагдсан',9,MUTED).pack(side='right')
        self.label(self.main,self.view,25,bold=True).pack(anchor='w',pady=(24,6))
        descriptions={'Хяналтын самбар':'Техникийн үйлчилгээний өнөөгийн төлөв, анхаарах ажлууд.', 'Асуудлын хүсэлт':'Хүсэлт бүртгэх, хуваарилах, засварын явцыг хянах.', 'Төхөөрөмжүүд':'Байгууллагын компьютер, тоног төхөөрөмжийн нэгдсэн бүртгэл.', 'Программ, лиценз':'Программ хангамж, суудлын тоо, лицензийн хугацаа.', 'Техникчид':'Техникийн үйлчилгээ хариуцсан ажилтнууд.', 'Ажлын тайлан':'Сонгосон хугацааны шийдвэрлэлт болон зарцуулсан цаг.', 'Тохиргоо':'Өгөгдлийн сан, нөөцлөлт болон ашиглалтын мэдээлэл.'}
        self.label(self.main,descriptions[self.view],10,MUTED).pack(anchor='w',pady=(0,24))
        if self.view=='Хяналтын самбар': self.dashboard()
        elif self.view=='Тохиргоо': self.settings()
        elif self.view=='Ажлын тайлан': self.reports()
        else: self.records()

    def table(self,parent,columns,rows,height=12):
        box=tk.Frame(parent,bg='white'); box.pack(fill='both',expand=True)
        tree=ttk.Treeview(box,columns=[c[0] for c in columns],show='headings',height=height,selectmode='browse')
        for key,title,width in columns:
            tree.heading(key,text=title); tree.column(key,width=width,minwidth=65,anchor='w')
        for ident,values in rows: tree.insert('', 'end',iid=str(ident),values=values)
        y=ttk.Scrollbar(box,orient='vertical',command=tree.yview); x=ttk.Scrollbar(box,orient='horizontal',command=tree.xview)
        tree.configure(yscrollcommand=y.set,xscrollcommand=x.set)
        box.rowconfigure(0,weight=1); box.columnconfigure(0,weight=1)
        tree.grid(row=0,column=0,sticky='nsew'); y.grid(row=0,column=1,sticky='ns'); x.grid(row=1,column=0,sticky='ew')
        return tree

    def dashboard(self):
        tickets=self.store.all('tickets'); assets=self.store.all('assets'); reminders=self.store.reminders()
        stats=[('НЭЭЛТТЭЙ ХҮСЭЛТ',sum(t['status'] not in STATUSES[-2:] for t in tickets),'Үйлчилгээ хүлээж буй'),('НИЙТ ТӨХӨӨРӨМЖ',len(assets),'Бүртгэлтэй хөрөнгө'),('ШИЙДВЭРЛЭСЭН',sum(t['status'] in STATUSES[-2:] for t in tickets),'Хаагдсан ба шийдэгдсэн'),('ЛИЦЕНЗИЙН САНУУЛГА',len(reminders),'30 хоногийн дотор / дууссан')]
        cards=tk.Frame(self.main,bg=BG); cards.pack(fill='x',pady=(0,25))
        for i,(title,value,hint) in enumerate(stats):
            cards.columnconfigure(i,weight=1)
            card=tk.Frame(cards,bg='white',padx=18,pady=20,highlightbackground='#e1e7ef',highlightthickness=1); card.grid(row=0,column=i,sticky='nsew',padx=(0,12 if i<3 else 0))
            self.label(card,title,9,MUTED).pack(anchor='w'); self.label(card,str(value),32,BLUE,True).pack(anchor='w',pady=7); self.label(card,hint,9,MUTED).pack(anchor='w')
        bar=tk.Frame(self.main,bg=BG); bar.pack(fill='x',pady=(0,12))
        self.label(bar,'Сүүлийн хүсэлтүүд',14,bold=True).pack(side='left'); self.button(bar,'+ Хүсэлт бүртгэх',lambda:self.editor('tickets'),True).pack(side='right')
        columns=[('id','ДУГААР',90),('title','АСУУДАЛ',320),('priority','ЗЭРЭГЛЭЛ',120),('status','ТӨЛӨВ',160)]
        tree=self.table(self.main,columns,[(r['id'],(f"HD-{r['id']:04}",r['title'],r['priority'],r['status'])) for r in tickets[:7]],6)
        tree.bind('<Double-1>',lambda e:self.open_selection(tree,'tickets'))
        self.label(self.main,'Лицензийн хугацаа',14,bold=True).pack(anchor='w',pady=(20,10))
        area=tk.Frame(self.main,bg='#fff4df',padx=18,pady=14); area.pack(fill='x')
        if reminders:
            for r in reminders[:3]:
                text=f"{r['name']}   ·   {r['expires']}   ·   "+(f"{abs(r['remaining'])} хоног хэтэрсэн" if r['remaining']<0 else f"{r['remaining']} хоног үлдсэн")
                self.label(area,text,10,'#895c16').pack(anchor='w',pady=3)
            self.button(area,'Бүх лиценз харах →',lambda:self.navigate('Программ, лиценз')).pack(anchor='e')
        else: self.label(area,'Ойрын 30 хоногт дуусах лиценз алга.',10,'#895c16').pack(anchor='w')
        if not tickets: self.label(self.main,'Эхлэхдээ техникч, төхөөрөмжөө нэмээд эхний хүсэлтээ бүртгээрэй.',10,MUTED).pack(anchor='w',pady=16)

    def records(self):
        table={'Асуудлын хүсэлт':'tickets','Төхөөрөмжүүд':'assets','Программ, лиценз':'licenses','Техникчид':'technicians'}[self.view]
        bar=tk.Frame(self.main,bg=BG); bar.pack(fill='x',pady=(0,15))
        entry=ttk.Entry(bar,textvariable=self.query,width=27); entry.pack(side='left'); entry.bind('<KeyRelease>',lambda e:self.fill_records())
        self.button(bar,'Хайх',self.fill_records).pack(side='left',padx=8)
        if table=='tickets':
            combo=ttk.Combobox(bar,textvariable=self.filter,values=('Бүгд',)+STATUSES,state='readonly',width=18); combo.pack(side='left'); combo.bind('<<ComboboxSelected>>',lambda e:self.fill_records())
        self.button(bar,'+ Нэмэх',lambda:self.editor(table),True).pack(side='right')
        self.button(bar,'CSV татах',self.export_current).pack(side='right',padx=8)
        defs={
          'tickets':[('id','ДУГААР',90),('title','АСУУДАЛ',250),('requester','ХҮСЭЛТ ГАРГАГЧ',160),('asset','ТӨХӨӨРӨМЖ',120),('tech','ТЕХНИКЧ',130),('priority','ЗЭРЭГЛЭЛ',110),('status','ТӨЛӨВ',160)],
          'assets':[('code','КОД',110),('name','ТӨХӨӨРӨМЖ',210),('serial','СЕРИЙН ДУГААР',150),('owner','ЭЗЭМШИГЧ',150),('department','ХЭЛТЭС',140),('state','ТӨЛӨВ',150)],
          'licenses':[('name','ПРОГРАММ',210),('asset','ТӨХӨӨРӨМЖ',160),('seats','СУУДАЛ',80),('expires','ДУУСАХ ОГНОО',140),('remaining','ХУГАЦАА',160),('vendor','НИЙЛҮҮЛЭГЧ',150)],
          'technicians':[('name','НЭР',240),('contact','УТАС / ИМЭЙЛ',300)]}
        self.current_table=table; self.columns=defs[table]; self.tree=self.table(self.main,self.columns,[])
        self.tree.bind('<Double-1>',lambda e:self.open_selection(self.tree,table))
        bottom=tk.Frame(self.main,bg=BG); bottom.pack(fill='x',pady=15)
        self.label(bottom,'Мөрийг давхар дарж дэлгэрэнгүй мэдээлэл, засварын түүх харна.',9,MUTED).pack(side='left')
        self.button(bottom,'Сонгосныг нээх',lambda:self.open_selection(self.tree,table)).pack(side='right')
        self.fill_records()

    def fill_records(self):
        self.tree.delete(*self.tree.get_children())
        assets={r['id']:r['code'] for r in self.store.all('assets')}; tech={r['id']:r['name'] for r in self.store.all('technicians')}
        q=self.query.get().casefold()
        for r in self.store.all(self.current_table):
            r['asset']=assets.get(r.get('asset_id'),'—'); r['tech']=tech.get(r.get('technician_id'),'—')
            if self.current_table=='licenses':
                d=(date.fromisoformat(r['expires'])-date.today()).days if r['expires'] else None
                r['remaining']='Хугацаагүй' if d is None else f'{abs(d)} хоног '+('хэтэрсэн' if d<0 else 'үлдсэн')
            if q and q not in ' '.join(str(v) for v in r.values()).casefold(): continue
            if self.current_table=='tickets' and self.filter.get()!='Бүгд' and r['status']!=self.filter.get(): continue
            self.tree.insert('','end',iid=str(r['id']),values=[f"HD-{r['id']:04}" if c[0]=='id' else r.get(c[0],'') for c in self.columns])

    def open_selection(self,tree,table):
        sel=tree.selection()
        if not sel: messagebox.showinfo('Сонгох','Жагсаалтаас мөр сонгоно уу.'); return
        self.editor(table,int(sel[0]))

    def editor(self,table,ident=None):
        existing=self.store.get(table,ident) if ident else {}
        win=tk.Toplevel(self); win.title('Бүртгэл засах' if ident else 'Шинэ бүртгэл'); win.geometry('800x750'); win.minsize(720,600); win.configure(bg=BG); win.transient(self); win.grab_set()
        # Canvas keeps long forms usable on smaller monitors.
        canvas=tk.Canvas(win,bg=BG,highlightthickness=0); scroll=ttk.Scrollbar(win,orient='vertical',command=canvas.yview); scroll.pack(side='right',fill='y'); canvas.pack(fill='both',expand=True); canvas.configure(yscrollcommand=scroll.set)
        body=tk.Frame(canvas,bg=BG,padx=25,pady=20); item=canvas.create_window((0,0),window=body,anchor='nw'); body.bind('<Configure>',lambda e:canvas.configure(scrollregion=canvas.bbox('all'))); canvas.bind('<Configure>',lambda e:canvas.itemconfigure(item,width=e.width))
        self.label(body,('HD-%04d · '%ident if table=='tickets' and ident else '')+('Бүртгэл засах' if ident else 'Шинэ бүртгэл'),19,bold=True).pack(anchor='w',pady=(0,15))
        assets={'— Холбоогүй —':None}; assets.update({f"{a['code']} · {a['name']}":a['id'] for a in self.store.all('assets')})
        technicians={'— Хуваарилаагүй —':None}; technicians.update({f"{a['name']} (#{a['id']})":a['id'] for a in self.store.all('technicians')})
        defs={
          'assets':[('code','Бүртгэлийн код *',None),('name','Төхөөрөмжийн нэр *',None),('serial','Серийн дугаар',None),('owner','Эзэмшигч',None),('department','Хэлтэс / байршил',None),('specs','Үзүүлэлт, тайлбар','text'),('state','Төлөв',('Ашиглаж байна','Засварт','Нөөцөд','Ашиглалтаас гарсан'))],
          'technicians':[('name','Техникчийн нэр *',None),('contact','Утас / имэйл',None)],
          'licenses':[('name','Программын нэр *',None),('asset_id','Суулгасан төхөөрөмж',assets),('seats','Лицензийн суудлын тоо *',None),('expires','Дуусах огноо · YYYY-MM-DD (хугацаагүй бол хоосон)',None),('vendor','Нийлүүлэгч',None),('note','Тайлбар (нууц түлхүүр бүү хадгал)','text')],
          'tickets':[('title','Асуудлын гарчиг *',None),('requester','Хүсэлт гаргасан ажилтан',None),('asset_id','Төхөөрөмж',assets),('technician_id','Хариуцсан техникч',technicians),('priority','Зэрэглэл',PRIORITIES),('status','Төлөв',STATUSES),('description','Асуудлын дэлгэрэнгүй','text')]}
        controls={}; defaults={'seats':1,'state':'Ашиглаж байна','priority':'Хэвийн','status':'Шинэ'}
        for field,label,choices in defs[table]:
            self.label(body,label,10,MUTED).pack(anchor='w',pady=(9,4))
            value=existing.get(field,defaults.get(field,''))
            if choices=='text':
                widget=tk.Text(body,height=3,font=('Segoe UI',10),relief='flat',padx=10,pady=8); widget.insert('1.0',str(value or ''))
            elif isinstance(choices,dict):
                widget=ttk.Combobox(body,values=list(choices),state='readonly'); widget.set(next((k for k,v in choices.items() if v==value),next(iter(choices))))
            elif choices:
                widget=ttk.Combobox(body,values=choices,state='readonly'); widget.set(value or choices[0])
            else:
                widget=ttk.Entry(body); widget.insert(0,str(value or ''))
            widget.pack(fill='x'); controls[field]=(widget,choices)
        def save():
            try:
                data={}
                for field,(widget,choices) in controls.items():
                    value=widget.get('1.0','end-1c') if choices=='text' else widget.get()
                    data[field]=choices[value] if isinstance(choices,dict) else value
                self.store.save(table,data,ident); win.destroy(); self.refresh()
            except (ValueError,sqlite3.Error) as e: messagebox.showerror('Хадгалж чадсангүй',str(e),parent=win)
        self.button(body,'Өөрчлөлт хадгалах' if ident else 'Бүртгэх',save,True).pack(anchor='e',pady=20)
        if ident and table in ('tickets','assets'):
            self.label(body,'Засвар, үйлчилгээний түүх',14,bold=True).pack(anchor='w',pady=(15,10))
            if table=='tickets': self.button(body,'+ Хийсэн ажил, минут бүртгэх',lambda:self.work_dialog(ident,win)).pack(anchor='w',pady=(0,10))
            hist=self.store.history(ticket=ident) if table=='tickets' else self.store.history(asset=ident)
            if not hist: self.label(body,'Одоогоор түүх алга.',10,MUTED).pack(anchor='w')
            for h in hist:
                card=tk.Frame(body,bg='white',padx=12,pady=10); card.pack(fill='x',pady=4)
                self.label(card,f"{h['created'].replace('T',' ')} · {h['technician']} · {h['minutes']} минут",9,MUTED).pack(anchor='w')
                self.label(card,h['note'],10,wraplength=650,justify='left').pack(anchor='w',pady=(5,0))
        win.bind('<Escape>',lambda e:win.destroy())

    def work_dialog(self,ticket,parent):
        dialog=tk.Toplevel(parent); dialog.title('Хийсэн ажил'); dialog.geometry('500x390'); dialog.configure(bg=BG); dialog.transient(parent); dialog.grab_set()
        body=tk.Frame(dialog,bg=BG,padx=24,pady=20); body.pack(fill='both',expand=True)
        mapping={'— Сонгох —':None}; mapping.update({f"{a['name']} (#{a['id']})":a['id'] for a in self.store.all('technicians')})
        self.label(body,'Ажил гүйцэтгэсэн техникч').pack(anchor='w'); tech=ttk.Combobox(body,values=list(mapping),state='readonly'); tech.set(next(iter(mapping))); tech.pack(fill='x',pady=7)
        self.label(body,'Зарцуулсан минут').pack(anchor='w'); minutes=ttk.Entry(body); minutes.insert(0,'0'); minutes.pack(fill='x',pady=7)
        self.label(body,'Хийсэн ажил, шийдэл *').pack(anchor='w'); note=tk.Text(body,height=5,font=('Segoe UI',10)); note.pack(fill='x',pady=7)
        def save():
            try:
                self.store.add_work(ticket,mapping[tech.get()],note.get('1.0','end-1c'),minutes.get()); dialog.destroy(); parent.destroy(); self.refresh(); self.editor('tickets',ticket)
            except (ValueError,sqlite3.Error) as e: messagebox.showerror('Алдаа',str(e),parent=dialog)
        self.button(body,'Ажлыг хадгалах',save,True).pack(anchor='e',pady=10)

    def reports(self):
        bar=tk.Frame(self.main,bg=BG); bar.pack(fill='x',pady=10)
        today=date.today(); self.start=ttk.Entry(bar,width=14); self.start.insert(0,today.replace(day=1).isoformat()); self.end=ttk.Entry(bar,width=14); self.end.insert(0,today.isoformat())
        self.label(bar,'Эхлэх').pack(side='left',padx=(0,8)); self.start.pack(side='left'); self.label(bar,'Дуусах').pack(side='left',padx=8); self.end.pack(side='left')
        self.button(bar,'Тооцоолох',self.fill_report,True).pack(side='left',padx=12); self.button(bar,'CSV татах',self.export_current).pack(side='right')
        self.label(self.main,'Огноо: YYYY-MM-DD · Минут нь ажлын бүртгэлийн бодит нийлбэр.',9,MUTED).pack(anchor='w',pady=10)
        self.columns=[('name','ТЕХНИКЧ',250),('assigned','БҮРТГЭГДСЭН ХҮСЭЛТ',180),('resolved','ШИЙДВЭРЛЭСЭН',150),('minutes','АЖИЛЛАСАН МИНУТ',180)]
        self.tree=self.table(self.main,self.columns,[]); self.fill_report()
    def fill_report(self):
        try:
            rows=self.store.report(self.start.get(),self.end.get())
            self.tree.delete(*self.tree.get_children())
            for i,r in enumerate(rows): self.tree.insert('','end',iid=str(i),values=[r[c[0]] for c in self.columns])
        except ValueError as e: messagebox.showerror('Огноо шалгана уу',str(e))
    def export_current(self):
        path=filedialog.asksaveasfilename(defaultextension='.csv',filetypes=[('CSV','*.csv')],initialfile='helpdesk-report.csv')
        if not path: return
        try:
            with open(path,'w',encoding='utf-8-sig',newline='') as f:
                writer=csv.writer(f); writer.writerow([c[1] for c in self.columns])
                for row in self.tree.get_children():
                    # Neutralize spreadsheet formula injection from user-entered text.
                    values=self.tree.item(row,'values'); writer.writerow(["'"+str(v) if str(v).lstrip().startswith(('=','+','-','@')) else v for v in values])
            messagebox.showinfo('Бэлэн','CSV файл хадгаллаа. Excel-ээр нээж болно.')
        except OSError as e: messagebox.showerror('Алдаа',str(e))
    def settings(self):
        card=tk.Frame(self.main,bg='white',padx=25,pady=25); card.pack(fill='x')
        self.label(card,'Өгөгдлийн нөөц',16,bold=True).pack(anchor='w')
        self.label(card,'Өгөгдлийн сан: '+str(self.store.path),10,MUTED,wraplength=750,justify='left').pack(anchor='w',pady=15)
        self.button(card,'Нөөц хуулбар хадгалах',self.backup,True).pack(anchor='w')
        self.label(card,'Нөөцөөс сэргээхдээ программыг хааж, helpdesk.db файлыг нөөцөөр солино.\nСолихын өмнө одоогийн сангаа заавал тусад нь хадгална.',10,MUTED,justify='left').pack(anchor='w',pady=15)
        self.label(card,'Ашиглалтын хүрээ',16,bold=True).pack(anchor='w',pady=(20,10))
        self.label(card,'• Нэг компьютер дээр ажиллах офлайн хувилбар.\n• Олон төхөөрөмжийн бүртгэл хөтөлнө; төхөөрөмжөөс автоматаар мэдээлэл татахгүй.\n• Лицензийн сануулга програм нээлттэй үед болон эхлэхэд гарна.\n• 30 хоногийн дотор дуусах болон хугацаа хэтэрсэн лицензийг харуулна.\n• Энэ хувилбар олон хэрэглэгчийн сервер, нэвтрэх эрх, имэйл илгээлт агуулаагүй.',10,MUTED,justify='left',wraplength=760).pack(anchor='w')
    def backup(self):
        path=filedialog.asksaveasfilename(defaultextension='.db',initialfile=f'helpdesk-backup-{date.today()}.db',filetypes=[('SQLite database','*.db')])
        if path:
            try: self.store.backup(path); messagebox.showinfo('Амжилттай','Нөөц хуулбар хадгаллаа.')
            except (ValueError,OSError,sqlite3.Error) as e: messagebox.showerror('Алдаа',str(e))
    def startup_reminder(self):
        reminders=self.store.reminders()
        if reminders: messagebox.showwarning('Лицензийн сануулга',f'{len(reminders)} лицензийн хугацааг шалгах шаардлагатай.\nПрограмм, лиценз хэсгээс дэлгэрэнгүй харна уу.')
    def daily_check(self):
        if date.today()!=self.last_reminder:
            self.last_reminder=date.today(); self.startup_reminder()
            if self.view in ('Хяналтын самбар','Программ, лиценз'): self.refresh()
        self.after(60000,self.daily_check)

if __name__=='__main__':
    App().mainloop()
