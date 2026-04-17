using System;
using System.Windows;
using PwDemo.Services;

namespace PwDemo;

/// <summary>
/// 主窗口交互逻辑：粘贴原始 HTML，点击"精简 HTML"按钮，
/// 在右侧得到精简后的 HTML。
/// </summary>
public partial class MainWindow : Window
{
    private readonly HtmlSimplifierService _simplifier = new();
    private SimplifyResult? _lastResult;
    private StatsWindow? _statsWindow;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void SetStatus(string message) => StatusText.Text = message;

    private void SimplifyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var raw = RawHtmlTextBox.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
            {
                SetStatus("请先粘贴 HTML 代码");
                return;
            }

            SimplifyButton.IsEnabled = false;
            SetStatus("正在精简 HTML...");
            var result = _simplifier.Simplify(raw);
            _lastResult = result;
            SimplifiedHtmlTextBox.Text = result.Html;
            SetStatus(result.ToChineseSummary());
            ShowStatsWindow(result);
        }
        catch (Exception ex)
        {
            SetStatus($"精简失败：{ex.Message}");
        }
        finally
        {
            SimplifyButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// 弹出（或刷新）统计窗口，显示本次精简的字符数与删除明细。
    /// </summary>
    private void ShowStatsWindow(SimplifyResult result)
    {
        if (_statsWindow == null || !_statsWindow.IsLoaded)
        {
            _statsWindow = new StatsWindow { Owner = this };
            _statsWindow.Closed += (_, _) => _statsWindow = null;
            _statsWindow.ShowResult(result);
            _statsWindow.Show();
        }
        else
        {
            _statsWindow.ShowResult(result);
            _statsWindow.Activate();
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 复制时使用未格式化的紧凑 HTML，避免换行/缩进增加字符数。
            var text = _lastResult?.RawHtml;
            if (string.IsNullOrEmpty(text))
            {
                text = SimplifiedHtmlTextBox.Text ?? string.Empty;
            }
            if (string.IsNullOrEmpty(text))
            {
                SetStatus("没有可复制的内容");
                return;
            }
            Clipboard.SetText(text);
            SetStatus($"已复制到剪贴板（{text.Length} 字符，未换行）");
        }
        catch (Exception ex)
        {
            SetStatus($"复制失败：{ex.Message}");
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        RawHtmlTextBox.Clear();
        SimplifiedHtmlTextBox.Clear();
        _lastResult = null;
        SetStatus("已清空");
    }
}
