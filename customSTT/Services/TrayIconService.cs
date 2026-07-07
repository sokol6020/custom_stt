using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;

namespace customSTT.Services;

public enum TrayIconStatus
{
    Ready,
    Recording,
    Processing,
    Downloading
}

public class TrayIconService : IDisposable
{
    private static readonly Color IdleColor = Color.FromArgb(156, 163, 175);
    private static readonly Color RecordingColor = Color.FromArgb(31, 157, 85);
    private static readonly Color ProcessingColor = Color.FromArgb(234, 179, 8);
    private static readonly Color DownloadingColor = Color.FromArgb(91, 141, 239);

    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private Icon? _idleIcon;
    private Icon? _recordingIcon;
    private Icon? _processingIcon;
    private Icon? _downloadingIcon;
    private bool _initialized;

    public void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        _idleIcon?.Dispose();
        _idleIcon = null;
        InitializeContextMenu();
        CreateNotifyIcon();
        SetStatus(TrayIconStatus.Ready);
    }

    private void InitializeContextMenu()
    {
        _contextMenu = new ContextMenuStrip();

        var minimizeItem = new ToolStripMenuItem("Минимизировать в трей");
        minimizeItem.Click += (_, _) => Minimize();

        var restoreItem = new ToolStripMenuItem("Восстановить окно");
        restoreItem.Click += (_, _) => Restore();

        var exitItem = new ToolStripMenuItem("Выход");
        exitItem.Click += (_, _) => Exit();

        _contextMenu.Items.Add(minimizeItem);
        _contextMenu.Items.Add(restoreItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);
    }

    private void CreateNotifyIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Icon = GetIdleIcon(),
            Text = "Speech to Text — ожидание"
        };

        _notifyIcon.ContextMenuStrip = _contextMenu;
        _notifyIcon.DoubleClick += OnDoubleClick;
    }

    public void SetStatus(TrayIconStatus status)
    {
        if (!_initialized)
            Initialize();

        if (_notifyIcon == null)
            return;

        _notifyIcon.Icon = status switch
        {
            TrayIconStatus.Recording => _recordingIcon ??= AppIconHelper.GetTrayIconFromColor(RecordingColor),
            TrayIconStatus.Processing => _processingIcon ??= AppIconHelper.GetTrayIconFromColor(ProcessingColor),
            TrayIconStatus.Downloading => _downloadingIcon ??= AppIconHelper.GetTrayIconFromColor(DownloadingColor),
            _ => GetIdleIcon()
        };

        _notifyIcon.Text = status switch
        {
            TrayIconStatus.Recording => "Speech to Text — запись",
            TrayIconStatus.Processing => "Speech to Text — обработка",
            TrayIconStatus.Downloading => "Speech to Text — скачивание",
            _ => "Speech to Text — ожидание"
        };
    }

    private Icon GetIdleIcon() => _idleIcon ??= AppIconHelper.GetTrayIconFromColor(IdleColor);

    private void OnDoubleClick(object? sender, EventArgs e) => Restore();

    public void Minimize()
    {
        var mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
        if (mainWindow == null)
            return;

        mainWindow.WindowState = WindowState.Minimized;
        mainWindow.Visibility = System.Windows.Visibility.Hidden;
    }

    public void Restore()
    {
        var mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
        if (mainWindow == null)
            return;

        mainWindow.Show();
        mainWindow.WindowState = WindowState.Normal;
        mainWindow.Activate();
    }

    public void Exit()
    {
        if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.PrepareExit();

        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
        _idleIcon?.Dispose();
        _recordingIcon?.Dispose();
        _processingIcon?.Dispose();
        _downloadingIcon?.Dispose();
        GC.SuppressFinalize(this);
    }
}
