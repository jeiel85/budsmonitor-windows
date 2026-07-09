using System.Windows;
using System.Windows.Documents;
using BudsMonitor.Application.Updates;

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
        RenderNotes(releaseNotes);
    }

    /// <summary>Renders the markdown release notes as themed inlines (headings, bullets, bold)
    /// so the dialog no longer shows literal <c>##</c> / <c>**</c> markers.</summary>
    private void RenderNotes(string releaseNotes)
    {
        NotesText.Inlines.Clear();
        var blocks = ReleaseNotesMarkdown.Parse(releaseNotes);
        if (blocks.Count == 0)
        {
            NotesText.Text = "릴리스 노트가 없습니다.";
            return;
        }

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (i > 0)
            {
                NotesText.Inlines.Add(new LineBreak());
                if (block.Kind == ReleaseNotesBlockKind.Heading)
                {
                    NotesText.Inlines.Add(new LineBreak()); // breathing room before a section heading
                }
            }

            if (block.Kind == ReleaseNotesBlockKind.Bullet)
            {
                if (block.Level > 0)
                {
                    NotesText.Inlines.Add(new Run(new string(' ', block.Level * 3)));
                }

                NotesText.Inlines.Add(new Run("• "));
            }

            var isHeading = block.Kind == ReleaseNotesBlockKind.Heading;
            foreach (var span in block.Spans)
            {
                var run = new Run(span.Text);
                if (isHeading || span.Bold)
                {
                    run.FontWeight = FontWeights.SemiBold;
                }

                if (isHeading)
                {
                    run.FontSize = block.Level <= 1 ? 15 : 13.5;
                    run.SetResourceReference(TextElement.ForegroundProperty, "InkBrush");
                }

                NotesText.Inlines.Add(run);
            }
        }
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
