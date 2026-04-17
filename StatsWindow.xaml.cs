using System.Linq;
using System.Windows;
using PwDemo.Services;

namespace PwDemo;

/// <summary>
/// 精简统计窗口：显示原始/精简字符数、减少数量、以及具体删除了哪些标签与属性。
/// </summary>
public partial class StatsWindow : Window
{
    public StatsWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 根据传入的精简结果刷新窗口显示。
    /// </summary>
    public void ShowResult(SimplifyResult result)
    {
        OriginalLengthText.Text = $"{result.OriginalLength} 字符";
        SimplifiedLengthText.Text = $"{result.SimplifiedLength} 字符";
        RemovedLengthText.Text = $"{result.RemovedLength} 字符";
        RemovedRatioText.Text = $"{result.RemovedRatio:P1}";

        RemovedTagsText.Text = result.RemovedTags.Count == 0
            ? "（无）"
            : string.Join("、", result.RemovedTags
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key}×{kv.Value}"));

        RemovedAttrsText.Text = result.RemovedAttributes.Count == 0
            ? "（无）"
            : string.Join("、", result.RemovedAttributes
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key}×{kv.Value}"));
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
