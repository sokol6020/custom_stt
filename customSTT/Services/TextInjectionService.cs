using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace customSTT.Services;

public class TextInjectionService : IDisposable
{
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint WM_CHAR = 0x0102;
    private const int VK_CONTROL = 0x11;
    private const int VK_V = 0x56;
    private const int VK_RETURN = 0x0D;
    private const int VK_SPACE = 0x20;
    private const int CharDelayMs = 12;

    private IntPtr _targetWindow;
    private readonly uint _currentProcessId;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint dwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    public TextInjectionService()
    {
        _currentProcessId = GetCurrentProcessId();
    }

    public void RememberTargetWindow()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
        {
            _targetWindow = IntPtr.Zero;
            return;
        }

        GetWindowThreadProcessId(hwnd, out uint processId);
        _targetWindow = processId == _currentProcessId ? IntPtr.Zero : hwnd;
    }

    public void ClearTargetWindow()
    {
        _targetWindow = IntPtr.Zero;
    }

    public async Task InjectTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var targetWindow = ResolveTargetWindow();

        try
        {
            await Task.Run(() => TypeText(targetWindow, text));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка вставки текста: {ex.Message}");
            await Application.Current.Dispatcher.InvokeAsync(() => Clipboard.SetText(text));
        }
        finally
        {
            ClearTargetWindow();
        }
    }

    private IntPtr ResolveTargetWindow()
    {
        if (_targetWindow != IntPtr.Zero && IsWindow(_targetWindow))
            return _targetWindow;

        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
            return IntPtr.Zero;

        GetWindowThreadProcessId(hwnd, out uint processId);
        return processId == _currentProcessId ? IntPtr.Zero : hwnd;
    }

    private void TypeText(IntPtr targetWindow, string text)
    {
        if (targetWindow != IntPtr.Zero)
            ActivateTargetWindow(targetWindow);

        var focusHwnd = GetFocusedControl(targetWindow);
        if (focusHwnd == IntPtr.Zero)
            focusHwnd = targetWindow;

        if (focusHwnd != IntPtr.Zero && TryTypeWithMessages(focusHwnd, text))
            return;

        if (TryTypeWithSendInput(text))
            return;

        TypeWithClipboard(text, targetWindow);
    }

    private void ActivateTargetWindow(IntPtr targetWindow)
    {
        GetWindowThreadProcessId(targetWindow, out uint processId);
        AllowSetForegroundWindow(processId);
        ShowWindow(targetWindow, 5);
        BringWindowToTop(targetWindow);
        SetForegroundWindow(targetWindow);
        Thread.Sleep(150);
    }

    private IntPtr GetFocusedControl(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero)
            return IntPtr.Zero;

        var targetThread = GetWindowThreadProcessId(targetWindow, out _);
        var currentThread = GetCurrentThreadId();
        var attached = false;

        try
        {
            if (targetThread != currentThread)
                attached = AttachThreadInput(currentThread, targetThread, true);

            var focus = GetFocus();
            if (focus != IntPtr.Zero)
                return focus;
        }
        finally
        {
            if (attached)
                AttachThreadInput(currentThread, targetThread, false);
        }

        return targetWindow;
    }

    private bool TryTypeWithMessages(IntPtr hwnd, string text)
    {
        var typed = false;

        foreach (var c in text)
        {
            if (c == '\r')
                continue;

            if (c == '\n')
            {
                SendVirtualKey(VK_RETURN);
                typed = true;
                continue;
            }

            SendMessage(hwnd, WM_CHAR, (IntPtr)c, IntPtr.Zero);
            typed = true;
            Thread.Sleep(CharDelayMs);
        }

        return typed;
    }

    private bool TryTypeWithSendInput(string text)
    {
        foreach (var c in text)
        {
            if (c == '\r')
                continue;

            if (c == '\n')
            {
                SendVirtualKey(VK_RETURN);
                continue;
            }

            if (c == ' ')
            {
                SendVirtualKey(VK_SPACE);
                Thread.Sleep(CharDelayMs);
                continue;
            }

            SendUnicodeChar(c, 0);
            SendUnicodeChar(c, KEYEVENTF_KEYUP);
            Thread.Sleep(CharDelayMs);
        }

        return true;
    }

    private void TypeWithClipboard(string text, IntPtr targetWindow)
    {
        Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(text));

        if (targetWindow != IntPtr.Zero)
            ActivateTargetWindow(targetWindow);

        SendVirtualKey(VK_CONTROL, false);
        SendVirtualKey(VK_V, false);
        SendVirtualKey(VK_V, true);
        SendVirtualKey(VK_CONTROL, true);
    }

    private void SendVirtualKey(int vk, bool keyUp = false)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)vk,
                    wScan = 0,
                    dwFlags = keyUp ? KEYEVENTF_KEYUP : 0,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero
                }
            }
        };

        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private void SendUnicodeChar(char c, uint flags)
    {
        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = KEYEVENTF_UNICODE | flags,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero
                }
            }
        };

        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    public void CopyToClipboard(string text)
    {
        Clipboard.SetText(text);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
