using System;
using System.Threading;
using System.Windows.Input;
using customSTT.Models;

namespace customSTT.Services;

public class HotkeyService : IDisposable
{
    public const int RecordingHotkeyId = 9000;
    public const int OverlayHotkeyId = 9001;
    public const int HotkeyId = RecordingHotkeyId;

    private const int HoldPollIntervalMs = 10;

    private IntPtr _windowHandle;
    private bool _recordingRegistered;
    private bool _overlayRegistered;
    private string _registeredHotkey = string.Empty;
    private string _registeredOverlayHotkey = string.Empty;

    private RecordingHotkeyMode _recordingMode = RecordingHotkeyMode.Toggle;
    private ModifierKeys _holdModifiers = ModifierKeys.None;
    private int _holdVirtualKey;
    private volatile bool _holdKeyActive;
    private volatile bool _holdPollingActive;
    private Thread? _holdPollThread;

    public event Action? HotkeyActivated;
    public event Action? HotkeyDeactivated;
    public event Action? OverlayHotkeyActivated;

    public string RegisteredHotkey => _registeredHotkey;
    public string RegisteredOverlayHotkey => _registeredOverlayHotkey;
    public RecordingHotkeyMode RecordingMode => _recordingMode;

    public void SetWindowHandle(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public void SetRecordingHotkeyMode(RecordingHotkeyMode mode)
    {
        _recordingMode = mode;
    }

    public bool RegisterHotkeys(string recordingHotkey, string? overlayHotkey = null)
    {
        if (_windowHandle == IntPtr.Zero)
        {
            System.Diagnostics.Trace.WriteLine("HotkeyService: window handle is not set");
            return false;
        }

        UnregisterHotkeys();

        bool recordingOk = _recordingMode == RecordingHotkeyMode.Hold
            ? RegisterHoldHotkey(recordingHotkey)
            : RegisterSingleHotkey(recordingHotkey, RecordingHotkeyId, ref _recordingRegistered, ref _registeredHotkey);

        if (!string.IsNullOrWhiteSpace(overlayHotkey))
            RegisterSingleHotkey(overlayHotkey, OverlayHotkeyId, ref _overlayRegistered, ref _registeredOverlayHotkey);

        return recordingOk;
    }

    private bool RegisterHoldHotkey(string hotkey)
    {
        if (!TryParseHotkey(hotkey, out _holdModifiers, out _holdVirtualKey))
        {
            System.Diagnostics.Trace.WriteLine($"HotkeyService: invalid hold hotkey '{hotkey}'");
            return false;
        }

        _holdKeyActive = false;
        _registeredHotkey = hotkey;
        _recordingRegistered = true;
        StartHoldPolling();
        System.Diagnostics.Trace.WriteLine($"Hold hotkey registered (polling): {hotkey}");
        return true;
    }

    private bool RegisterSingleHotkey(string hotkey, int id, ref bool registeredFlag, ref string registeredValue)
    {
        if (!TryParseHotkey(hotkey, out var modifiers, out var virtualKey))
        {
            System.Diagnostics.Trace.WriteLine($"HotkeyService: invalid hotkey '{hotkey}'");
            return false;
        }

        bool result = HotkeyWin32.RegisterHotKey(
            _windowHandle,
            id,
            GetModifierFlags(modifiers),
            (uint)virtualKey);

        if (!result)
        {
            int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            System.Diagnostics.Trace.WriteLine(
                $"RegisterHotKey failed: error={error}, hotkey={hotkey}, id={id}, vk=0x{virtualKey:X}, mods={GetModifierFlags(modifiers)}");
            return false;
        }

        registeredFlag = true;
        registeredValue = hotkey;
        System.Diagnostics.Trace.WriteLine($"Hotkey registered: {hotkey} (id={id})");
        return true;
    }

    public void UnregisterHotkeys()
    {
        StopHoldPolling();
        _holdKeyActive = false;

        if (_windowHandle != IntPtr.Zero)
        {
            HotkeyWin32.UnregisterHotKey(_windowHandle, RecordingHotkeyId);
            HotkeyWin32.UnregisterHotKey(_windowHandle, OverlayHotkeyId);
        }

        _recordingRegistered = false;
        _overlayRegistered = false;
        _registeredHotkey = string.Empty;
        _registeredOverlayHotkey = string.Empty;
    }

    public void NotifyHotkeyPressed(int hotkeyId)
    {
        switch (hotkeyId)
        {
            case RecordingHotkeyId:
                if (_recordingMode == RecordingHotkeyMode.Toggle)
                    HotkeyActivated?.Invoke();
                break;
            case OverlayHotkeyId:
                OverlayHotkeyActivated?.Invoke();
                break;
        }
    }

    private void StartHoldPolling()
    {
        StopHoldPolling();
        _holdPollingActive = true;
        _holdPollThread = new Thread(HoldPollLoop)
        {
            IsBackground = true,
            Name = "RecordingHotkeyHoldPoll"
        };
        _holdPollThread.Start();
    }

    private void StopHoldPolling()
    {
        _holdPollingActive = false;
        var thread = _holdPollThread;
        if (thread is { IsAlive: true })
            thread.Join(500);
        _holdPollThread = null;
    }

    private void HoldPollLoop()
    {
        while (_holdPollingActive)
        {
            try
            {
                bool chordDown = IsRecordingChordDown();

                if (chordDown && !_holdKeyActive)
                {
                    _holdKeyActive = true;
                    HotkeyActivated?.Invoke();
                }
                else if (!chordDown && _holdKeyActive)
                {
                    _holdKeyActive = false;
                    HotkeyDeactivated?.Invoke();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Hold poll error: {ex.Message}");
            }

            Thread.Sleep(HoldPollIntervalMs);
        }
    }

    private bool IsRecordingChordDown()
    {
        if (!IsVirtualKeyDown(_holdVirtualKey))
            return false;

        if ((_holdModifiers & ModifierKeys.Control) != 0 && !IsControlDown())
            return false;

        if ((_holdModifiers & ModifierKeys.Alt) != 0 && !IsAltDown())
            return false;

        if ((_holdModifiers & ModifierKeys.Shift) != 0 && !IsShiftDown())
            return false;

        return true;
    }

    private static bool IsVirtualKeyDown(int virtualKey) =>
        (HotkeyWin32.GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static bool IsControlDown() =>
        IsVirtualKeyDown(0x11) || IsVirtualKeyDown(0xA2) || IsVirtualKeyDown(0xA3);

    private static bool IsAltDown() =>
        IsVirtualKeyDown(0x12) || IsVirtualKeyDown(0xA4) || IsVirtualKeyDown(0xA5);

    private static bool IsShiftDown() =>
        IsVirtualKeyDown(0x10) || IsVirtualKeyDown(0xA0) || IsVirtualKeyDown(0xA1);

    public static bool TryParseHotkey(string hotkey, out ModifierKeys modifiers, out int virtualKey)
    {
        modifiers = ModifierKeys.None;
        virtualKey = 0;

        if (string.IsNullOrWhiteSpace(hotkey))
            return false;

        var parts = hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;

        if (parts.Length == 1)
        {
            virtualKey = ParseVirtualKey(parts[0]);
            return virtualKey != 0;
        }

        var hasModifier = false;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i])
            {
                case "Ctrl":
                    modifiers |= ModifierKeys.Control;
                    hasModifier = true;
                    break;
                case "Alt":
                    modifiers |= ModifierKeys.Alt;
                    hasModifier = true;
                    break;
                case "Shift":
                    modifiers |= ModifierKeys.Shift;
                    hasModifier = true;
                    break;
                default:
                    return false;
            }
        }

        if (!hasModifier)
            return false;

        virtualKey = ParseVirtualKey(parts[^1]);
        return virtualKey != 0;
    }

