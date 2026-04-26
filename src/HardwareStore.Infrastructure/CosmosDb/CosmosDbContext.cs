namespace HardwareStore.Infrastructure.CosmosDb;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

public class CosmosDbContext
{
    private readonly CosmosClient _client;
    private readonly string _databaseName;
    private readonly string _containerName;

    public CosmosDbContext(IOptions<CosmosDbSettings> settings)
    {
        var opts = settings.Value;
        _client = new CosmosClient(opts.ConnectionString, new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        });
        _databaseName = opts.DatabaseName;
        _containerName = opts.ContainerName;
    }

    public Container GetContainer() =>
        _client.GetContainer(_databaseName, _containerName);

    public async Task InitializeAsync()
    {
        var db = await _client.CreateDatabaseIfNotExistsAsync(_databaseName);
        await db.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(_containerName, "/documentType"));
    }
}
