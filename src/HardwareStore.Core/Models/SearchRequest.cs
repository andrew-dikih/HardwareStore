namespace HardwareStore.Core.Models;

public enum SearchStatus { Queued, Processing, Completed, Failed }

public class SearchRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string NaturalLanguageQuery { get; set; } = string.Empty;
    public List<ProductSelection> SelectedProducts { get; set; } = new();
    public List<ProductSelection> AdditionalItems { get; set; } = new();
    public List<string> SelectedRetailerIds { get; set; } = new();
    public SearchStatus Status { get; set; } = SearchStatus.Queued;
    public string? ReportId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string DocumentType { get; set; } = "searchrequest";
}

public class ProductSelection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string SearchTerm { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsSelected { get; set; } = true;
    public bool IsAdditional { get; set; } = false;
    public string? Unit { get; set; }
    public double? Quantity { get; set; }
}
