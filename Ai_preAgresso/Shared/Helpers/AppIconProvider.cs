namespace Ai_preAgresso.Shared.Helpers;

// Reuses the icon embedded in the exe (ApplicationIcon in the csproj) so every form/taskbar entry matches.
public static class AppIconProvider
{
    private static readonly Lazy<Icon> LazyIcon = new(() => Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath)!);

    public static Icon Current => LazyIcon.Value;
}
