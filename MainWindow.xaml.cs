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
            SimplifiedHtmlTextBox.Text = _simplifier.Simplify(raw);
            SetStatus($"精简完成（{SimplifiedHtmlTextBox.Text.Length} 字符）");
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

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = SimplifiedHtmlTextBox.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                SetStatus("没有可复制的内容");
                return;
            }
            Clipboard.SetText(text);
            SetStatus("已复制到剪贴板");
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
        SetStatus("已清空");
    }
}
