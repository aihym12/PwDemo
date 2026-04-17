using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PwDemo.Scripts;

namespace PwDemo.Services;

/// <summary>
/// Owns the Playwright/Browser/Page lifecycle and exposes high-level
/// operations used by the UI (navigate, extract elements across frames,
/// simplify HTML).
/// </summary>
public sealed class PlaywrightService : IAsyncDisposable
{
    private readonly Action<string> _log;

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;

    public PlaywrightService(Action<string> log)
    {
        _log = log ?? (_ => { });
    }

    /// <summary>
    /// Launches Chromium (headed) and a fresh context/page. Safe to call
    /// multiple times; a subsequent call replaces the previous instance.
    /// </summary>
    public async Task LaunchAsync()
    {
        await DisposeAsync();

        // Ensure the browser binaries are installed. This is a no-op if
        // they already are.
        var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        if (exitCode != 0)
        {
            _log($"playwright install exited with code {exitCode}");
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false
        });
        _context = await _browser.NewContextAsync();
        _page = await _context.NewPageAsync();
        _log("Browser launched.");
    }

    /// <summary>
    /// Navigates to <paramref name="url"/> and returns the top-frame HTML.
    /// </summary>
    public async Task<string> NavigateAndGetHtmlAsync(string url)
    {
        var page = RequirePage();
        await page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });
        var html = await page.ContentAsync();
        _log($"Navigated to {url} ({html.Length} chars).");
        return html;
    }

    /// <summary>
    /// Extracts interactive/visible elements from the main frame and all
    /// child frames (iframes) using a JavaScript extractor that produces
    /// stable selectors even for elements with dynamic class names or no id.
    /// </summary>
    public async Task<string> ExtractElementsJsonAsync()
    {
        var page = RequirePage();
        var allElements = new List<JsonElement>();

        foreach (var frame in page.Frames)
        {
            try
            {
                var raw = await frame.EvaluateAsync<JsonElement>(JsScripts.ExtractElements);
                if (raw.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in raw.EnumerateArray())
                    {
                        using var doc = JsonDocument.Parse(item.GetRawText());
                        var dict = new Dictionary<string, JsonElement>();
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            dict[prop.Name] = prop.Value.Clone();
                        }
                        var frameUrlJson = JsonDocument.Parse(JsonSerializer.Serialize(frame.Url)).RootElement.Clone();
                        dict["frameUrl"] = frameUrlJson;
                        var merged = JsonSerializer.SerializeToElement(dict);
                        allElements.Add(merged);
                    }
                }
                _log($"Frame '{frame.Url}': extracted elements.");
            }
            catch (Exception ex)
            {
                _log($"Frame '{frame.Url}' extraction failed: {ex.Message}");
            }
        }

        return JsonSerializer.Serialize(allElements, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Runs the HTML simplifier script in every frame and concatenates the
    /// results, with a comment marker identifying each frame.
    /// </summary>
    public async Task<string> SimplifyHtmlAsync()
    {
        var page = RequirePage();
        var sb = new StringBuilder();

        foreach (var frame in page.Frames)
        {
            try
            {
                var simplified = await frame.EvaluateAsync<string>(JsScripts.SimplifyHtml);
                sb.AppendLine($"<!-- frame: {frame.Url} -->");
                sb.AppendLine(simplified);
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                _log($"Frame '{frame.Url}' simplify failed: {ex.Message}");
            }
        }

        return sb.ToString();
    }

    private IPage RequirePage()
    {
        if (_page is null)
        {
            throw new InvalidOperationException("Browser not launched. Click 'Launch Browser' first.");
        }
        return _page;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_context is not null)
            {
                await _context.CloseAsync();
                _context = null;
            }
            if (_browser is not null)
            {
                await _browser.CloseAsync();
                _browser = null;
            }
            _playwright?.Dispose();
            _playwright = null;
            _page = null;
        }
        catch (Exception ex)
        {
            _log($"Dispose error: {ex.Message}");
        }
    }
}
