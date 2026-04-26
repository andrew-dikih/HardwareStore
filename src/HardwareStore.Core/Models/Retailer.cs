namespace HardwareStore.Core.Models;

public class Retailer
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsAvailableToAll { get; set; } = false;
    public string ScraperType { get; set; } = string.Empty;
    public Dictionary<string, string> Config { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string DocumentType { get; set; } = "retailer";
}
