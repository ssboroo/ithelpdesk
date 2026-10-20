import os
import tempfile
import unittest
from unittest.mock import patch
from app import App

@unittest.skipUnless(os.name=='nt' or os.environ.get('DISPLAY'),'GUI display required')
class UITests(unittest.TestCase):
    def test_remote_only_views_and_manual_connect(self):
        with tempfile.TemporaryDirectory() as root:
            app=App(root);app.withdraw()
            try:
                self.assertEqual(len(app.notebook.tabs()),3)
                for tab in app.notebook.tabs():app.notebook.select(tab);app.update_idletasks()
                self.assertFalse(hasattr(app,'store'))
                app.peer.set('123 456 789')
                with patch('app.messagebox.showerror') as error,patch.object(app.engine,'launch') as launch:
                    app.connect();error.assert_called_once();launch.assert_not_called()
                app.allowed.set(True)
                with patch.object(app.engine,'launch') as launch:
                    app.connect();launch.assert_called_once_with('123456789','control')
                self.assertEqual(len(app.recent.get_children()),1);self.assertFalse(app.allowed.get())
                self.assertEqual(app.contacts.data['recent'][0]['peer'],'123456789')
                app.contacts.save('Тест PC','123456789');app.refresh_lists();app.book.selection_set('123456789');app.select_contact()
                self.assertEqual(app.peer.get(),'123456789')
            finally:app.shutdown()
