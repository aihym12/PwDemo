using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace PwDemo.Services;

/// <summary>
/// 单次精简的结果，包含格式化后的 HTML 以及统计信息。
/// </summary>
public sealed class SimplifyResult
{
    /// <summary>格式化（带缩进、便于换行阅读）后的精简 HTML。</summary>
    public string Html { get; init; } = string.Empty;

    /// <summary>原始 HTML 字符数。</summary>
    public int OriginalLength { get; init; }

    /// <summary>精简后 HTML 字符数（未格式化时的长度，统计用）。</summary>
    public int SimplifiedLength { get; init; }

    /// <summary>减少的字符数。</summary>
    public int RemovedLength => OriginalLength - SimplifiedLength;

    /// <summary>减少比例（0-1）。</summary>
    public double RemovedRatio => OriginalLength == 0 ? 0 : (double)RemovedLength / OriginalLength;

    /// <summary>被整段删除的标签名及数量（如 script/style/注释等）。</summary>
    public Dictionary<string, int> RemovedTags { get; init; } = new();

    /// <summary>被删除的属性名及数量（如 class/style/on* 等）。</summary>
    public Dictionary<string, int> RemovedAttributes { get; init; } = new();

    /// <summary>生成一行中文摘要，适合放在状态栏显示。</summary>
    public string ToChineseSummary()
    {
        var sb = new StringBuilder();
        sb.Append($"原始 {OriginalLength} 字符，精简后 {SimplifiedLength} 字符，");
        sb.Append($"共精简 {RemovedLength} 字符（{RemovedRatio:P1}）");

        if (RemovedTags.Count > 0)
        {
            var tags = string.Join("、", RemovedTags
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key}×{kv.Value}"));
            sb.Append($"；删除标签：{tags}");
        }

        if (RemovedAttributes.Count > 0)
        {
            var attrs = string.Join("、", RemovedAttributes
                .OrderByDescending(kv => kv.Value)
                .Take(8)
                .Select(kv => $"{kv.Key}×{kv.Value}"));
            var more = RemovedAttributes.Count > 8 ? $" 等 {RemovedAttributes.Count} 类" : string.Empty;
            sb.Append($"；删除属性：{attrs}{more}");
        }

        return sb.ToString();
    }
}

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

    /// <summary>HTML 中不需要闭合标签的空元素（用于格式化）。</summary>
    private static readonly HashSet<string> VoidTags = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link",
        "meta", "param", "source", "track", "wbr"
    };

    /// <summary>格式化时保持内联（不换行）的标签，避免把文本切得支离破碎。</summary>
    private static readonly HashSet<string> InlineTags = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "a", "span", "b", "i", "u", "em", "strong", "small", "sub", "sup",
        "code", "kbd", "label", "abbr", "cite", "mark", "q", "s", "time", "var"
    };

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// 精简 HTML，并返回包含统计信息与格式化结果的对象。
    /// </summary>
    public SimplifyResult Simplify(string rawHtml)
    {
        if (string.IsNullOrWhiteSpace(rawHtml))
        {
            return new SimplifyResult();
        }

        var originalLength = rawHtml.Length;

        var doc = new HtmlDocument
        {
            OptionWriteEmptyNodes = true,
            OptionAutoCloseOnEnd = true
        };
        doc.LoadHtml(rawHtml);

        var root = doc.DocumentNode;

        var removedTags = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        var removedAttrs = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

        // 1. 删除不需要的整段节点（script/style/...）及注释。
        var toRemove = root.DescendantsAndSelf()
            .Where(n => n.NodeType == HtmlNodeType.Comment
                     || (n.NodeType == HtmlNodeType.Element && DropTags.Contains(n.Name)))
            .ToList();
        foreach (var n in toRemove)
        {
            var key = n.NodeType == HtmlNodeType.Comment ? "<!--注释-->" : n.Name.ToLowerInvariant();
            removedTags[key] = removedTags.TryGetValue(key, out var c) ? c + 1 : 1;
            n.Remove();
        }

        // 2. 清理属性：去掉 on* 事件、style、class 及不在白名单且不以 data- 开头的属性。
        foreach (var el in root.DescendantsAndSelf().Where(n => n.NodeType == HtmlNodeType.Element).ToList())
        {
            var attrs = el.Attributes.ToList();
            foreach (var attr in attrs)
            {
                var name = attr.Name.ToLowerInvariant();
                string? bucket = null;
                if (name.StartsWith("on"))
                {
                    bucket = "on*(事件)";
                }
                else if (name == "style" || name == "class")
                {
                    bucket = name;
                }
                else if (!KeepAttrs.Contains(name) && !name.StartsWith("data-"))
                {
                    bucket = name;
                }

                if (bucket != null)
                {
                    removedAttrs[bucket] = removedAttrs.TryGetValue(bucket, out var c) ? c + 1 : 1;
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

        var simplifiedHtml = doc.DocumentNode.OuterHtml;
        var formatted = FormatHtml(doc.DocumentNode);

        return new SimplifyResult
        {
            Html = formatted,
            OriginalLength = originalLength,
            SimplifiedLength = simplifiedHtml.Length,
            RemovedTags = removedTags,
            RemovedAttributes = removedAttrs
        };
    }

    /// <summary>
    /// 将节点树格式化为带缩进的字符串，便于阅读与自动换行。
    /// </summary>
    private static string FormatHtml(HtmlNode root)
    {
        var sb = new StringBuilder();
        foreach (var child in root.ChildNodes)
        {
            WriteNode(child, sb, 0);
        }
        return sb.ToString().TrimEnd();
    }

    private static void WriteNode(HtmlNode node, StringBuilder sb, int depth)
    {
        const string indentUnit = "  ";
        switch (node.NodeType)
        {
            case HtmlNodeType.Text:
                {
                    var text = node.InnerHtml.Trim();
                    if (text.Length == 0) return;
                    sb.Append(string.Concat(Enumerable.Repeat(indentUnit, depth)));
                    sb.Append(text).Append('\n');
                    break;
                }
            case HtmlNodeType.Comment:
                sb.Append(string.Concat(Enumerable.Repeat(indentUnit, depth)));
                sb.Append(node.OuterHtml).Append('\n');
                break;
            case HtmlNodeType.Element:
                {
                    var indent = string.Concat(Enumerable.Repeat(indentUnit, depth));
                    var openTag = BuildOpenTag(node);

                    if (VoidTags.Contains(node.Name) || !node.HasChildNodes)
                    {
                        sb.Append(indent).Append(openTag);
                        if (!VoidTags.Contains(node.Name))
                        {
                            sb.Append("</").Append(node.Name).Append('>');
                        }
                        sb.Append('\n');
                        return;
                    }

                    // 仅含单个文本子节点时，一行写完。
                    if (node.ChildNodes.Count == 1 && node.FirstChild.NodeType == HtmlNodeType.Text)
                    {
                        var text = node.FirstChild.InnerHtml.Trim();
                        sb.Append(indent).Append(openTag).Append(text)
                          .Append("</").Append(node.Name).Append(">\n");
                        return;
                    }

                    // 全部子节点都是内联元素/文本时，也尝试写在一行。
                    if (InlineTags.Contains(node.Name) &&
                        node.ChildNodes.All(c => c.NodeType == HtmlNodeType.Text
                                              || (c.NodeType == HtmlNodeType.Element && InlineTags.Contains(c.Name))))
                    {
                        sb.Append(indent).Append(openTag);
                        foreach (var c in node.ChildNodes)
                        {
                            if (c.NodeType == HtmlNodeType.Text) sb.Append(c.InnerHtml.Trim());
                            else sb.Append(c.OuterHtml);
                        }
                        sb.Append("</").Append(node.Name).Append(">\n");
                        return;
                    }

                    sb.Append(indent).Append(openTag).Append('\n');
                    foreach (var c in node.ChildNodes)
                    {
                        WriteNode(c, sb, depth + 1);
                    }
                    sb.Append(indent).Append("</").Append(node.Name).Append(">\n");
                    break;
                }
            case HtmlNodeType.Document:
                foreach (var c in node.ChildNodes) WriteNode(c, sb, depth);
                break;
        }
    }

    private static string BuildOpenTag(HtmlNode el)
    {
        var sb = new StringBuilder();
        sb.Append('<').Append(el.Name);
        foreach (var a in el.Attributes)
        {
            sb.Append(' ').Append(a.Name);
            if (a.Value != null)
            {
                sb.Append("=\"").Append(HtmlEntity.Entitize(a.Value, true, true)).Append('"');
            }
        }
        if (VoidTags.Contains(el.Name))
        {
            sb.Append(" />");
        }
        else
        {
            sb.Append('>');
        }
        return sb.ToString();
    }
}
