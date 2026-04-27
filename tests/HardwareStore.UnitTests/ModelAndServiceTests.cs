using HardwareStore.Core.Models;
using HardwareStore.Infrastructure.Services;

namespace HardwareStore.UnitTests;

public class ModelAndServiceTests
{
    // ── NoOpSearchStatusNotifier ─────────────────────────────────────────────

    [Fact]
    public async Task NoOpSearchStatusNotifier_DoesNotThrow()
    {
        var notifier = new NoOpSearchStatusNotifier();
        await notifier.NotifyStatusChangedAsync("id", "Completed", "report-1", null);
        // No exception = pass
    }

    [Fact]
    public async Task NoOpSearchStatusNotifier_WithNullOptionalParams_DoesNotThrow()
    {
        var notifier = new NoOpSearchStatusNotifier();
        await notifier.NotifyStatusChangedAsync("id", "Failed");
    }

    // ── ProductCandidateItem ─────────────────────────────────────────────────

    [Fact]
    public void ProductCandidateItem_DefaultValues_AreExpected()
    {
        var item = new ProductCandidateItem();

        Assert.Equal(string.Empty, item.RetailerId);
        Assert.Equal(string.Empty, item.RetailerName);
        Assert.Equal(string.Empty, item.ProductTitle);
        Assert.Equal(0m, item.Price);
        Assert.Equal(string.Empty, item.PriceDisplay);
        Assert.Null(item.ProductUrl);
        Assert.Null(item.ImageUrl);
    }

    [Fact]
    public void ProductCandidateItem_SetProperties_RoundTrip()
    {
        var item = new ProductCandidateItem
        {
            RetailerId = "homedepot",
            RetailerName = "Home Depot",
            ProductTitle = "Duck Tape",
            Price = 7.98m,
            PriceDisplay = "$7.98",
            ProductUrl = "https://homedepot.com/p/1",
            ImageUrl = "https://img.example.com/1.jpg"
        };

        Assert.Equal("homedepot", item.RetailerId);
        Assert.Equal("Home Depot", item.RetailerName);
        Assert.Equal("Duck Tape", item.ProductTitle);
        Assert.Equal(7.98m, item.Price);
        Assert.Equal("$7.98", item.PriceDisplay);
        Assert.Equal("https://homedepot.com/p/1", item.ProductUrl);
        Assert.Equal("https://img.example.com/1.jpg", item.ImageUrl);
    }

    // ── ProductCandidate ─────────────────────────────────────────────────────

    [Fact]
    public void ProductCandidate_DefaultValues_AreExpected()
    {
        var candidate = new ProductCandidate();

        Assert.False(string.IsNullOrEmpty(candidate.Id)); // auto-generated GUID
        Assert.Equal(string.Empty, candidate.SearchTerm);
        Assert.Equal(string.Empty, candidate.DisplayName);
        Assert.Empty(candidate.BrandNames);
        Assert.Equal(ProductCandidateConfidence.Exact, candidate.Confidence);
        Assert.Empty(candidate.Items);
    }

    [Fact]
    public void ProductCandidate_SetProperties_RoundTrip()
    {
        var item = new ProductCandidateItem { RetailerId = "lowes", Price = 9.99m };
        var candidate = new ProductCandidate
        {
            Id = "test-id",
            SearchTerm = "duct tape",
            DisplayName = "Duct Tape",
            BrandNames = ["Duck", "3M"],
            Confidence = ProductCandidateConfidence.SpecMatch,
            Items = [item]
        };

        Assert.Equal("test-id", candidate.Id);
        Assert.Equal("duct tape", candidate.SearchTerm);
        Assert.Equal("Duct Tape", candidate.DisplayName);
        Assert.Equal(2, candidate.BrandNames.Count);
        Assert.Equal(ProductCandidateConfidence.SpecMatch, candidate.Confidence);
        Assert.Single(candidate.Items);
        Assert.Equal(9.99m, candidate.Items[0].Price);
    }

    [Fact]
    public void ProductCandidateConfidence_EnumValues_AreCorrect()
    {
        Assert.Equal(0, (int)ProductCandidateConfidence.Exact);
        Assert.Equal(1, (int)ProductCandidateConfidence.SpecMatch);
        Assert.Equal(2, (int)ProductCandidateConfidence.Individual);
    }
}
