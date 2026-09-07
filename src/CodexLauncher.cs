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

internal static class CodexLauncher
{
    internal const int MaxQuery = 2000;
    internal static void ValidateQuery(string query) {
        if (String.IsNullOrWhiteSpace(query) || query.Length > MaxQuery) throw new ArgumentException("请输入 1–2000 字的内容。");
    }
    internal static string BuildThreadUri(string target) {
        Guid id;
        if (!Guid.TryParseExact(target.Trim(), "D", out id) || id == Guid.Empty) throw new ArgumentException("固定助手对话编号无效，请检查配置。");
        return "codex://threads/" + id.ToString("D");
    }
    internal static string ThreadUri() { return BuildThreadUri(File.ReadAllText(Path.Combine(Program.Home, "thread-id.txt"))); }
    internal static void OpenThread() { Process.Start(new ProcessStartInfo(ThreadUri()) { UseShellExecute = true }); }
    internal static void Deliver(string query, string target, Action<string> copy, Action<string> open) {
        ValidateQuery(query);
        string uri = BuildThreadUri(target);
        copy(query.Trim());
        open(uri);
    }
    internal static void Open(string query) {
        Deliver(query, File.ReadAllText(Path.Combine(Program.Home, "thread-id.txt")),
            text => Clipboard.SetDataObject(text, true, 3, 60),
            uri => Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }));
    }
}
