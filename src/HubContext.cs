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

internal sealed class HubContext : ApplicationContext
{
    readonly Control dispatcher = new Control();
    readonly NotifyIcon tray;
    readonly IQuickFeature[] features;
    readonly SearchShortcut search;
    readonly HintWindow hint = new HintWindow();
    readonly EventWaitHandle stop = new EventWaitHandle(false, EventResetMode.AutoReset, Program.StopName);
    readonly EventWaitHandle show = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ShowName);
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    Task<string> reading;
    Task<QueueResult> sending;
    string pendingQuery;
    DateTime readStart;
    bool timedOut;
    bool closing;
    Form input;
    HubWindow dashboard;
    internal HubContext(bool showWindow) {
        var unused = dispatcher.Handle;
        tray = new NotifyIcon { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath), Text = "The Hub · Alt+Enter", Visible = true };
        search = new SearchShortcut(Capture);
        features = new IQuickFeature[] { search };
        var menu = new ContextMenuStrip();
        menu.Items.Add("打开 The Hub", null, delegate { ShowHub(); });
        menu.Items.Add("快速输入…", null, delegate { ShowInput(""); });
        foreach (var feature in features) menu.Items.Add(feature.MenuItem);
        menu.Items.Add("打开固定助手对话", null, delegate { try { CodexLauncher.OpenThread(); } catch (Exception ex) { Notify(ex); } });
        menu.Items.Add("编辑助手规则", null, delegate {
            try { OpenFile(File.ReadAllText(Path.Combine(Program.Home, "rules-path.txt")).Trim()); }
            catch (Exception ex) { Notify(ex); }
        });
        menu.Items.Add("预览快捷提示", null, delegate { hint.Preview(); });
        menu.Items.Add("打开程序目录", null, delegate { OpenFile(Program.Home); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, delegate { ExitThread(); });
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += delegate { ShowHub(); };
        timer.Interval = 300;
        timer.Tick += Tick;
        timer.Start();
        Program.Log("Started");
        if (showWindow) dispatcher.BeginInvoke((Action)ShowHub);
    }
    void ShowHub() {
        if (dashboard == null || dashboard.IsDisposed) {
            dashboard = new HubWindow(Launch, () => hint.Preview(), () => search.MenuItem.PerformClick(), () => search.Enabled);
            dashboard.FormClosed += delegate { dashboard = null; };
        }
        dashboard.Show(); dashboard.WindowState = FormWindowState.Normal; dashboard.Activate();
    }
    void OpenFile(string path) { try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch (Exception ex) { Notify(ex); } }
    void Capture(IntPtr window) {
        if (closing) return;
        dispatcher.BeginInvoke((Action)delegate {
            if (sending != null) { hint.ShowError("上一条正在发送，请稍候。"); return; }
            if (input != null) { input.Activate(); return; }
            if (reading != null) { if (timedOut) ShowInput("搜索框读取暂不可用，请在此输入。"); return; }
            timedOut = false;
            readStart = DateTime.UtcNow;
            reading = Task.Factory.StartNew(() => SearchReader.Read(window));
        });
    }
    void Tick(object sender, EventArgs e) {
        if (stop.WaitOne(0)) { ExitThread(); return; }
        if (show.WaitOne(0)) ShowHub();
        hint.UpdateSearch(search.Enabled && input == null, (reading != null && !timedOut) || sending != null);
        timer.Interval = hint.Visible || reading != null ? 100 : 300;
        if (sending != null && sending.IsCompleted) {
            var outcome = sending.Result; sending = null;
            if (outcome.Accepted) {
                hint.ShowSent();
                try { CodexLauncher.OpenThread(); } catch { hint.ShowError("消息已发送，请手动打开助手对话。"); }
            } else { hint.ShowError(outcome.Error); ShowInput(outcome.Error, pendingQuery); }
            pendingQuery = null;
        }
        if (reading == null) return;
        if (reading.IsCompleted) {
            string value = null;
            if (reading.IsFaulted) Program.Log(reading.Exception.GetBaseException().GetType().Name);
            else value = reading.Result;
            reading = null;
            if (!timedOut) {
                if (String.IsNullOrWhiteSpace(value)) ShowInput("未能读取搜索框，请在此输入。");
                else Launch(value);
            }
        } else if (!timedOut && DateTime.UtcNow - readStart > TimeSpan.FromSeconds(2)) {
            timedOut = true;
            ShowInput("搜索框读取超时，请在此输入。");
        }
    }
    void ShowInput(string message, string initial = "") {
        if (input != null) { input.Activate(); return; }
        var form = new Form { Text = "The Hub 快速助手", ClientSize = new Size(520, 188), StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, TopMost = true, Font = new Font("Microsoft YaHei UI", 10) };
        input = form;
        var label = new Label { Text = message.Length == 0 ? "输入应用、设置名称或问题" : message, AutoSize = true, Location = new Point(16, 15) };
        var box = new TextBox { Location = new Point(16, 48), Width = 488, MaxLength = CodexLauncher.MaxQuery, Text = initial };
        var instructions = new Label { Text = "同一个对话 · 已保存规则\n回车直接发送，无需粘贴。", Location = new Point(16, 92), Size = new Size(340, 55), ForeColor = Color.DimGray };
        var send = new Button { Text = "直接发送", Location = new Point(274, 142), Size = new Size(110, 32) };
        var cancel = new Button { Text = "取消", Location = new Point(394, 142), Size = new Size(110, 32), DialogResult = DialogResult.Cancel };
        var copy = new Button { Text = "复制转入", Location = new Point(16, 142), Size = new Size(110, 32) };
        copy.Click += delegate {
            try { CodexLauncher.Open(box.Text); hint.ShowCopied(); form.Close(); } catch (Exception ex) { Notify(ex); }
        };
        send.Click += delegate { if (String.IsNullOrWhiteSpace(box.Text)) return; if (Launch(box.Text)) form.Close(); };
        form.Controls.AddRange(new Control[] { label, box, instructions, send, copy, cancel });
        form.AcceptButton = send; form.CancelButton = cancel;
        form.FormClosed += delegate { input = null; form.Dispose(); };
        form.Shown += delegate { box.Focus(); };
        form.Show(); form.Activate();
    }
    bool Launch(string query) {
        if (sending != null) { hint.ShowError("上一条正在发送，请稍候。"); return false; }
        try {
            CodexLauncher.ValidateQuery(query);
            CodexLauncher.ThreadUri();
            pendingQuery = query;
            sending = Task.Factory.StartNew(() => QueueDelivery.Send(query));
            hint.ShowSending();
            return true;
        }
        catch (Exception ex) { Notify(ex); return false; }
    }
    void Notify(Exception ex) {
        Program.Log(ex.GetType().Name);
        hint.ShowError(ex is ArgumentException ? ex.Message : "请检查对话配置或剪贴板是否被占用。");
    }
    protected override void ExitThreadCore() {
        closing = true; timer.Stop();
        foreach (var feature in features) feature.Dispose();
        if (input != null) input.Close();
        if (dashboard != null) dashboard.Close();
        tray.Visible = false;
        Program.Log("Stopped");
        base.ExitThreadCore();
    }
    protected override void Dispose(bool disposing) {
        if (disposing) { hint.Dispose(); timer.Dispose(); stop.Dispose(); show.Dispose(); tray.ContextMenuStrip.Dispose(); tray.Icon.Dispose(); tray.Dispose(); dispatcher.Dispose(); }
        base.Dispose(disposing);
    }
}
