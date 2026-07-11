using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace customSTT.Services;

internal static class MonitorNameResolver
{
    private const int MonitorNameDescriptorType = 0xFC;

    public static string Resolve(Screen screen)
    {
        var fallback = FormatDeviceName(screen.DeviceName);

        if (!TryGetBestMonitorDevice(screen.DeviceName, out var monitorDevice))
            return fallback;

        var edidName = TryGetEdidModelName(monitorDevice);
        if (IsUsableName(edidName))
            return edidName!;

        var deviceString = monitorDevice.DeviceString?.Trim();
        if (IsUsableName(deviceString))
            return deviceString!;

        return fallback;
    }

    private static bool TryGetBestMonitorDevice(string adapterDeviceName, out DisplayDevice monitorDevice)
    {
        monitorDevice = default;

        for (uint monitorIndex = 0; ; monitorIndex++)
        {
            var candidate = CreateDisplayDevice();
            if (!NativeMethods.EnumDisplayDevices(adapterDeviceName, monitorIndex, ref candidate, 0))
                break;

            if ((candidate.StateFlags & DisplayDeviceStateFlags.AttachedToDesktop) == 0)
                continue;

            monitorDevice = candidate;
            return true;
        }

        return false;
    }

    private static string? TryGetEdidModelName(DisplayDevice monitorDevice)
    {
        var edid = ReadEdid(monitorDevice);
        if (edid == null)
            return null;

        return ParseMonitorNameFromEdid(edid);
    }

    private static byte[]? ReadEdid(DisplayDevice monitorDevice)
    {
        var fromDeviceKey = ReadEdidFromDeviceKey(monitorDevice.DeviceKey);
        if (fromDeviceKey != null)
            return fromDeviceKey;

        return ReadEdidFromDisplayRegistry(monitorDevice.DeviceID);
    }

    private static byte[]? ReadEdidFromDeviceKey(string? deviceKey)
    {
        if (string.IsNullOrWhiteSpace(deviceKey))
            return null;

        var registryPath = NormalizeRegistryPath(deviceKey);
        if (string.IsNullOrWhiteSpace(registryPath))
            return null;

        return ReadEdidValue(registryPath);
    }

    private static byte[]? ReadEdidFromDisplayRegistry(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return null;

        var displaySegment = ExtractDisplaySegment(deviceId);
        if (string.IsNullOrWhiteSpace(displaySegment))
            return null;

        using var displayRoot = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{displaySegment}");
        if (displayRoot == null)
            return null;

        foreach (var instanceName in displayRoot.GetSubKeyNames())
        {
            var edid = ReadEdidValue($@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{displaySegment}\{instanceName}");
            if (edid != null)
                return edid;
        }

        return null;
    }

    private static byte[]? ReadEdidValue(string registryPath)
    {
        using var parametersKey = Registry.LocalMachine.OpenSubKey($@"{registryPath}\Device Parameters");
        if (parametersKey?.GetValue("EDID") is byte[] edid && edid.Length >= 128)
            return edid;

        return null;
    }

    private static string? NormalizeRegistryPath(string deviceKey)
    {
        const string machinePrefix = @"\Registry\Machine\";
        if (deviceKey.StartsWith(machinePrefix, StringComparison.OrdinalIgnoreCase))
            return deviceKey[machinePrefix.Length..];

        return deviceKey.TrimStart('\\');
    }

    private static string? ExtractDisplaySegment(string deviceId)
    {
        var parts = deviceId.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("MONITOR", StringComparison.OrdinalIgnoreCase))
                return parts[i + 1];
        }

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("DISPLAY", StringComparison.OrdinalIgnoreCase))
                return parts[i + 1];
        }

        return null;
    }

    private static string? ParseMonitorNameFromEdid(byte[] edid)
    {
        for (var offset = 54; offset <= 108; offset += 18)
        {
            if (edid[offset] != 0 || edid[offset + 1] != 0 || edid[offset + 2] != 0)
                continue;

            if (edid[offset + 3] != MonitorNameDescriptorType)
                continue;

            var name = Encoding.ASCII.GetString(edid, offset + 5, 13)
                .Replace('\0', ' ')
                .Trim();

            if (IsUsableName(name))
                return name;
        }

        return null;
    }

    private static bool IsUsableName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return !name.Contains("Generic PnP Monitor", StringComparison.OrdinalIgnoreCase)
               && !name.Contains("Generic Non-PnP Monitor", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatDeviceName(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return "неизвестно";

        const string prefix = @"\\.\";
        if (deviceName.StartsWith(prefix, StringComparison.Ordinal))
            return deviceName[prefix.Length..];

        return deviceName;
    }

    private static DisplayDevice CreateDisplayDevice()
    {
        return new DisplayDevice
        {
            cb = Marshal.SizeOf<DisplayDevice>()
        };
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DisplayDevice
    {
        public int cb;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;

        public DisplayDeviceStateFlags StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [Flags]
    private enum DisplayDeviceStateFlags
    {
        AttachedToDesktop = 0x00000001
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern bool EnumDisplayDevices(
            string? lpDevice,
            uint iDevNum,
            ref DisplayDevice lpDisplayDevice,
            uint dwFlags);
    }
}
