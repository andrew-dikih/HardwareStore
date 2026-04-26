namespace HardwareStore.Infrastructure.Services;
using HardwareStore.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

public class SearchBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SearchBackgroundService> _logger;
    private static readonly ConcurrentQueue<string> _queue = new();

    public SearchBackgroundService(IServiceProvider serviceProvider, ILogger<SearchBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public static void EnqueueSearch(string searchRequestId)
    {
        _queue.Enqueue(searchRequestId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Search background service started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_queue.TryDequeue(out var searchRequestId))
            {
                using var scope = _serviceProvider.CreateScope();
                var jobService = scope.ServiceProvider.GetRequiredService<ISearchJobService>();
                
                try
                {
                    await jobService.ProcessSearchAsync(searchRequestId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error processing search {Id}", searchRequestId);
                }
            }
            else
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
