namespace HardwareStore.Infrastructure.RetailerClients;
using Microsoft.Playwright;
using Microsoft.Extensions.Logging;

public sealed class PlaywrightBrowserService : IAsyncDisposable
{
    private readonly ILogger<PlaywrightBrowserService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public PlaywrightBrowserService(ILogger<PlaywrightBrowserService> logger)
    {
        _logger = logger;
    }

    private async Task<IBrowser> GetBrowserAsync()
    {
        if (_browser is not null) return _browser;

        await _initLock.WaitAsync();
        try
        {
            if (_browser is not null) return _browser;

            _playwright = await Playwright.CreateAsync();

            // Firefox has a different TLS/JS fingerprint to Chromium and is less commonly
            // targeted by bot detection rules. Fall back to Chromium if Firefox fails.
            try
            {
                _browser = await _playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true,
                });
                _logger.LogInformation("Playwright Firefox browser started");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Firefox launch failed, falling back to Chromium");
                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true,
                    Args = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-blink-features=AutomationControlled"],
                });
                _logger.LogInformation("Playwright Chromium browser started (fallback)");
            }

            return _browser;
        }
        finally
        {
            _initLock.Release();
        }
    }

    // Comprehensive stealth script covering the most common bot-detection vectors.
    // Based on well-known automation detection techniques used by Cloudflare, PerimeterX, etc.
    private const string StealthScript = """
        // Remove webdriver flag
        Object.defineProperty(navigator, 'webdriver', { get: () => undefined });

        // Realistic plugin list
        Object.defineProperty(navigator, 'plugins', {
            get: () => {
                const arr = [
                    { name: 'Chrome PDF Plugin', filename: 'internal-pdf-viewer', description: 'Portable Document Format' },
                    { name: 'Chrome PDF Viewer', filename: 'mhjfbmdgcfjbbpaeojofohoefgiehjai', description: '' },
                    { name: 'Native Client', filename: 'internal-nacl-plugin', description: '' },
                ];
                arr.__proto__ = PluginArray.prototype;
                return arr;
            }
        });

        // Realistic language settings
        Object.defineProperty(navigator, 'languages', { get: () => ['en-US', 'en'] });

        // Non-zero hardware concurrency
        Object.defineProperty(navigator, 'hardwareConcurrency', { get: () => 8 });

        // Non-zero device memory
        Object.defineProperty(navigator, 'deviceMemory', { get: () => 8 });

        // Spoof platform
        Object.defineProperty(navigator, 'platform', { get: () => 'Win32' });

        // Chrome runtime object (missing in automation)
        if (!window.chrome) {
            window.chrome = { runtime: {}, loadTimes: function(){}, csi: function(){}, app: {} };
        }

        // Permissions API — headless Chrome returns 'denied' for notifications; real Chrome returns 'default'
        const originalQuery = window.navigator.permissions.query;
        window.navigator.permissions.query = (parameters) =>
            parameters.name === 'notifications'
                ? Promise.resolve({ state: Notification.permission })
                : originalQuery(parameters);

        // Remove automation-related properties from window
        delete window.cdc_adoQpoasnfa76pfcZLmcfl_Array;
        delete window.cdc_adoQpoasnfa76pfcZLmcfl_Promise;
        delete window.cdc_adoQpoasnfa76pfcZLmcfl_Symbol;
    """;

    public async Task<string> GetHtmlAsync(string url)
    {
        var browser = await GetBrowserAsync();
        var page = await browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
        });

        await page.AddInitScriptAsync(StealthScript);

        try
        {
            await page.GotoAsync(url, new PageGotoOptions
            {
                Timeout = 30_000,
                WaitUntil = WaitUntilState.NetworkIdle
            });

            var title = await page.TitleAsync();
            _logger.LogInformation("Playwright loaded '{Title}' from {Url}", title, url);

            try
            {
                await page.WaitForSelectorAsync("script#__NEXT_DATA__", new PageWaitForSelectorOptions
                {
                    Timeout = 10_000
                });
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("__NEXT_DATA__ not found on '{Title}' ({Url})", title, url);
            }

            return await page.ContentAsync();
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }
        _playwright?.Dispose();
        _playwright = null;
        _initLock.Dispose();
    }
}
