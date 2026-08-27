using System.Drawing;

namespace Ai_OutlookNewMailBlink.Shared.Helpers;

public static class AppIconProvider
{
    private static readonly Lazy<Icon> LazyIcon = new(() =>
        Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath)!);

    public static Icon Current => LazyIcon.Value;
}
