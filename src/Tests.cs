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

internal static class Tests
{
    static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    internal static int Run() {
        string report = Path.Combine(Program.Home, "test-results.txt");
        try {
            string query = "蓝牙 & price? # % + \"quotes\"\n第二行";
            string target = "12345678-1234-1234-1234-123456789abc";
            string copied = null, opened = null;
            CodexLauncher.Deliver(query, target, text => copied = text, uri => opened = uri);
            Check(copied == query, "Only query copied, Unicode preserved");
            Check(opened == "codex://threads/" + target, "Fixed thread, no query in URL");
            copied = null; opened = null;
            try { CodexLauncher.Deliver(query, "new?prompt=oops", text => copied = text, uri => opened = uri); } catch (ArgumentException) { }
            Check(copied == null && opened == null, "Invalid target has no side effects");
            bool failed = false;
            try { CodexLauncher.Deliver(query, target, text => { throw new ExternalException(); }, uri => opened = uri); } catch (ExternalException) { failed = true; }
            Check(failed && opened == null, "Clipboard failure does not navigate");
            string[] quotes = { query, "end\\", "\\\"quoted\"", "", "a & b | c $(d)", "line\nnext" };
            foreach (string value in quotes) {
                int count;
                var argv = Native.CommandLineToArgvW("hub " + QueueDelivery.Quote(value), out count);
                try { Check(count == 2 && Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, IntPtr.Size)) == value, "Windows argv round trip"); }
                finally { Native.LocalFree(argv); }
            }
            Check(QueueDelivery.IsAcknowledged("Queued message test for thread " + target + ".\n", target), "Queue acknowledgement");
            Check(!QueueDelivery.IsAcknowledged("Queued message test for thread different.", target), "Wrong-thread acknowledgement rejected");
            bool rejected = false;
            try { CodexLauncher.ValidateQuery(new string('a', 2001)); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Query length bound");
            rejected = false;
            try { CodexLauncher.ValidateQuery(" "); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Empty query rejected");
            Check(SearchReader.IsSearchProcess("SearchHost") && !SearchReader.IsSearchProcess("explorer") && !SearchReader.IsSearchProcess("SearchHostFake"), "Search process filter");
            var area = new Rectangle(-1920, 0, 1920, 1040);
            var location = HintWindow.Place(new Rectangle(-800, 0, 800, 1040), area, new Size(348, 88));
            Check(area.Contains(new Rectangle(location, new Size(348, 88))), "Hint stays on secondary screen");
            var panel = new Rectangle(100, 300, 830, 700);
            var upper = HintWindow.Place(panel, new Rectangle(0, 0, 1920, 1080), new Size(348, 88));
            Check(upper.Y + 88 <= panel.Top && upper.X >= panel.Left && upper.X + 348 <= panel.Right, "Hint docks above search, never under right companion panel");
            using (var form = new Form()) {
                var box = new TextBox { Text = query.Replace("\n", " ") }; form.Controls.Add(box); form.Show();
                var handle = box.Handle;
                var read = Task.Factory.StartNew(() => {
                    var element = AutomationElement.FromHandle(handle);
                    string result = SearchReader.Value(element);
                    if (result == null) throw new Exception("Edit unavailable: " + element.Current.ControlType.ProgrammaticName + ", offscreen=" + element.Current.IsOffscreen + ", password=" + element.Current.IsPassword);
                    return result;
                });
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (!read.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(10); }
                if (read.IsFaulted) throw read.Exception;
                Check(read.IsCompleted && read.Result == box.Text, "Live UI Automation Edit read");
                form.Close();
            }
            File.WriteAllText(report, "PASS: query-only Unicode delivery; fixed thread routing; invalid target has no side effects; clipboard failure does not navigate; Windows argv escaping; queue acknowledgement; wrong-thread rejection; length bound; empty input; search filter; secondary screen bounds; above-panel docking; live UI Automation Edit read.\n");
            return 0;
        } catch (Exception ex) { File.WriteAllText(report, "FAIL: " + ex.ToString()); return 1; }
    }
}
