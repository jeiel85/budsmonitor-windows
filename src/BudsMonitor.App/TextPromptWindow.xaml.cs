using System.Windows;

namespace BudsMonitor.App;

/// <summary>
/// A small themed, non-modal text-input window replacing the unthemed VisualBasic InputBox so
/// prompts (e.g. device alias) match the Notion theme. Non-modal (like the app's other windows)
/// because a modal ShowDialog opened from a context-menu click can hang; the result is delivered
/// via <c>onCompleted</c> (null = cancelled).
/// </summary>
public partial class TextPromptWindow : Window
{
    private readonly Action<string?> _onCompleted;
    private bool _reported;

    public TextPromptWindow(string title, string message, string initial, Action<string?> onCompleted)
    {
        InitializeComponent();
        _onCompleted = onCompleted;
        Title = title;
        MessageText.Text = message;
        InputBox.Text = initial;
        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
        KeyDown += (_, args) =>
        {
            if (args.Key == System.Windows.Input.Key.Escape)
            {
                Close();
            }
        };
        Closed += (_, _) => Report(null); // closing via the X or Esc counts as cancel
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Report(InputBox.Text);
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private void Report(string? result)
    {
        if (_reported)
        {
            return;
        }

        _reported = true;
        _onCompleted(result);
    }
}
