using System.IO;
using System.Text;
using System.Windows.Automation;

namespace Ai_OutlookNewMailBlink.Application.Services;

// Diagnostic helper: dumps the UI Automation tree of a window to a text file so the
// InboxFolderNames setting can be tuned to match the real folder-name text shown in
// the user's Outlook build/locale.
public static class AutomationTreeDumper
{
    private const int MaxDepth = 10;
    private const int MaxNodes = 4000;

    public static void DumpToFile(IntPtr hWnd, string filePath)
    {
        var builder = new StringBuilder();
        var nodeCount = 0;

        try
        {
            var root = AutomationElement.FromHandle(hWnd);
            Walk(root, 0, builder, ref nodeCount);
        }
        catch (Exception ex)
        {
            builder.AppendLine($"[error] {ex.GetType().Name}: {ex.Message}");
        }

        WriteToFile(builder, filePath);
    }

    public static void DumpTaskbarToFile(string filePath)
    {
        var builder = new StringBuilder();
        var nodeCount = 0;

        try
        {
            var taskbarHandle = Ai_OutlookNewMailBlink.Infrastructure.Win32.NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (taskbarHandle == IntPtr.Zero)
            {
                builder.AppendLine("[error] No se encontró la ventana Shell_TrayWnd.");
            }
            else
            {
                var root = AutomationElement.FromHandle(taskbarHandle);
                Walk(root, 0, builder, ref nodeCount);
            }
        }
        catch (Exception ex)
        {
            builder.AppendLine($"[error] {ex.GetType().Name}: {ex.Message}");
        }

        WriteToFile(builder, filePath);
    }

    private static void WriteToFile(StringBuilder builder, string filePath)
    {
        var directoryPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        File.WriteAllText(filePath, builder.ToString());
    }

    private static void Walk(AutomationElement element, int depth, StringBuilder builder, ref int nodeCount)
    {
        if (depth > MaxDepth || nodeCount > MaxNodes)
        {
            return;
        }

        nodeCount++;

        try
        {
            var current = element.Current;
            var indent = new string(' ', depth * 2);
            builder.AppendLine($"{indent}[{current.ControlType.LocalizedControlType}] Name=\"{current.Name}\" AutomationId=\"{current.AutomationId}\" ClassName=\"{current.ClassName}\"");
        }
        catch (ElementNotAvailableException)
        {
            return;
        }

        AutomationElement? child;
        try
        {
            child = TreeWalker.ControlViewWalker.GetFirstChild(element);
        }
        catch (ElementNotAvailableException)
        {
            return;
        }

        while (child is not null && nodeCount <= MaxNodes)
        {
            Walk(child, depth + 1, builder, ref nodeCount);

            try
            {
                child = TreeWalker.ControlViewWalker.GetNextSibling(child);
            }
            catch (ElementNotAvailableException)
            {
                break;
            }
        }
    }
}
