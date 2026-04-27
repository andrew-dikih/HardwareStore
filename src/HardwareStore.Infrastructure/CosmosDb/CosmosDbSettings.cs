namespace HardwareStore.Infrastructure.CosmosDb;

public class CosmosDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "HardwareStore";
    public string ContainerName { get; set; } = "Documents";
    /// <summary>
    /// Disables TLS certificate validation. Use only for the local CosmosDB emulator.
    /// </summary>
    public bool AllowInsecure { get; set; } = false;
}
