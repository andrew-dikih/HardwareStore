namespace HardwareStore.Infrastructure.CosmosDb;

public class CosmosDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "HardwareStore";
    public string ContainerName { get; set; } = "Documents";
}