    public static string FormatHotkey(ModifierKeys modifiers, Key key)
    {
        var keyName = FormatKey(key);
        if (keyName == null)
            return string.Empty;

        var parts = new System.Collections.Generic.List<string>();
        if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        parts.Add(keyName);
        return string.Join("+", parts);
    }

    private static string? FormatKey(Key key)
    {
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.System)
            return null;

        if (key is >= Key.A and <= Key.Z)
            return ((char)('A' + (key - Key.A))).ToString();

        if (key is >= Key.D0 and <= Key.D9)
            return ((char)('0' + (key - Key.D0))).ToString();

        return key switch
        {
            Key.Escape => "Esc",
            Key.Space => "Space",
            Key.Insert => "Insert",
            Key.Delete => "Delete",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Tab => "Tab",
            Key.CapsLock => "CapsLock",
            Key.NumLock => "NumLock",
            Key.Scroll => "ScrollLock",
            Key.PrintScreen => "PrintScreen",
            Key.Pause => "Pause",
            >= Key.F1 and <= Key.F24 => key.ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => key.ToString(),
            _ => null
        };
    }

    private static int ParseVirtualKey(string keyPart)
    {
        if (keyPart.StartsWith('F') && int.TryParse(keyPart[1..], out int fNum) && fNum is >= 1 and <= 24)
            return 0x70 + fNum - 1;

        if (keyPart.StartsWith("NumPad") && int.TryParse(keyPart["NumPad".Length..], out int numpad) && numpad is >= 0 and <= 9)
            return 0x60 + numpad;

        return keyPart.ToUpperInvariant() switch
        {
            "A" => 0x41, "B" => 0x42, "C" => 0x43, "D" => 0x44, "E" => 0x45,
            "F" => 0x46, "G" => 0x47, "H" => 0x48, "I" => 0x49, "J" => 0x4A,
            "K" => 0x4B, "L" => 0x4C, "M" => 0x4D, "N" => 0x4E, "O" => 0x4F,
            "P" => 0x50, "Q" => 0x51, "R" => 0x52, "S" => 0x53, "T" => 0x54,
            "U" => 0x55, "V" => 0x56, "W" => 0x57, "X" => 0x58, "Y" => 0x59,
            "Z" => 0x5A, "0" => 0x30, "1" => 0x31, "2" => 0x32, "3" => 0x33,
            "4" => 0x34, "5" => 0x35, "6" => 0x36, "7" => 0x37, "8" => 0x38,
            "9" => 0x39, "ESC" => 0x1B, "SPACE" => 0x20,
            "INSERT" => 0x2D, "DELETE" => 0x2E, "HOME" => 0x24, "END" => 0x23,
            "PAGEUP" => 0x21, "PAGEDOWN" => 0x22, "TAB" => 0x09,
            "CAPSLOCK" => 0x14, "NUMLOCK" => 0x90, "SCROLLLOCK" => 0x91,
            "PRINTSCREEN" => 0x2C, "PAUSE" => 0x13,
            _ => 0
        };
    }

    private static uint GetModifierFlags(ModifierKeys modifiers)
    {
        uint flags = 0;

        if ((modifiers & ModifierKeys.Alt) != 0)
            flags |= 0x1;

        if ((modifiers & ModifierKeys.Control) != 0)
            flags |= 0x2;

        if ((modifiers & ModifierKeys.Shift) != 0)
            flags |= 0x4;

        return flags;
    }

    public void Dispose()
    {
        UnregisterHotkeys();
        GC.SuppressFinalize(this);
    }
}

internal static class HotkeyWin32
{
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);
}
