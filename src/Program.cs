using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;

internal static class Program
{
    internal static readonly string Home = AppDomain.CurrentDomain.BaseDirectory;
    internal const string StopName = @"Local\TheHub.Stop.v1";
    internal const string ShowName = @"Local\TheHub.Show.v1";
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--self-test") return Tests.Run();
        if (args.Length > 0 && args[0] == "--render-preview") { HintWindow.RenderPreview(); return 0; }
        if (args.Length == 2 && args[0] == "--send") {
            var result = QueueDelivery.Send(args[1]);
            Console.WriteLine(result.Accepted ? "{\"accepted\":true}" : "{\"accepted\":false,\"uncertain\":" + (result.Uncertain ? "true" : "false") + "}");
            return result.Accepted ? 0 : 2;
        }
        if (args.Length > 0 && args[0] == "--stop") {
            try { using (var e = EventWaitHandle.OpenExisting(StopName)) e.Set(); } catch (WaitHandleCannotBeOpenedException) { }
            return 0;
        }
        bool first;
        using (var mutex = new Mutex(true, @"Local\TheHub.v1", out first)) {
            if (!first) {
                if (args.Length == 0 || args[0] != "--background") {
                    for (int attempt = 0; attempt < 20; attempt++) {
                        try { using (var signal = EventWaitHandle.OpenExisting(ShowName)) signal.Set(); break; }
                        catch (WaitHandleCannotBeOpenedException) { Thread.Sleep(50); }
                    }
                }
                return 0;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { using (var context = new HubContext(args.Length == 0 || args[0] != "--background")) Application.Run(context); }
            catch (Exception ex) { Log(ex.GetType().Name); return 1; }
            mutex.ReleaseMutex();
        }
        return 0;
    }
    internal static void Log(string message) {
        try {
            string path = Path.Combine(Home, "companion.log");
            if (File.Exists(path) && new FileInfo(path).Length > 131072) File.WriteAllText(path, "");
            File.AppendAllText(path, DateTime.UtcNow.ToString("s") + " " + message + Environment.NewLine);
        } catch { }
    }
}
