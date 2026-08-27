using Ai_OutlookNewMailBlink.Infrastructure.Win32;

namespace Ai_OutlookNewMailBlink.Application.Services;

// Wraps the FlashWindowEx Win32 API to blink a window's taskbar button/caption.
public sealed class WindowFlashService
{
    public void StartFlashing(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return;
        }

        var info = new NativeMethods.FLASHWINFO
        {
            hwnd = hWnd,
            dwFlags = NativeMethods.FLASHW_ALL | NativeMethods.FLASHW_TIMERNOFG,
            uCount = 0,
            dwTimeout = 0
        };
        info.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(info);

        NativeMethods.FlashWindowEx(ref info);
    }

    public void StopFlashing(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return;
        }

        var info = new NativeMethods.FLASHWINFO
        {
            hwnd = hWnd,
            dwFlags = NativeMethods.FLASHW_STOP,
            uCount = 0,
            dwTimeout = 0
        };
        info.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(info);

        NativeMethods.FlashWindowEx(ref info);
    }
}
