using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using HardwareStore.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Concurrent;

namespace HardwareStore.UnitTests;

public class SearchBackgroundServiceTests
{
    // ── helpers ────────────────────────────────────────────────────────────

    private static SearchRequest MakeQueued(string id) => new()
    {
        Id = id,
        Status = SearchStatus.Queued,
        UserId = "user1",
        NaturalLanguageQuery = "test"
    };

    private static SearchRequest MakeProcessingExpiredLease(string id) => new()
    {
        Id = id,
        Status = SearchStatus.Processing,
        LeaseExpiresAt = DateTime.UtcNow.AddMinutes(-5),
        UserId = "user1",
        NaturalLanguageQuery = "test"
    };

    /// <summary>
    /// Builds a real ServiceProvider that supplies the given mocks.
    /// </summary>
    private static IServiceProvider BuildProvider(
        Mock<ISearchRepository> searchRepo,
        Mock<ISearchJobService> jobService)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => searchRepo.Object);
        services.AddScoped(_ => jobService.Object);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Polls until the condition is true or the timeout elapses.
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }

    // ── startup recovery ──────────────────────────────────────────────────

    [Fact]
    public async Task OnStartup_PendingRequestsAreRecoveredIntoQueue()
    {
        var queued = MakeQueued("recovered-queued-1");
        var stale = MakeProcessingExpiredLease("recovered-stale-2");

        var searchRepo = new Mock<ISearchRepository>();
        searchRepo.Setup(r => r.GetPendingAsync()).ReturnsAsync([queued, stale]);
        // Lease succeeds only for the expected IDs; default (false) covers any stale queue items
        searchRepo.Setup(r => r.TryAcquireLeaseAsync("recovered-queued-1", It.IsAny<string>(), It.IsAny<TimeSpan>()))
                  .ReturnsAsync(true);
        searchRepo.Setup(r => r.TryAcquireLeaseAsync("recovered-stale-2", It.IsAny<string>(), It.IsAny<TimeSpan>()))
                  .ReturnsAsync(true);

        var processed = new ConcurrentBag<string>();
        var jobService = new Mock<ISearchJobService>();
        jobService.Setup(j => j.ProcessSearchAsync(It.IsAny<string>()))
                  .Callback<string>(id => processed.Add(id))
                  .Returns(Task.CompletedTask);

        var provider = BuildProvider(searchRepo, jobService);
        var logger = new Mock<ILogger<SearchBackgroundService>>();
        var svc = new SearchBackgroundService(provider, logger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await svc.StartAsync(cts.Token);

        await WaitUntilAsync(
            () => processed.Contains("recovered-queued-1") && processed.Contains("recovered-stale-2"),
            TimeSpan.FromSeconds(3));

        await svc.StopAsync(CancellationToken.None);

        // Both recovered IDs must have been processed exactly once
        jobService.Verify(j => j.ProcessSearchAsync("recovered-queued-1"), Times.Once);
        jobService.Verify(j => j.ProcessSearchAsync("recovered-stale-2"), Times.Once);
    }

    [Fact]
    public async Task OnStartup_WhenNoPendingRequests_NothingIsEnqueued()
    {
        var searchRepo = new Mock<ISearchRepository>();
        searchRepo.Setup(r => r.GetPendingAsync()).ReturnsAsync([]);

        var jobService = new Mock<ISearchJobService>();

        var provider = BuildProvider(searchRepo, jobService);
        var logger = new Mock<ILogger<SearchBackgroundService>>();
        var svc = new SearchBackgroundService(provider, logger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await svc.StartAsync(cts.Token);
        await WaitUntilAsync(() => cts.IsCancellationRequested, TimeSpan.FromMilliseconds(400));
        await svc.StopAsync(CancellationToken.None);

        jobService.Verify(j => j.ProcessSearchAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task OnStartup_WhenRecoveryFails_ServiceContinuesWithoutCrashing()
    {
        var searchRepo = new Mock<ISearchRepository>();
        searchRepo.Setup(r => r.GetPendingAsync()).ThrowsAsync(new Exception("db offline"));

        var jobService = new Mock<ISearchJobService>();

        var provider = BuildProvider(searchRepo, jobService);
        var logger = new Mock<ILogger<SearchBackgroundService>>();
        var svc = new SearchBackgroundService(provider, logger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        // Should not throw
        await svc.StartAsync(cts.Token);
        await WaitUntilAsync(() => cts.IsCancellationRequested, TimeSpan.FromMilliseconds(400));
        await svc.StopAsync(CancellationToken.None);
    }

    // ── lease ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task WhenLeaseAcquisitionFails_ItemIsSkippedAndNotProcessed()
    {
        var searchRepo = new Mock<ISearchRepository>();
        searchRepo.Setup(r => r.GetPendingAsync()).ReturnsAsync([]);
        searchRepo.Setup(r => r.TryAcquireLeaseAsync("req1", It.IsAny<string>(), It.IsAny<TimeSpan>()))
                  .ReturnsAsync(false);

        var jobService = new Mock<ISearchJobService>();

        var provider = BuildProvider(searchRepo, jobService);
        var logger = new Mock<ILogger<SearchBackgroundService>>();
        var svc = new SearchBackgroundService(provider, logger.Object);

        // Enqueue manually before starting
        SearchBackgroundService.EnqueueSearch("req1");

        // Give the service enough time to dequeue and attempt the lease
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await svc.StartAsync(cts.Token);
        await WaitUntilAsync(
            () => searchRepo.Invocations.Any(i => i.Method.Name == nameof(ISearchRepository.TryAcquireLeaseAsync)),
            TimeSpan.FromSeconds(2));
        await svc.StopAsync(CancellationToken.None);

        jobService.Verify(j => j.ProcessSearchAsync("req1"), Times.Never);
    }

    [Fact]
    public async Task WhenLeaseAcquisitionSucceeds_ItemIsProcessed()
    {
        var searchRepo = new Mock<ISearchRepository>();
        searchRepo.Setup(r => r.GetPendingAsync()).ReturnsAsync([]);
        searchRepo.Setup(r => r.TryAcquireLeaseAsync("req2", It.IsAny<string>(), It.IsAny<TimeSpan>()))
                  .ReturnsAsync(true);

        var tcs = new TaskCompletionSource<bool>();
        var jobService = new Mock<ISearchJobService>();
        jobService.Setup(j => j.ProcessSearchAsync("req2"))
                  .Callback(() => tcs.TrySetResult(true))
                  .Returns(Task.CompletedTask);

        var provider = BuildProvider(searchRepo, jobService);
        var logger = new Mock<ILogger<SearchBackgroundService>>();
        var svc = new SearchBackgroundService(provider, logger.Object);

        SearchBackgroundService.EnqueueSearch("req2");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await svc.StartAsync(cts.Token);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await svc.StopAsync(CancellationToken.None);

        jobService.Verify(j => j.ProcessSearchAsync("req2"), Times.Once);
    }

    [Fact]
    public async Task WhenLeaseAcquisitionThrows_ItemIsSkippedAndServiceContinues()
    {
        var searchRepo = new Mock<ISearchRepository>();
        searchRepo.Setup(r => r.GetPendingAsync()).ReturnsAsync([]);
        searchRepo.Setup(r => r.TryAcquireLeaseAsync("req3", It.IsAny<string>(), It.IsAny<TimeSpan>()))
                  .ThrowsAsync(new Exception("cosmos error"));

        var jobService = new Mock<ISearchJobService>();

        var provider = BuildProvider(searchRepo, jobService);
        var logger = new Mock<ILogger<SearchBackgroundService>>();
        var svc = new SearchBackgroundService(provider, logger.Object);

        SearchBackgroundService.EnqueueSearch("req3");

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await svc.StartAsync(cts.Token);
        await WaitUntilAsync(
            () => searchRepo.Invocations.Any(i => i.Method.Name == nameof(ISearchRepository.TryAcquireLeaseAsync)),
            TimeSpan.FromSeconds(2));
        await svc.StopAsync(CancellationToken.None);

        // Service survived and item was not processed
        jobService.Verify(j => j.ProcessSearchAsync(It.IsAny<string>()), Times.Never);
    }
}
