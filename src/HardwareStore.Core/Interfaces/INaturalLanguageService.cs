namespace HardwareStore.Core.Interfaces;
using HardwareStore.Core.Models;

public interface INaturalLanguageService
{
    Task<NaturalLanguageSearchResult> ParseSearchQueryAsync(string query);
}

public class NaturalLanguageSearchResult
{
    public List<ProductSelection> SuggestedProducts { get; set; } = new();
    public List<ProductSelection> AdditionalItems { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}
