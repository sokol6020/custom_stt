namespace customSTT;

public static class StartupOptions
{
    public static bool MinimizeToTrayOnStartup { get; private set; }

    public static void Parse(string[] args)
    {
        foreach (var arg in args)
        {
            var normalized = arg.Trim().TrimStart('-').TrimStart('/').ToLowerInvariant();
            if (normalized is "minimize-to-tray" or "tray" or "minimized")
                MinimizeToTrayOnStartup = true;
        }
    }
}
