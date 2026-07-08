using System.Windows;

namespace BudsMonitor.App;

/// <summary>
/// Diagnostics window (GOAL 9). Shows an environment summary and generates a local ZIP
/// bundle for debugging provider failures. Nothing is transmitted off the machine.
/// </summary>
public partial class DiagnosticsWindow : Window
{
    public DiagnosticsWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshSummary();
    }

    private void RefreshSummary()
    {
        if (System.Windows.Application.Current is App app)
        {
            SummaryItems.ItemsSource = app.BuildDiagnosticsSummaryLines();
        }
    }

    private async void OnGenerateClick(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is not App app)
        {
            return;
        }

        GenerateButton.IsEnabled = false;
        ResultText.Text = "번들 생성 중...";
        try
        {
            var path = await app.ExportDiagnosticsAsync();
            if (path is null)
            {
                ResultText.Text = "번들 생성에 실패했습니다. 로그를 확인하세요.";
            }
            else
            {
                ResultText.Text = $"생성됨: {path}";
                app.OpenDiagnosticsFolder();
                RefreshSummary();
            }
        }
        finally
        {
            GenerateButton.IsEnabled = true;
        }
    }

    private void OnOpenDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app)
        {
            app.OpenDiagnosticsFolder();
        }
    }

    private void OnOpenLogsClick(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app)
        {
            app.OpenLogsFolder();
        }
    }
}
