using System;
using System.Windows;
using PwDemo.Services;

namespace PwDemo;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly PlaywrightService _playwright;

    public MainWindow()
    {
        InitializeComponent();
        _playwright = new PlaywrightService(Log);
        Closed += async (_, _) => await _playwright.DisposeAsync();
    }

    private void Log(string message)
    {
        Dispatcher.Invoke(() =>
        {
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            LogTextBox.ScrollToEnd();
        });
    }

    private void SetStatus(string message) =>
        Dispatcher.Invoke(() => StatusText.Text = message);

    private async void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LaunchButton.IsEnabled = false;
            SetStatus("Launching browser...");
            await _playwright.LaunchAsync();
            SetStatus("Browser ready");
        }
        catch (Exception ex)
        {
            Log($"Launch failed: {ex.Message}");
            SetStatus("Launch failed");
        }
        finally
        {
            LaunchButton.IsEnabled = true;
        }
    }

    private async void NavigateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var url = UrlTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(url))
            {
                Log("URL is empty.");
                return;
            }
            NavigateButton.IsEnabled = false;
            SetStatus($"Navigating to {url}...");
            var html = await _playwright.NavigateAndGetHtmlAsync(url);
            RawHtmlTextBox.Text = html;
            SetStatus("Navigation complete");
        }
        catch (Exception ex)
        {
            Log($"Navigate failed: {ex.Message}");
            SetStatus("Navigate failed");
        }
        finally
        {
            NavigateButton.IsEnabled = true;
        }
    }

    private async void ExtractButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ExtractButton.IsEnabled = false;
            SetStatus("Extracting elements (including iframes)...");
            var json = await _playwright.ExtractElementsJsonAsync();
            ElementsJsonTextBox.Text = json;
            SetStatus("Elements extracted");
        }
        catch (Exception ex)
        {
            Log($"Extract failed: {ex.Message}");
            SetStatus("Extract failed");
        }
        finally
        {
            ExtractButton.IsEnabled = true;
        }
    }

    private async void SimplifyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SimplifyButton.IsEnabled = false;
            SetStatus("Simplifying HTML...");
            var simplified = await _playwright.SimplifyHtmlAsync();
            SimplifiedHtmlTextBox.Text = simplified;
            SetStatus("HTML simplified");
        }
        catch (Exception ex)
        {
            Log($"Simplify failed: {ex.Message}");
            SetStatus("Simplify failed");
        }
        finally
        {
            SimplifyButton.IsEnabled = true;
        }
    }

    private async void CloseBrowserButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _playwright.DisposeAsync();
            SetStatus("Browser closed");
        }
        catch (Exception ex)
        {
            Log($"Close failed: {ex.Message}");
        }
    }
}
