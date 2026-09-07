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

internal static class SearchReader
{
    internal static bool IsSearchProcess(string name) {
        return String.Equals(name, "SearchHost", StringComparison.OrdinalIgnoreCase) ||
               String.Equals(name, "SearchApp", StringComparison.OrdinalIgnoreCase) ||
               String.Equals(name, "SearchUI", StringComparison.OrdinalIgnoreCase);
    }
    internal static bool IsSearchWindow(IntPtr window) {
        try {
            uint pid; Native.GetWindowThreadProcessId(window, out pid);
            using (var p = Process.GetProcessById((int)pid)) {
                if (!IsSearchProcess(p.ProcessName)) return false;
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SystemApps") + Path.DirectorySeparatorChar;
                return p.MainModule.FileName.StartsWith(root, StringComparison.OrdinalIgnoreCase);
            }
        } catch { return false; }
    }
    internal static string Value(AutomationElement edit) {
        if (edit == null || edit.Current.IsPassword || edit.Current.ControlType != ControlType.Edit) return null;
        object pattern;
        if (edit.TryGetCurrentPattern(ValuePattern.Pattern, out pattern)) return ((ValuePattern)pattern).Current.Value;
        if (edit.TryGetCurrentPattern(TextPattern.Pattern, out pattern)) return ((TextPattern)pattern).DocumentRange.GetText(CodexLauncher.MaxQuery + 1);
        return null;
    }
    internal static string Read(IntPtr window) {
        if (Native.GetForegroundWindow() != window || !IsSearchWindow(window)) return null;
        var root = AutomationElement.FromHandle(window);
        var edits = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
        string value = null;
        int candidates = 0;
        foreach (AutomationElement edit in edits) {
            if (edit.Current.IsOffscreen) continue;
            string candidate = Value(edit);
            if (String.IsNullOrWhiteSpace(candidate)) continue;
            candidates++; value = candidate;
        }
        if (Native.GetForegroundWindow() != window || candidates != 1) return null;
        return value;
    }
}
