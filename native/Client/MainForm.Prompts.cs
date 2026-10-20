namespace RainyRemote;
sealed partial class MainForm {
 Form? inlinePrompt;Panel? promptSurface;TaskCompletionSource<DialogResult>? promptResult;
 // Permission is still explicit, but it lives inside the existing application window.
 Task<DialogResult> ShowInline(Form form){
  CancelInline();var result=new TaskCompletionSource<DialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);inlinePrompt=form;promptResult=result;
  form.TopLevel=false;form.FormBorderStyle=FormBorderStyle.None;form.MinimumSize=Size.Empty;
  var surface=new Panel{Dock=DockStyle.Fill,BackColor=Ui.Canvas};promptSurface=surface;
  var preferred=form.Size;surface.Resize+=(_,_)=>{form.Size=new Size(Math.Min(preferred.Width,Math.Max(1,surface.Width-24)),Math.Min(preferred.Height,Math.Max(1,surface.Height-24)));form.Location=new Point(Math.Max(0,(surface.Width-form.Width)/2),Math.Max(0,(surface.Height-form.Height)/2));};foreach(var stack in Descendants(form).OfType<FlowLayoutPanel>())if(stack.FlowDirection==FlowDirection.TopDown)Ui.FitStack(stack);
  foreach(var button in Descendants(form).OfType<Button>())button.Click+=(_,_)=>{
   var choice=button.DialogResult!=DialogResult.None?button.DialogResult:form.DialogResult;
   if(choice==DialogResult.None||inlinePrompt!=form)return;
   surface.Controls.Remove(form);Controls.Remove(surface);surface.Dispose();promptSurface=null;form.Hide();inlinePrompt=null;promptResult=null;result.TrySetResult(choice);
  };
  surface.Controls.Add(form);Controls.Add(surface);surface.BringToFront();form.Show();form.Select();return result.Task;
 }
 static IEnumerable<Control> Descendants(Control parent){foreach(Control child in parent.Controls){yield return child;foreach(var item in Descendants(child))yield return item;}}
 void CancelInline(){var form=inlinePrompt;inlinePrompt=null;var result=promptResult;promptResult=null;if(form!=null){promptSurface?.Controls.Remove(form);form.Hide();}if(promptSurface!=null){Controls.Remove(promptSurface);promptSurface.Dispose();promptSurface=null;}result?.TrySetResult(DialogResult.Cancel);}
}
