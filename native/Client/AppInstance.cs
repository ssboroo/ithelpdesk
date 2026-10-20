using System.Diagnostics;
using System.Runtime.InteropServices;
namespace RainyRemote;
static class AppInstance {
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr window);
 [DllImport("user32.dll")]static extern bool ShowWindowAsync(IntPtr window,int command);
 public static void Activate(){using var current=Process.GetCurrentProcess();foreach(var process in new[]{current.ProcessName,"RainyRemote","RainyRemote-Portable"}.Distinct().SelectMany(Process.GetProcessesByName))using(process){if(process.Id==current.Id||process.SessionId!=current.SessionId)continue;var window=process.MainWindowHandle;if(window==IntPtr.Zero)continue;ShowWindowAsync(window,9);SetForegroundWindow(window);break;}}
}
