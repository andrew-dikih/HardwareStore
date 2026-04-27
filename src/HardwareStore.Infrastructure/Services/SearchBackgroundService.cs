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
    private readonly string _instanceId = Guid.NewGuid().ToString();
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(10);
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
        _logger.LogInformation("Search background service started (instance {InstanceId})", _instanceId);

        await RecoverPendingRequestsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_queue.TryDequeue(out var searchRequestId))
            {
                using var scope = _serviceProvider.CreateScope();
                var searchRepo = scope.ServiceProvider.GetRequiredService<ISearchRepository>();

                bool leaseAcquired;
                try
                {
                    leaseAcquired = await searchRepo.TryAcquireLeaseAsync(searchRequestId, _instanceId, LeaseDuration);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error acquiring lease for search {Id}", searchRequestId);
                    continue;
                }

                if (!leaseAcquired)
                {
                    _logger.LogDebug("Could not acquire lease for search {Id} – skipping (already handled by another instance)", searchRequestId);
                    continue;
                }

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
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task RecoverPendingRequestsAsync(CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested)
            return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var searchRepo = scope.ServiceProvider.GetRequiredService<ISearchRepository>();
            var pending = await searchRepo.GetPendingAsync();

            foreach (var request in pending)
            {
                _queue.Enqueue(request.Id);
            }

            _logger.LogInformation("Recovered {Count} pending search request(s) into queue", pending.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to recover pending search requests on startup");
        }
    }
}

