namespace RainyRemote;
internal static class Program {
 [STAThread] static void Main(string[] args) {
  ApplicationConfiguration.Initialize();
  if(args.Contains("--self-test")){SelfTest.Run();return;}
  using var instance=new Mutex(true,@"Local\RainyRemote.Interactive",out bool first);
  if(!first){AppInstance.Activate();return;}
  try{Application.Run(new MainForm());}finally{instance.ReleaseMutex();}
 }
}
