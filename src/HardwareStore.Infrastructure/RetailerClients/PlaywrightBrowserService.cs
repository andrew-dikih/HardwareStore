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
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = ["--no-sandbox", "--disable-setuid-sandbox"]
            });

            _logger.LogInformation("Playwright Chromium browser started");
            return _browser;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<string> GetHtmlAsync(string url)
    {
        var browser = await GetBrowserAsync();
        var page = await browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
        });

        try
        {
            await page.GotoAsync(url, new PageGotoOptions
            {
                Timeout = 30_000,
                WaitUntil = WaitUntilState.DOMContentLoaded
            });

            try
            {
                await page.WaitForSelectorAsync("script#__NEXT_DATA__", new PageWaitForSelectorOptions
                {
                    Timeout = 15_000
                });
            }
            catch (TimeoutException)
            {
                // __NEXT_DATA__ may not exist on all pages; return HTML anyway
                _logger.LogWarning("Timed out waiting for __NEXT_DATA__ script on {Url}; returning page HTML as-is", url);
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
