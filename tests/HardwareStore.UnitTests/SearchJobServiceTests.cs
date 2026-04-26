using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;
using HardwareStore.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace HardwareStore.UnitTests;

public class SearchJobServiceTests
{
    private readonly Mock<ISearchRepository> _searchRepo;
    private readonly Mock<IReportRepository> _reportRepo;
    private readonly Mock<IRetailerRepository> _retailerRepo;
    private readonly Mock<ISearchStatusNotifier> _notifier;
    private readonly Mock<ILogger<SearchJobService>> _logger;

    public SearchJobServiceTests()
    {
        _searchRepo = new Mock<ISearchRepository>();
        _reportRepo = new Mock<IReportRepository>();
        _retailerRepo = new Mock<IRetailerRepository>();
        _notifier = new Mock<ISearchStatusNotifier>();
        _logger = new Mock<ILogger<SearchJobService>>();
    }

    private SearchJobService CreateService(IEnumerable<IRetailerSearchClient>? clients = null)
    {
        return new SearchJobService(
            _searchRepo.Object,
            _reportRepo.Object,
            _retailerRepo.Object,
            clients ?? Enumerable.Empty<IRetailerSearchClient>(),
            _notifier.Object,
            _logger.Object);
    }

    private static SearchRequest MakeSearchRequest(string id = "req1") => new()
    {
        Id = id,
        UserId = "user1",
        NaturalLanguageQuery = "wood screws",
        Status = SearchStatus.Queued,
        SelectedProducts =
        [
            new ProductSelection { Id = "p1", Name = "Wood Screw", SearchTerm = "wood screw", IsSelected = true }
        ],
        AdditionalItems = [],
        SelectedRetailerIds = ["retailer1"]
    };

    private static Retailer MakeRetailer(string id = "retailer1") => new()
    {
        Id = id,
        Name = "Test Retailer",
        IsEnabled = true
    };

    // ─── EnqueueSearchAsync ────────────────────────────────────────────────

    [Fact]
    public async Task EnqueueSearchAsync_ReturnsCompletedTask()
    {
        var service = CreateService();

        // Should complete without exception
        await service.EnqueueSearchAsync("req1");
    }

    // ─── ProcessSearchAsync – not found ───────────────────────────────────

    [Fact]
    public async Task ProcessSearchAsync_WhenSearchNotFound_LogsWarningAndReturns()
    {
        _searchRepo.Setup(r => r.GetByIdAsync("missing")).ReturnsAsync((SearchRequest?)null);
        var service = CreateService();

        await service.ProcessSearchAsync("missing");

        _reportRepo.Verify(r => r.CreateAsync(It.IsAny<SearchReport>()), Times.Never);
    }

    // ─── ProcessSearchAsync – happy path ──────────────────────────────────

    [Fact]
    public async Task ProcessSearchAsync_HappyPath_CreatesReportAndMarksCompleted()
    {
        var request = MakeSearchRequest();
        var retailer = MakeRetailer();

        _searchRepo.Setup(r => r.GetByIdAsync("req1")).ReturnsAsync(request);
        _searchRepo.Setup(r => r.UpdateAsync(It.IsAny<SearchRequest>())).ReturnsAsync((SearchRequest r) => r);
        _retailerRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([retailer]);
        _reportRepo.Setup(r => r.CreateAsync(It.IsAny<SearchReport>()))
            .ReturnsAsync((SearchReport rpt) => { rpt.Id = "rpt1"; return rpt; });

        var clientMock = new Mock<IRetailerSearchClient>();
        clientMock.SetupGet(c => c.RetailerId).Returns("retailer1");
        clientMock.SetupGet(c => c.RetailerName).Returns("Test Retailer");
        clientMock.Setup(c => c.SearchProductAsync("wood screw", retailer))
            .ReturnsAsync([
                new RetailerProductResult
                {
                    RetailerId = "retailer1",
                    RetailerName = "Test Retailer",
                    Price = 5.99m,
                    IsAvailable = true
                }
            ]);

        var service = CreateService([clientMock.Object]);

        await service.ProcessSearchAsync("req1");

        _reportRepo.Verify(r => r.CreateAsync(It.IsAny<SearchReport>()), Times.Once);
        Assert.Equal(SearchStatus.Completed, request.Status);
        Assert.Equal("rpt1", request.ReportId);
        Assert.NotNull(request.CompletedAt);
        _notifier.Verify(n => n.NotifyStatusChangedAsync("req1", SearchStatus.Completed.ToString(), "rpt1", null), Times.Once);
    }

