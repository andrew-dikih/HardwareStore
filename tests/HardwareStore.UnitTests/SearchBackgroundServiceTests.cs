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

    // ── startup recovery ──────────────────────────────────────────────────

    [Fact]
    public async Task OnStartup_PendingRequestsAreRecoveredIntoQueue()
    {
        var queued = MakeQueued("req1");
        var stale = MakeProcessingExpiredLease("req2");

        var searchRepo = new Mock<ISearchRepository>();
        searchRepo.Setup(r => r.GetPendingAsync()).ReturnsAsync([queued, stale]);
        searchRepo.Setup(r => r.TryAcquireLeaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                  .ReturnsAsync(true);

        var jobService = new Mock<ISearchJobService>();
        jobService.Setup(j => j.ProcessSearchAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        var provider = BuildProvider(searchRepo, jobService);
        var logger = new Mock<ILogger<SearchBackgroundService>>();
        var svc = new SearchBackgroundService(provider, logger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await svc.StartAsync(cts.Token);
        await Task.Delay(1500);
        await svc.StopAsync(CancellationToken.None);

        // Both recovered IDs must have been processed
        jobService.Verify(j => j.ProcessSearchAsync("req1"), Times.Once);
        jobService.Verify(j => j.ProcessSearchAsync("req2"), Times.Once);
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

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await svc.StartAsync(cts.Token);
        await Task.Delay(400);
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

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Should not throw
        await svc.StartAsync(cts.Token);
        await Task.Delay(300);
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

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
        await svc.StartAsync(cts.Token);
        await Task.Delay(600);
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

        var jobService = new Mock<ISearchJobService>();
        jobService.Setup(j => j.ProcessSearchAsync("req2")).Returns(Task.CompletedTask);

        var provider = BuildProvider(searchRepo, jobService);
        var logger = new Mock<ILogger<SearchBackgroundService>>();
        var svc = new SearchBackgroundService(provider, logger.Object);

        SearchBackgroundService.EnqueueSearch("req2");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await svc.StartAsync(cts.Token);
        await Task.Delay(800);
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

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
        await svc.StartAsync(cts.Token);
        await Task.Delay(600);
        await svc.StopAsync(CancellationToken.None);

        // Service survived and item was not processed
        jobService.Verify(j => j.ProcessSearchAsync(It.IsAny<string>()), Times.Never);
    }
}
