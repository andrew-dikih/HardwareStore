namespace HardwareStore.Core.Models;

public enum ProductCandidateConfidence
{
    Exact = 0,      // Same/very similar product found at multiple retailers
    SpecMatch = 1,  // Different brand but matching specs at multiple retailers
    Individual = 2  // Single retailer, no cross-retailer match
}

public class ProductCandidateItem
{
    public string RetailerId { get; set; } = string.Empty;
    public string RetailerName { get; set; } = string.Empty;
    public string ProductTitle { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string PriceDisplay { get; set; } = string.Empty;
    public string? ProductUrl { get; set; }
    public string? ImageUrl { get; set; }
}

public class ProductCandidate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SearchTerm { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> BrandNames { get; set; } = new();
    public ProductCandidateConfidence Confidence { get; set; }
    public List<ProductCandidateItem> Items { get; set; } = new();
}
