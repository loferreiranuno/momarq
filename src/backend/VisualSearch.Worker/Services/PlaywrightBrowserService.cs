using System.Collections.Concurrent;
using Microsoft.Playwright;

namespace VisualSearch.Worker.Services;

/// <summary>
/// Manages a pool of Playwright browser instances for efficient page rendering.
/// Browsers are expensive to create (~300MB each), so we pool and reuse them.
/// </summary>
public sealed class PlaywrightBrowserService : IAsyncDisposable
{
    private readonly ILogger<PlaywrightBrowserService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly ConcurrentBag<IBrowserContext> _availableContexts = [];
    private readonly SemaphoreSlim _contextSemaphore;
    
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _initialized;
    private bool _disposed;

    private const int MaxContexts = 2;

    public PlaywrightBrowserService(ILogger<PlaywrightBrowserService> logger)
    {
        _logger = logger;
        _contextSemaphore = new SemaphoreSlim(MaxContexts, MaxContexts);
    }

    /// <summary>
    /// Ensures Playwright and browser are initialized.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;

            _logger.LogInformation("Initializing Playwright browser...");
            
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = [
                    "--disable-dev-shm-usage",
                    "--disable-gpu",
                    "--no-sandbox",
                    "--disable-setuid-sandbox"
                ]
            });

            _initialized = true;
            _logger.LogInformation("Playwright browser initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Playwright. Run 'playwright install chromium' to install browsers.");
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Acquires a browser context from the pool.
    /// </summary>
    public async Task<BrowserContextLease> AcquireContextAsync(CancellationToken ct = default)
    {
        await InitializeAsync(ct);
        await _contextSemaphore.WaitAsync(ct);

        try
        {
            if (_availableContexts.TryTake(out var context))
            {
                return new BrowserContextLease(context, this);
            }

            // Create new context with realistic browser settings
            var newContext = await _browser!.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                Locale = "es-ES",
                TimezoneId = "Europe/Madrid",
                JavaScriptEnabled = true
            });

            _logger.LogDebug("Created new browser context");
            return new BrowserContextLease(newContext, this);
        }
        catch
        {
            _contextSemaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Returns a context to the pool.
    /// </summary>
    internal void ReturnContext(IBrowserContext context)
    {
        if (_disposed)
        {
            _ = context.CloseAsync();
            return;
        }

        _availableContexts.Add(context);
        _contextSemaphore.Release();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _logger.LogInformation("Disposing Playwright browser service...");

        // Close all pooled contexts
        while (_availableContexts.TryTake(out var context))
        {
            await context.CloseAsync();
        }

        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
        _initLock.Dispose();
        _contextSemaphore.Dispose();

        _logger.LogInformation("Playwright browser service disposed");
    }
}

/// <summary>
/// RAII wrapper for browser context - automatically returns to pool on dispose.
/// </summary>
public sealed class BrowserContextLease : IAsyncDisposable
{
    private readonly PlaywrightBrowserService _service;
    private bool _disposed;

    public IBrowserContext Context { get; }

    internal BrowserContextLease(IBrowserContext context, PlaywrightBrowserService service)
    {
        Context = context;
        _service = service;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Clear cookies and storage between uses for isolation
        await Context.ClearCookiesAsync();
        
        _service.ReturnContext(Context);
    }
}
