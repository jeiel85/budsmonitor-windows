using System.Windows;

namespace BudsMonitor.App;

/// <summary>
/// Notifies the user of a newer release and drives the download. The actual folder swap +
/// restart is handled by the app (a PowerShell script) after the download completes.
/// </summary>
public partial class UpdateDialog : Window
{
    /// <summary>Raised when the user clicks "지금 업데이트".</summary>
    public event Action? UpdateRequested;

    /// <summary>Raised when the user clicks "이 버전 건너뛰기".</summary>
    public event Action? SkipRequested;

    public UpdateDialog(string tag, string releaseNotes)
    {
        InitializeComponent();
        VersionText.Text = $"{tag} 로 업데이트할 수 있습니다.";
        NotesText.Text = string.IsNullOrWhiteSpace(releaseNotes)
            ? "릴리스 노트가 없습니다."
            : releaseNotes;
    }

    public void ShowProgress(int percent, string status)
    {
        Dispatcher.Invoke(() =>
        {
            ActionPanel.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;
            ErrorText.Visibility = Visibility.Collapsed;
            ProgressBar.Value = percent;
            StatusText.Text = status;
        });
    }

    public void ShowError(string message)
    {
        Dispatcher.Invoke(() =>
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            ActionPanel.Visibility = Visibility.Visible;
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        });
    }

    private void Update_Click(object sender, RoutedEventArgs e)
    {
        ShowProgress(0, "다운로드 준비 중...");
        UpdateRequested?.Invoke();
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        SkipRequested?.Invoke();
        Close();
    }

    private void Later_Click(object sender, RoutedEventArgs e) => Close();
}
