namespace RainyRemote;
internal static class Program {
 [STAThread] static void Main(string[] args) {
  ApplicationConfiguration.Initialize();
  if(args.Contains("--self-test")){SelfTest.Run();return;}
  Application.Run(new MainForm());
 }
}
