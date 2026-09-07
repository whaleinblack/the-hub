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

internal sealed class HintWindow : Form
{
    string mode = "idle";
    string errorText = "";
    DateTime toastUntil;
    DateTime checkedAt;
    IntPtr checkedWindow;
    bool isSearch;
    internal HintWindow() {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        AutoScaleMode = AutoScaleMode.None; StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(348, 88); BackColor = Color.FromArgb(23, 28, 37);
        Opacity = 0.98; DoubleBuffered = true;
        Text = "The Hub 提示";
        Resize += delegate { using (var path = Rounded(new RectangleF(0, 0, Width, Height), 14)) { var old = Region; Region = new Region(path); if (old != null) old.Dispose(); } };
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams {
        get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x00000080 | 0x00080000 | 0x00000020; return p; }
    }
    internal void UpdateSearch(bool enabled, bool busy) {
        if (DateTime.UtcNow < toastUntil) { if (!Visible) Show(); return; }
        IntPtr window = Native.GetForegroundWindow();
        if (window != checkedWindow || DateTime.UtcNow - checkedAt > TimeSpan.FromSeconds(1)) {
            checkedWindow = window; checkedAt = DateTime.UtcNow; isSearch = SearchReader.IsSearchWindow(window);
        }
        Native.Rect rect;
        if (!enabled || !isSearch || !Native.GetWindowRect(window, out rect) || rect.Right <= rect.Left || rect.Bottom <= rect.Top) { Hide(); return; }
        var work = Screen.FromHandle(window).WorkingArea;
        if (rect.Top - Height - 8 < work.Top + 4) { Hide(); return; }
        // This process uses Windows DPI virtualization; all window and screen coordinates stay in the same logical space.
        ClientSize = new Size(Math.Min(348, work.Width - 16), 88);
        Location = Place(Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom), work, Size);
        SetMode(busy ? "reading" : ((Native.GetAsyncKeyState(0x12) & 0x8000) != 0 ? "alt" : "idle"));
        if (!Visible) Show();
    }
    internal static Point Place(Rectangle search, Rectangle work, Size size) {
        int x = search.Left + (search.Width - size.Width) / 2, y = search.Top - size.Height - 8;
        return new Point(Math.Max(work.Left + 8, Math.Min(x, work.Right - size.Width - 8)),
                         Math.Max(work.Top + 8, Math.Min(y, work.Bottom - size.Height - 8)));
    }
    void SetMode(string value) { if (mode != value) { mode = value; Invalidate(); } }
    void Toast(string value) {
        SetMode(value); toastUntil = DateTime.UtcNow.AddSeconds(7);
        var work = Screen.FromPoint(Cursor.Position).WorkingArea;
        ClientSize = new Size(Math.Min(348, work.Width - 16), 88);
        Location = new Point(work.Right - Width - 20, work.Bottom - Height - 20);
        Show(); Invalidate();
    }
    internal void ShowCopied() { Toast("copied"); }
    internal void ShowSent() { Toast("sent"); }
    internal void ShowSending() { Toast("sending"); }
    internal void ShowError(string text) { errorText = text; Toast("error"); }
    internal void Preview() { Toast("alt"); }
    static GraphicsPath Rounded(RectangleF r, float radius) {
        float d = radius * 2; var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    static void TextAt(Graphics g, string text, float size, FontStyle style, Color color, RectangleF bounds) {
        using (var font = new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(color))
        using (var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center })
            g.DrawString(text, font, brush, bounds, format);
    }
    static void Key(Graphics g, string text, RectangleF bounds, bool active) {
        using (var path = Rounded(bounds, 6))
        using (var fill = new SolidBrush(active ? Color.FromArgb(39, 69, 63) : Color.FromArgb(43, 50, 63)))
        using (var pen = new Pen(active ? Color.FromArgb(95, 208, 174) : Color.FromArgb(78, 89, 108))) {
            g.FillPath(fill, path); g.DrawPath(pen, path);
        }
        TextAt(g, text, 12, FontStyle.Bold, Color.FromArgb(233, 243, 248), new RectangleF(bounds.X + 9, bounds.Y, bounds.Width - 9, bounds.Height));
    }
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); PaintCard(e.Graphics, mode, errorText); }
    internal static void PaintCard(Graphics g, string state, string error) {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        bool active = state == "alt" || state == "copied" || state == "sent";
        Color accent = state == "error" ? Color.FromArgb(247, 176, 102) : active ? Color.FromArgb(115, 233, 195) : Color.FromArgb(146, 171, 215);
        using (var bg = new LinearGradientBrush(new Rectangle(0, 0, 348, 88), Color.FromArgb(31, 39, 50), Color.FromArgb(19, 25, 34), 25f))
        using (var border = new Pen(active ? Color.FromArgb(80, 143, 128) : Color.FromArgb(60, 70, 80), 1))
        using (var path = Rounded(new RectangleF(0.5f, 0.5f, 347, 87), 14)) { g.FillPath(bg, path); g.DrawPath(border, path); }
        using (var brush = new SolidBrush(accent)) {
            g.FillPolygon(brush, new PointF[] { new PointF(29, 19), new PointF(33, 29), new PointF(43, 33), new PointF(33, 37), new PointF(29, 47), new PointF(25, 37), new PointF(15, 33), new PointF(25, 29) });
        }
        string title = state == "alt" ? "直接发送给 The Hub" : state == "sent" ? "已送达固定助手对话" : state == "sending" ? "正在发送…" : state == "copied" ? "搜索文字已复制" : state == "reading" ? "正在读取搜索文字…" : state == "error" ? "暂时无法转入" : "The Hub 快速助手";
        TextAt(g, title, 15, FontStyle.Bold, Color.FromArgb(239, 245, 250), new RectangleF(53, 13, 282, 25));
        if (state == "error") TextAt(g, error, 12, FontStyle.Regular, accent, new RectangleF(53, 42, 279, 30));
        else if (state == "sent" || state == "sending") TextAt(g, state == "sent" ? "无需粘贴 · 助手将按队列处理" : "正在连接固定对话，请稍候", 12, FontStyle.Regular, accent, new RectangleF(53, 43, 270, 27));
        else if (state == "copied") {
            Key(g, "Ctrl+V", new RectangleF(53, 46, 68, 25), true);
            TextAt(g, "粘贴，再按", 12, FontStyle.Regular, Color.LightGray, new RectangleF(128, 46, 70, 25));
            Key(g, "Enter", new RectangleF(204, 46, 60, 25), true);
            TextAt(g, "发送", 12, FontStyle.Regular, Color.LightGray, new RectangleF(272, 46, 45, 25));
        } else if (state == "reading") TextAt(g, "稍后打开固定对话", 12, FontStyle.Regular, accent, new RectangleF(53, 43, 270, 27));
        else {
            Key(g, "Alt", new RectangleF(53, 46, 42, 25), active);
            TextAt(g, "+", 13, FontStyle.Regular, Color.Gray, new RectangleF(99, 46, 15, 25));
            Key(g, "Enter", new RectangleF(115, 46, 60, 25), active);
            TextAt(g, state == "alt" ? "松开 Alt 取消高亮" : "固定对话 · 无需重复规则", 11, FontStyle.Regular, accent, new RectangleF(184, 46, 154, 25));
        }
    }
    internal static void RenderPreview() {
        using (var bitmap = new Bitmap(744, 344))
        using (var g = Graphics.FromImage(bitmap)) {
            g.Clear(Color.FromArgb(11, 16, 23));
            TextAt(g, "THE HUB", 15, FontStyle.Bold, Color.FromArgb(139, 159, 184), new RectangleF(24, 12, 600, 28));
            string[] states = { "idle", "alt", "reading", "sent" };
            string[] labels = { "搜索时", "按住 Alt", "读取中", "跳转后" };
            for (int i = 0; i < 4; i++) {
                int x = 16 + i % 2 * 364, y = 82 + i / 2 * 132;
                TextAt(g, labels[i], 12, FontStyle.Regular, Color.FromArgb(150, 163, 180), new RectangleF(x + 3, y - 27, 300, 22));
                var saved = g.Save(); g.TranslateTransform(x, y); PaintCard(g, states[i], ""); g.Restore(saved);
            }
            bitmap.Save(Path.Combine(Program.Home, "visual-preview.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
