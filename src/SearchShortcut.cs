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

internal sealed class SearchShortcut : IQuickFeature
{
    readonly Native.Hook callback;
    readonly Action<IntPtr> invoke;
    IntPtr hook;
    bool swallowed;
    bool enabled = true;
    readonly ToolStripMenuItem item = new ToolStripMenuItem("启用搜索 Alt+Enter") { Checked = true, CheckOnClick = true };
    public ToolStripItem MenuItem { get { return item; } }
    internal bool Enabled { get { return enabled; } }
    internal SearchShortcut(Action<IntPtr> action) {
        invoke = action; callback = Handle;
        item.CheckedChanged += delegate { enabled = item.Checked; };
        hook = Native.SetWindowsHookEx(13, callback, Native.GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
    IntPtr Handle(int code, IntPtr message, IntPtr data) {
        try {
            if (code >= 0) {
                var key = (Native.Keyboard)Marshal.PtrToStructure(data, typeof(Native.Keyboard));
                if (key.vkCode == 13) {
                    bool down = message.ToInt64() == 0x100 || message.ToInt64() == 0x104;
                    bool up = message.ToInt64() == 0x101 || message.ToInt64() == 0x105;
                    if (swallowed && up) { swallowed = false; return new IntPtr(1); }
                    if (swallowed && down) return new IntPtr(1);
                    if (enabled && down && (key.flags & 0x20) != 0 && (Native.GetAsyncKeyState(0x11) & 0x8000) == 0 && (Native.GetAsyncKeyState(0x10) & 0x8000) == 0) {
                        IntPtr window = Native.GetForegroundWindow();
                        if (SearchReader.IsSearchWindow(window)) { swallowed = true; invoke(window); return new IntPtr(1); }
                    }
                }
            }
        } catch { /* Never disrupt keyboard input when the target window disappears. */ }
        return Native.CallNextHookEx(hook, code, message, data);
    }
    public void Dispose() { if (hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; } }
}
