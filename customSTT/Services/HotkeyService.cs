using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using customSTT.Models;

namespace customSTT.Services;

public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

public class HotkeyService : IDisposable
{
    public const int RecordingHotkeyId = 9000;
    public const int OverlayHotkeyId = 9001;
    public const int HotkeyId = RecordingHotkeyId;

    private const int WhKeyboardLl = 13;
    private const int WmKeydown = 0x0100;
    private const int WmKeyup = 0x0101;
    private const int WmSyskeydown = 0x0104;
    private const int WmSyskeyup = 0x0105;

    private IntPtr _windowHandle;
    private bool _recordingRegistered;
    private bool _overlayRegistered;
    private string _registeredHotkey = string.Empty;
    private string _registeredOverlayHotkey = string.Empty;

    private RecordingHotkeyMode _recordingMode = RecordingHotkeyMode.Toggle;
    private ModifierKeys _holdModifiers = ModifierKeys.None;
    private int _holdVirtualKey;
    private bool _holdKeyActive;
    private IntPtr _keyboardHook = IntPtr.Zero;
    private LowLevelKeyboardProc? _keyboardHookProc;

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

        if (!InstallKeyboardHook())
        {
            _recordingRegistered = false;
            _registeredHotkey = string.Empty;
            System.Diagnostics.Trace.WriteLine($"HotkeyService: failed to install keyboard hook for '{hotkey}'");
            return false;
        }

        System.Diagnostics.Trace.WriteLine($"Hold hotkey registered: {hotkey}");
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
            int error = Marshal.GetLastWin32Error();
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
        RemoveKeyboardHook();
        _holdKeyActive = false;

        if (_windowHandle != IntPtr.Zero)
        {
            if (_recordingRegistered)
                HotkeyWin32.UnregisterHotKey(_windowHandle, RecordingHotkeyId);

            if (_overlayRegistered)
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

    private bool InstallKeyboardHook()
    {
        if (_keyboardHook != IntPtr.Zero)
            return true;

        _keyboardHookProc = KeyboardHookCallback;

        var moduleName = Process.GetCurrentProcess().MainModule?.ModuleName;
        _keyboardHook = HotkeyWin32.SetWindowsHookEx(
            WhKeyboardLl,
            _keyboardHookProc,
            HotkeyWin32.GetModuleHandle(moduleName),
            0);

        if (_keyboardHook == IntPtr.Zero)
        {
            _keyboardHook = HotkeyWin32.SetWindowsHookEx(
                WhKeyboardLl,
                _keyboardHookProc,
                HotkeyWin32.GetModuleHandle(null),
                0);
        }

        if (_keyboardHook == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            System.Diagnostics.Trace.WriteLine($"SetWindowsHookEx failed: error={error}");
            _keyboardHookProc = null;
            return false;
        }

        return true;
    }

    private void RemoveKeyboardHook()
    {
        if (_keyboardHook == IntPtr.Zero)
            return;

        HotkeyWin32.UnhookWindowsHookEx(_keyboardHook);
        _keyboardHook = IntPtr.Zero;
        _keyboardHookProc = null;
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _recordingMode == RecordingHotkeyMode.Hold && _recordingRegistered)
        {
            var hookStruct = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            int message = wParam.ToInt32();

            if (message is WmKeydown or WmSyskeydown or WmKeyup or WmSyskeyup)
                ProcessHoldHotkey(hookStruct.vkCode);
        }

        return HotkeyWin32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private void ProcessHoldHotkey(uint vkCode)
    {
        if (!IsRelevantHoldKey(vkCode))
            return;

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

    private bool IsRelevantHoldKey(uint vkCode)
    {
        if (vkCode == _holdVirtualKey)
            return true;

        return vkCode switch
        {
            0x10 or 0x11 or 0x12 or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5 => true,
            _ => false
        };
    }

    private bool IsRecordingChordDown()
    {
        if ((HotkeyWin32.GetAsyncKeyState(_holdVirtualKey) & 0x8000) == 0)
            return false;

        if ((_holdModifiers & ModifierKeys.Control) != 0 &&
            (HotkeyWin32.GetAsyncKeyState(0x11) & 0x8000) == 0)
            return false;

        if ((_holdModifiers & ModifierKeys.Alt) != 0 &&
            (HotkeyWin32.GetAsyncKeyState(0x12) & 0x8000) == 0)
            return false;

        if ((_holdModifiers & ModifierKeys.Shift) != 0 &&
            (HotkeyWin32.GetAsyncKeyState(0x10) & 0x8000) == 0)
            return false;

        return true;
    }

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

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}

internal static class HotkeyWin32
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);
}
