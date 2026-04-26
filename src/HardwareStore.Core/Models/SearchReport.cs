namespace HardwareStore.Core.Models;

public class SearchReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SearchRequestId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string QuerySummary { get; set; } = string.Empty;
    public List<ProductComparison> ProductComparisons { get; set; } = new();
    public string RecommendedRetailerId { get; set; } = string.Empty;
    public string RecommendedRetailerName { get; set; } = string.Empty;
    public string RecommendationReason { get; set; } = string.Empty;
    public Dictionary<string, RetailerTotal> RetailerTotals { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string DocumentType { get; set; } = "searchreport";
}

public class ProductComparison
{
    public string ProductSelectionId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string SearchTerm { get; set; } = string.Empty;
    public bool IsAdditional { get; set; }
    public List<RetailerProductResult> RetailerResults { get; set; } = new();
}

public class RetailerProductResult
{
    public string RetailerId { get; set; } = string.Empty;
    public string RetailerName { get; set; } = string.Empty;
    public string ProductTitle { get; set; } = string.Empty;
    public string ProductUrl { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public string PriceDisplay { get; set; } = string.Empty;
    public string? PackageSize { get; set; }
    public double? QuantityInPackage { get; set; }
    public string? Unit { get; set; }
    public decimal? NormalizedPrice { get; set; }
    public string? NormalizedPriceDisplay { get; set; }
    public bool IsAvailable { get; set; } = true;
    public string? Sku { get; set; }
}

public class RetailerTotal
{
    public string RetailerId { get; set; } = string.Empty;
    public string RetailerName { get; set; } = string.Empty;
    public decimal TotalPrice { get; set; }
    public string TotalPriceDisplay { get; set; } = string.Empty;
    public int ProductsFound { get; set; }
    public int ProductsNotFound { get; set; }
}
