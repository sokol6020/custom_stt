using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace customSTT.Services;

public class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "customSTT";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        var value = key?.GetValue(ValueName) as string;
        return !string.IsNullOrWhiteSpace(value);
    }

    public void SetEnabled(bool enabled, bool minimizeToTrayOnStartup)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        if (key == null)
            throw new InvalidOperationException("Не удалось открыть ключ автозапуска Windows.");

        if (!enabled)
        {
            key.DeleteValue(ValueName, false);
            return;
        }

        var exePath = Process.GetCurrentProcess().MainModule?.FileName
                      ?? Path.Combine(AppContext.BaseDirectory, "customSTT.exe");

        var command = $"\"{exePath}\"";
        if (minimizeToTrayOnStartup)
            command += " --minimize-to-tray";

        key.SetValue(ValueName, command);
    }
}