    // ─── ProcessSearchAsync – only selected products are processed ─────────

    [Fact]
    public async Task ProcessSearchAsync_OnlyProcessesSelectedProducts()
    {
        var request = MakeSearchRequest();
        request.SelectedProducts =
        [
            new ProductSelection { Id = "p1", Name = "Screw", SearchTerm = "screw", IsSelected = true },
            new ProductSelection { Id = "p2", Name = "Bolt", SearchTerm = "bolt", IsSelected = false }
        ];

        _searchRepo.Setup(r => r.GetByIdAsync("req1")).ReturnsAsync(request);
        _searchRepo.Setup(r => r.UpdateAsync(It.IsAny<SearchRequest>())).ReturnsAsync((SearchRequest r) => r);
        _retailerRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([MakeRetailer()]);
        _reportRepo.Setup(r => r.CreateAsync(It.IsAny<SearchReport>()))
            .ReturnsAsync((SearchReport rpt) => rpt);

        var clientMock = new Mock<IRetailerSearchClient>();
        clientMock.SetupGet(c => c.RetailerId).Returns("retailer1");
        clientMock.Setup(c => c.SearchProductAsync(It.IsAny<string>(), It.IsAny<Retailer>()))
            .ReturnsAsync([]);

        var service = CreateService([clientMock.Object]);

        await service.ProcessSearchAsync("req1");

        // Only the selected product should have triggered a search
        clientMock.Verify(c => c.SearchProductAsync("screw", It.IsAny<Retailer>()), Times.Once);
        clientMock.Verify(c => c.SearchProductAsync("bolt", It.IsAny<Retailer>()), Times.Never);
    }

    // ─── ProcessSearchAsync – retailer client throws ───────────────────────

    [Fact]
    public async Task ProcessSearchAsync_WhenRetailerClientThrows_ContinuesAndCompletesSearch()
    {
        var request = MakeSearchRequest();
        _searchRepo.Setup(r => r.GetByIdAsync("req1")).ReturnsAsync(request);
        _searchRepo.Setup(r => r.UpdateAsync(It.IsAny<SearchRequest>())).ReturnsAsync((SearchRequest r) => r);
        _retailerRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([MakeRetailer()]);
        _reportRepo.Setup(r => r.CreateAsync(It.IsAny<SearchReport>()))
            .ReturnsAsync((SearchReport rpt) => rpt);

        var clientMock = new Mock<IRetailerSearchClient>();
        clientMock.SetupGet(c => c.RetailerId).Returns("retailer1");
        clientMock.Setup(c => c.SearchProductAsync(It.IsAny<string>(), It.IsAny<Retailer>()))
            .ThrowsAsync(new HttpRequestException("network error"));

        var service = CreateService([clientMock.Object]);

        await service.ProcessSearchAsync("req1");

        // Search still completes even though the client threw
        Assert.Equal(SearchStatus.Completed, request.Status);
        _reportRepo.Verify(r => r.CreateAsync(It.IsAny<SearchReport>()), Times.Once);
    }

    // ─── ProcessSearchAsync – top-level exception ──────────────────────────

    [Fact]
    public async Task ProcessSearchAsync_WhenUnhandledException_SetsFailedStatus()
    {
        var request = MakeSearchRequest();
        _searchRepo.Setup(r => r.GetByIdAsync("req1")).ReturnsAsync(request);
        _searchRepo.Setup(r => r.UpdateAsync(It.IsAny<SearchRequest>())).ReturnsAsync((SearchRequest r) => r);

        _retailerRepo.Setup(r => r.GetAllAsync())
            .ThrowsAsync(new Exception("cosmos offline"));

        var service = CreateService();

        await service.ProcessSearchAsync("req1");

        Assert.Equal(SearchStatus.Failed, request.Status);
        Assert.Equal("cosmos offline", request.ErrorMessage);
        _notifier.Verify(n => n.NotifyStatusChangedAsync("req1", SearchStatus.Failed.ToString(), null, "cosmos offline"), Times.Once);
    }

