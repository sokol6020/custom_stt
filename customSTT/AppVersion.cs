using System.Reflection;

namespace customSTT;

public static class AppVersion
{
    public static string Current { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
}
