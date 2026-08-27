using System.Runtime.InteropServices;

namespace Ai_OutlookNewMailBlink.Infrastructure.Win32;

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    // Flash both the taskbar button and the caption.
    internal const uint FLASHW_ALL = 0x00000003;
    // Keep flashing until the window receives focus.
    internal const uint FLASHW_TIMERNOFG = 0x0000000C;
    internal const uint FLASHW_STOP = 0;

    [DllImport("user32.dll")]
    internal static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    [DllImport("user32.dll")]
    internal static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    // Hints Chromium/WebView2-hosted apps (like the new Outlook) that an assistive technology is
    // present, which makes them build their otherwise-lazy full UI Automation accessibility tree.
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    internal const uint SPI_SETSCREENREADER = 0x0047;
    internal const uint SPIF_SENDCHANGE = 0x0002;

    internal const int SW_RESTORE = 9;
}