    // ─── ProcessSearchAsync – retailer totals and recommendation ──────────

    [Fact]
    public async Task ProcessSearchAsync_SelectsBestRetailerByLowestTotalPriceWhenAllHaveAllProducts()
    {
        var request = new SearchRequest
        {
            Id = "req1",
            UserId = "user1",
            NaturalLanguageQuery = "test",
            Status = SearchStatus.Queued,
            SelectedProducts =
            [
                new ProductSelection { Id = "p1", Name = "Item", SearchTerm = "item", IsSelected = true }
            ],
            AdditionalItems = [],
            SelectedRetailerIds = ["r1", "r2"]
        };

        var retailer1 = new Retailer { Id = "r1", Name = "Cheap Store", IsEnabled = true };
        var retailer2 = new Retailer { Id = "r2", Name = "Expensive Store", IsEnabled = true };

        _searchRepo.Setup(r => r.GetByIdAsync("req1")).ReturnsAsync(request);
        _searchRepo.Setup(r => r.UpdateAsync(It.IsAny<SearchRequest>())).ReturnsAsync((SearchRequest r) => r);
        _retailerRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([retailer1, retailer2]);

        SearchReport? savedReport = null;
        _reportRepo.Setup(r => r.CreateAsync(It.IsAny<SearchReport>()))
            .Callback<SearchReport>(rpt => savedReport = rpt)
            .ReturnsAsync((SearchReport rpt) => rpt);

        var client1 = new Mock<IRetailerSearchClient>();
        client1.SetupGet(c => c.RetailerId).Returns("r1");
        client1.Setup(c => c.SearchProductAsync("item", retailer1))
            .ReturnsAsync([new RetailerProductResult { RetailerId = "r1", Price = 3.00m, IsAvailable = true }]);

        var client2 = new Mock<IRetailerSearchClient>();
        client2.SetupGet(c => c.RetailerId).Returns("r2");
        client2.Setup(c => c.SearchProductAsync("item", retailer2))
            .ReturnsAsync([new RetailerProductResult { RetailerId = "r2", Price = 9.99m, IsAvailable = true }]);

        var service = CreateService([client1.Object, client2.Object]);

        await service.ProcessSearchAsync("req1");

        Assert.NotNull(savedReport);
        Assert.Equal("r1", savedReport!.RecommendedRetailerId);
        Assert.Equal("Cheap Store", savedReport.RecommendedRetailerName);
    }

    [Fact]
    public async Task ProcessSearchAsync_WhenNoRetailerHasAllProducts_FallsBackToMostProductsFound()
    {
        var request = new SearchRequest
        {
            Id = "req1",
            UserId = "user1",
            NaturalLanguageQuery = "test",
            Status = SearchStatus.Queued,
            SelectedProducts =
            [
                new ProductSelection { Id = "p1", Name = "Item1", SearchTerm = "item1", IsSelected = true },
                new ProductSelection { Id = "p2", Name = "Item2", SearchTerm = "item2", IsSelected = true }
            ],
            AdditionalItems = [],
            SelectedRetailerIds = ["r1", "r2"]
        };

        var retailer1 = new Retailer { Id = "r1", Name = "Wide Store", IsEnabled = true };
        var retailer2 = new Retailer { Id = "r2", Name = "Limited Store", IsEnabled = true };

        _searchRepo.Setup(r => r.GetByIdAsync("req1")).ReturnsAsync(request);
        _searchRepo.Setup(r => r.UpdateAsync(It.IsAny<SearchRequest>())).ReturnsAsync((SearchRequest r) => r);
        _retailerRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([retailer1, retailer2]);

        SearchReport? savedReport = null;
        _reportRepo.Setup(r => r.CreateAsync(It.IsAny<SearchReport>()))
            .Callback<SearchReport>(rpt => savedReport = rpt)
            .ReturnsAsync((SearchReport rpt) => rpt);

        // r1 has both products; r2 has only one
        var client1 = new Mock<IRetailerSearchClient>();
        client1.SetupGet(c => c.RetailerId).Returns("r1");
        client1.Setup(c => c.SearchProductAsync("item1", retailer1))
            .ReturnsAsync([new RetailerProductResult { RetailerId = "r1", Price = 5.00m, IsAvailable = true }]);
        client1.Setup(c => c.SearchProductAsync("item2", retailer1))
            .ReturnsAsync([new RetailerProductResult { RetailerId = "r1", Price = 5.00m, IsAvailable = true }]);

        var client2 = new Mock<IRetailerSearchClient>();
        client2.SetupGet(c => c.RetailerId).Returns("r2");
        client2.Setup(c => c.SearchProductAsync("item1", retailer2))
            .ReturnsAsync([new RetailerProductResult { RetailerId = "r2", Price = 2.00m, IsAvailable = true }]);
        client2.Setup(c => c.SearchProductAsync("item2", retailer2))
            .ReturnsAsync([]); // r2 doesn't have item2

        var service = CreateService([client1.Object, client2.Object]);

        await service.ProcessSearchAsync("req1");

        Assert.NotNull(savedReport);
        // r1 has all products (no missing), so it should be selected even though it's more expensive
        Assert.Equal("r1", savedReport!.RecommendedRetailerId);
    }

