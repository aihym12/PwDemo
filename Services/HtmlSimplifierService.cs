using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace PwDemo.Services;

/// <summary>
/// 纯 C# 的 HTML 精简器。接收一段动态渲染后的 HTML，
/// 去掉脚本、样式、注释、事件处理属性、class/style 等无用信息，
/// 只保留对自动化（元素定位）有价值的语义属性。
/// </summary>
public sealed class HtmlSimplifierService
{
    /// <summary>需要保留的属性白名单。</summary>
    private static readonly HashSet<string> KeepAttrs = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "id", "name", "type", "role", "href", "src", "alt", "title", "value",
        "placeholder", "aria-label", "aria-labelledby", "aria-describedby",
        "data-testid", "data-test", "data-qa", "data-cy", "for", "action", "method"
    };

    /// <summary>整节点直接删除的标签。</summary>
    private static readonly HashSet<string> DropTags = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "noscript", "svg", "link", "meta"
    };

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public string Simplify(string rawHtml)
    {
        if (string.IsNullOrWhiteSpace(rawHtml))
        {
            return string.Empty;
        }

        var doc = new HtmlDocument
        {
            OptionWriteEmptyNodes = true,
            OptionAutoCloseOnEnd = true
        };
        doc.LoadHtml(rawHtml);

        var root = doc.DocumentNode;

        // 1. 删除不需要的整段节点（script/style/...）及注释。
        var toRemove = root.DescendantsAndSelf()
            .Where(n => n.NodeType == HtmlNodeType.Comment
                     || (n.NodeType == HtmlNodeType.Element && DropTags.Contains(n.Name)))
            .ToList();
        foreach (var n in toRemove)
        {
            n.Remove();
        }

        // 2. 清理属性：去掉 on* 事件、style、class 及不在白名单且不以 data- 开头的属性。
        foreach (var el in root.DescendantsAndSelf().Where(n => n.NodeType == HtmlNodeType.Element).ToList())
        {
            var attrs = el.Attributes.ToList();
            foreach (var attr in attrs)
            {
                var name = attr.Name.ToLowerInvariant();
                if (name.StartsWith("on")
                    || name == "style"
                    || name == "class"
                    || (!KeepAttrs.Contains(name) && !name.StartsWith("data-")))
                {
                    el.Attributes.Remove(attr);
                }
            }
        }

        // 3. 合并文本节点中的连续空白。
        foreach (var text in root.DescendantsAndSelf()
                     .Where(n => n.NodeType == HtmlNodeType.Text)
                     .ToList())
        {
            text.InnerHtml = WhitespaceRegex.Replace(text.InnerHtml, " ");
        }

        return doc.DocumentNode.OuterHtml;
    }
}
