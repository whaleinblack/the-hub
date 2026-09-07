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

internal static class Native
{
    internal delegate IntPtr Hook(int code, IntPtr message, IntPtr data);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr CommandLineToArgvW(string command, out int count);
    [DllImport("kernel32.dll")] internal static extern IntPtr LocalFree(IntPtr pointer);
    [StructLayout(LayoutKind.Sequential)] internal struct Keyboard { public uint vkCode, scanCode, flags, time; public UIntPtr extra; }
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] internal static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetWindowsHookEx(int id, Hook callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
}