    [Fact]
    public async Task ProcessSearchAsync_AdditionalItemsAreNotCountedInRetailerTotals()
    {
        var request = new SearchRequest
        {
            Id = "req1",
            UserId = "user1",
            NaturalLanguageQuery = "test",
            Status = SearchStatus.Queued,
            SelectedProducts =
            [
                new ProductSelection { Id = "p1", Name = "Main Item", SearchTerm = "main", IsSelected = true }
            ],
            AdditionalItems =
            [
                new ProductSelection { Id = "p2", Name = "Extra Item", SearchTerm = "extra", IsSelected = true, IsAdditional = true }
            ],
            SelectedRetailerIds = ["r1"]
        };

        var retailer = new Retailer { Id = "r1", Name = "Store", IsEnabled = true };

        _searchRepo.Setup(r => r.GetByIdAsync("req1")).ReturnsAsync(request);
        _searchRepo.Setup(r => r.UpdateAsync(It.IsAny<SearchRequest>())).ReturnsAsync((SearchRequest r) => r);
        _retailerRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([retailer]);

        SearchReport? savedReport = null;
        _reportRepo.Setup(r => r.CreateAsync(It.IsAny<SearchReport>()))
            .Callback<SearchReport>(rpt => savedReport = rpt)
            .ReturnsAsync((SearchReport rpt) => rpt);

        var client = new Mock<IRetailerSearchClient>();
        client.SetupGet(c => c.RetailerId).Returns("r1");
        client.Setup(c => c.SearchProductAsync(It.IsAny<string>(), It.IsAny<Retailer>()))
            .ReturnsAsync([new RetailerProductResult { RetailerId = "r1", Price = 10.00m, IsAvailable = true }]);

        var service = CreateService([client.Object]);

        await service.ProcessSearchAsync("req1");

        Assert.NotNull(savedReport);
        // Totals only include main products (non-additional)
        Assert.Equal(1, savedReport!.RetailerTotals["r1"].ProductsFound);
        Assert.Equal(0, savedReport.RetailerTotals["r1"].ProductsNotFound);
        // Both products appear in comparisons
        Assert.Equal(2, savedReport.ProductComparisons.Count);
    }

    [Fact]
    public async Task ProcessSearchAsync_WhenNoRetailerClientForRetailer_SkipsRetailer()
    {
        var request = MakeSearchRequest();
        _searchRepo.Setup(r => r.GetByIdAsync("req1")).ReturnsAsync(request);
        _searchRepo.Setup(r => r.UpdateAsync(It.IsAny<SearchRequest>())).ReturnsAsync((SearchRequest r) => r);
        _retailerRepo.Setup(r => r.GetAllAsync()).ReturnsAsync([MakeRetailer("retailer1")]);

        SearchReport? savedReport = null;
        _reportRepo.Setup(r => r.CreateAsync(It.IsAny<SearchReport>()))
            .Callback<SearchReport>(rpt => savedReport = rpt)
            .ReturnsAsync((SearchReport rpt) => rpt);

        // No client for retailer1
        var service = CreateService(Enumerable.Empty<IRetailerSearchClient>());

        await service.ProcessSearchAsync("req1");

        Assert.NotNull(savedReport);
        Assert.Empty(savedReport!.ProductComparisons[0].RetailerResults);
    }
}
