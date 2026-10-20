"""Run on Windows CI, which has a real Tk display."""
import os
import tempfile
import unittest
from pathlib import Path
from app import App

@unittest.skipUnless(os.name=='nt' or os.environ.get('DISPLAY'),'GUI display required')
class UITests(unittest.TestCase):
    def test_views_and_record_forms(self):
        with tempfile.TemporaryDirectory() as tmp:
            app=App(Path(tmp)/'ui.db'); app.withdraw()
            try:
                for view in app.nav:
                    app.navigate(view); app.update_idletasks()
                for table in ('assets','tickets','licenses','technicians'):
                    app.editor(table); app.update_idletasks()
                    for child in app.winfo_children():
                        if child.winfo_class()=='Toplevel': child.destroy()
            finally: app.shutdown()
