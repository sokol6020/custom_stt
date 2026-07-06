using System.Collections.ObjectModel;
using System.Windows;
using customSTT.Models;
using customSTT.Services;

namespace customSTT;

public partial class HistoryWindow : Window
{
    private readonly ObservableCollection<TranscriptionEntry> _history;
    private readonly TranscriptionHistoryService _historyService;
    private readonly WindowIconManager _iconManager = new();

    public HistoryWindow(
        ObservableCollection<TranscriptionEntry> history,
        TranscriptionHistoryService historyService)
    {
        _history = history;
        _historyService = historyService;

        InitializeComponent();
        _iconManager.Apply(this);
        HistoryListBox.ItemsSource = _history;
    }

    protected override void OnClosed(EventArgs e)
    {
        _iconManager.Dispose();
        base.OnClosed(e);
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: TranscriptionEntry entry } || string.IsNullOrEmpty(entry.Text))
            return;

        Clipboard.SetText(entry.Text);
        MessageBox.Show("Текст скопирован в буфер обмена", "Информация",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: TranscriptionEntry entry })
            return;

        var preview = entry.Text.Length > 30 ? entry.Text[..30] + "..." : entry.Text;
        if (MessageBox.Show($"Удалить запись «{preview}»?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _history.Remove(entry);
        _historyService.RemoveFromHistory(entry.Id);
    }

    private void OnClearAllClick(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Очистить всю историю?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        _history.Clear();
        _historyService.ClearHistory();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
