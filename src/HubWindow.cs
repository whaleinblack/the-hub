using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

internal sealed class HubWindow : Form
{
    static readonly Color Background = Color.FromArgb(18, 23, 31);
    static readonly Color Surface = Color.FromArgb(30, 38, 49);
    readonly TextBox query;
    internal HubWindow(Func<string, bool> deliver, Action preview, Action toggle, Func<bool> enabled) {
        Text = "The Hub"; ClientSize = new Size(620, 438); BackColor = Background; ForeColor = Color.WhiteSmoke;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Font = new Font("Microsoft YaHei UI", 10); StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        Controls.Add(Label("THE HUB", 28, 22, 400, 38, 25, Color.White, true));
        Controls.Add(Label("一个入口，连接你的 Windows。", 30, 64, 560, 28, 11, Color.FromArgb(161, 177, 195), false));
        var inputPanel = new Panel { Location = new Point(30, 114), Size = new Size(560, 52), BackColor = Surface, Padding = new Padding(12) };
        query = new TextBox { BorderStyle = BorderStyle.None, BackColor = Surface, ForeColor = Color.White, Location = new Point(13, 15), Width = 413, MaxLength = CodexLauncher.MaxQuery, AccessibleName = "输入应用、设置名称或问题" };
        inputPanel.Controls.Add(query);
        var send = Button("交给助手 ↵", 440, 8, 110, 36, delegate { if (deliver(query.Text)) query.Clear(); });
        send.BackColor = Color.FromArgb(103, 221, 181); send.ForeColor = Background;
        inputPanel.Controls.Add(send); Controls.Add(inputPanel); AcceptButton = send;
        Controls.Add(Label("输入应用、设置名称或问题 · 回车直接发送到固定助手", 31, 174, 558, 24, 9, Color.FromArgb(159, 176, 195), false));
        Controls.Add(Button("固定助手对话", 30, 220, 174, 48, delegate { TryOpen(CodexLauncher.OpenThread); }));
        Controls.Add(Button("查看快捷提示", 223, 220, 174, 48, delegate { preview(); }));
        Controls.Add(Button("连接设置", 416, 220, 174, 48, delegate { using (var settings = new ConnectionWindow()) settings.ShowDialog(this); }));
        var shortcut = Button("", 30, 282, 560, 40, null);
        Action refresh = () => shortcut.Text = enabled() ? "●  Windows 搜索 Alt+Enter 已开启    ·    点击暂停" : "○  Windows 搜索 Alt+Enter 已暂停    ·    点击开启";
        shortcut.Click += delegate { toggle(); refresh(); };
        Activated += delegate { refresh(); }; refresh(); Controls.Add(shortcut);
        Controls.Add(Label("扩展方向", 31, 345, 520, 25, 10, Color.WhiteSmoke, true));
        Controls.Add(Label("用户快捷操作   /   AI 调度   /   系统定制", 31, 372, 550, 26, 10, Color.FromArgb(139, 162, 183), false));
        Controls.Add(Label("关闭窗口后仍在托盘运行", 31, 410, 540, 18, 8, Color.FromArgb(116, 139, 162), false));
        Shown += delegate { query.Focus(); };
    }
    static Label Label(string text, int x, int y, int width, int height, float size, Color color, bool bold) {
        return new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height), ForeColor = color, Font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular) };
    }
    static Button Button(string text, int x, int y, int width, int height, EventHandler click) {
        var button = new Button { Text = text, Location = new Point(x, y), Size = new Size(width, height), FlatStyle = FlatStyle.Flat, BackColor = Surface, ForeColor = Color.WhiteSmoke, Cursor = Cursors.Hand };
        button.FlatAppearance.BorderSize = 0; if (click != null) button.Click += click; return button;
    }
    void TryOpen(Action action) {
        try { action(); } catch { MessageBox.Show(this, "请先在连接设置中填写固定助手对话链接。", "The Hub"); }
    }
}

internal sealed class ConnectionWindow : Form
{
    internal ConnectionWindow() {
        Text = "The Hub · 连接设置"; ClientSize = new Size(560, 245); StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; Font = new Font("Microsoft YaHei UI", 10);
        Controls.Add(new Label { Text = "固定对话链接或编号", Location = new Point(18, 18), AutoSize = true });
        var target = new TextBox { Location = new Point(18, 46), Width = 524 };
        string file = Path.Combine(Program.Home, "thread-id.txt");
        if (File.Exists(file)) target.Text = File.ReadAllText(file).Trim();
        Controls.Add(target);
        Controls.Add(new Label { Text = "在 Codex / ChatGPT 桌面应用中打开目标对话，复制会话链接。\n只发送查询文字；规则保留在该对话。", Location = new Point(18, 82), Size = new Size(524, 52) });
        Controls.Add(new Label { Text = "本地规则文件（可选）", Location = new Point(18, 139), AutoSize = true });
        var rules = new TextBox { Location = new Point(18, 164), Width = 524 };
        string rulesFile = Path.Combine(Program.Home, "rules-path.txt");
        if (File.Exists(rulesFile)) rules.Text = File.ReadAllText(rulesFile).Trim();
        Controls.Add(rules);
        var save = new Button { Text = "保存", Location = new Point(432, 204), Width = 110 };
        save.Click += delegate {
            try {
                string value = target.Text.Trim();
                const string prefix = "codex://threads/";
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) value = value.Substring(prefix.Length);
                string uri = CodexLauncher.BuildThreadUri(value);
                string path = rules.Text.Trim();
                if (path.Length > 0 && (!File.Exists(path) || !String.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("规则文件必须是已存在的 Markdown 文件。");
                File.WriteAllText(file, uri.Substring(prefix.Length));
                File.WriteAllText(rulesFile, path);
                DialogResult = DialogResult.OK; Close();
            } catch (Exception ex) { MessageBox.Show(this, ex is ArgumentException ? ex.Message : "无法保存连接设置。", "The Hub"); }
        };
        Controls.Add(save); AcceptButton = save;
    }
}
